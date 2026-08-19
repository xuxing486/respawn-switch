// Author: Stress Monster
using RespawnSwitch.Application.Media;
using RespawnSwitch.Core.Respawn;

namespace RespawnSwitch.App.Cycles;

public sealed class RespawnCycleRuntime : IAsyncDisposable
{
    private readonly object gate = new();
    private readonly CancellationTokenSource enterCancellation;
    private Task enterTask = Task.CompletedTask;
    private Task? returnTask;
    private RespawnCycleStage stage = RespawnCycleStage.Created;

    public RespawnCycleRuntime(RespawnCycleId cycleId, CancellationToken shutdownToken)
    {
        CycleId = cycleId;
        enterCancellation = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
    }

    public RespawnCycleId CycleId { get; }
    public RespawnCycleStage Stage { get { lock (gate) return stage; } }
    public bool DesktopAttached { get; set; }
    public bool WebCommandIssued { get; set; }
    public IDouyinMediaController? Media { get; set; }

    public void StartEnter(Func<RespawnCycleRuntime, CancellationToken, Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        lock (gate)
        {
            if (stage != RespawnCycleStage.Created) return;
            stage = RespawnCycleStage.EnteringDouyin;
            enterTask = RunEnterAsync(work);
        }
    }

    public bool TryCommit(Action<RespawnCycleRuntime> commit)
    {
        ArgumentNullException.ThrowIfNull(commit);
        lock (gate)
        {
            if (stage != RespawnCycleStage.EnteringDouyin) return false;
            commit(this);
            stage = RespawnCycleStage.WatchingDouyin;
            return true;
        }
    }

    public Task RequestReturnAsync() => ReturnOnceAsync(_ => Task.CompletedTask);

    public Task ReturnOnceAsync(Func<RespawnCycleRuntime, Task> cleanup)
    {
        ArgumentNullException.ThrowIfNull(cleanup);
        lock (gate)
        {
            return returnTask ??= RunReturnAsync(cleanup);
        }
    }

    private async Task RunEnterAsync(Func<RespawnCycleRuntime, CancellationToken, Task> work)
    {
        try { await work(this, enterCancellation.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (enterCancellation.IsCancellationRequested) { }
    }

    private async Task RunReturnAsync(Func<RespawnCycleRuntime, Task> cleanup)
    {
        lock (gate)
        {
            if (stage == RespawnCycleStage.Completed) return;
            stage = RespawnCycleStage.ReturningToLeague;
        }

        enterCancellation.Cancel();
        try { await enterTask.ConfigureAwait(false); }
        catch (OperationCanceledException) when (enterCancellation.IsCancellationRequested) { }

        try { await cleanup(this).ConfigureAwait(false); }
        finally { lock (gate) stage = RespawnCycleStage.Completed; }
    }

    public async ValueTask DisposeAsync()
    {
        enterCancellation.Cancel();
        try { await enterTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
        enterCancellation.Dispose();
    }
}
