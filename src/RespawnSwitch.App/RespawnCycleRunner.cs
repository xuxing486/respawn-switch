using System.Collections.Concurrent;
using RespawnSwitch.Core.Respawn;

namespace RespawnSwitch.App;

public sealed class RespawnCycleRunner : IAsyncDisposable
{
    private sealed record Active(CancellationTokenSource Cancellation, Task Task);
    private readonly ConcurrentDictionary<RespawnCycleId, Active> active = new();
    private readonly CancellationTokenSource shutdown = new();

    public bool IsRunning(RespawnCycleId cycleId) => active.ContainsKey(cycleId);

    public void Start(RespawnCycleId cycleId, Func<CancellationToken, Task> work)
    {
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!active.TryAdd(cycleId, new(cancellation, finished.Task))) { cancellation.Dispose(); return; }
        _ = Task.Run(async () =>
        {
            try { await work(cancellation.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (Exception ex) { finished.TrySetException(ex); }
            finally
            {
                active.TryRemove(cycleId, out _);
                cancellation.Dispose();
                finished.TrySetResult();
            }
        }, CancellationToken.None);
    }

    public async Task CancelAsync(RespawnCycleId cycleId, TimeSpan timeout)
    {
        if (!active.TryGetValue(cycleId, out var item)) return;
        item.Cancellation.Cancel();
        try { await item.Task.WaitAsync(timeout).ConfigureAwait(false); }
        catch (TimeoutException) { }
    }

    public async ValueTask DisposeAsync()
    {
        shutdown.Cancel();
        var tasks = active.Values.Select(x => x.Task).ToArray();
        try { await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false); }
        catch (Exception) { }
        shutdown.Dispose();
    }
}
