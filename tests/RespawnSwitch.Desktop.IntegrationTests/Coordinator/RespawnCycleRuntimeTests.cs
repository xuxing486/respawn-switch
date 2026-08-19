// Author: Stress Monster
using RespawnSwitch.App.Cycles;
using RespawnSwitch.Core.Respawn;

namespace RespawnSwitch.Desktop.IntegrationTests.Coordinator;

public sealed class RespawnCycleRuntimeTests
{
    [Fact]
    public async Task Return_rejects_a_late_enter_commit()
    {
        await using var runtime = NewRuntime();
        var entered = NewSignal();
        var release = NewSignal();
        runtime.StartEnter(async (cycle, _) =>
        {
            entered.SetResult();
            await release.Task;
            Assert.False(cycle.TryCommit(c => c.DesktopAttached = true));
        });

        await entered.Task;
        var returning = runtime.ReturnOnceAsync(_ => Task.CompletedTask);
        release.SetResult();
        await returning;

        Assert.Equal(RespawnCycleStage.Completed, runtime.Stage);
        Assert.False(runtime.DesktopAttached);
    }

    [Fact]
    public async Task Return_waits_for_enter_cleanup_instead_of_abandoning_the_task()
    {
        await using var runtime = NewRuntime();
        var entered = NewSignal();
        var cleanupFinished = NewSignal();
        runtime.StartEnter(async (_, token) =>
        {
            entered.SetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            catch (OperationCanceledException) { await Task.Delay(30); }
            finally { cleanupFinished.SetResult(); }
        });

        await entered.Task;
        var returning = runtime.ReturnOnceAsync(_ => Task.CompletedTask);

        Assert.False(returning.IsCompleted);
        await returning;
        Assert.True(cleanupFinished.Task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Duplicate_return_executes_cleanup_once()
    {
        await using var runtime = NewRuntime();
        var count = 0;

        await Task.WhenAll(
            runtime.ReturnOnceAsync(_ => { Interlocked.Increment(ref count); return Task.CompletedTask; }),
            runtime.ReturnOnceAsync(_ => { Interlocked.Increment(ref count); return Task.CompletedTask; }));

        Assert.Equal(1, count);
        Assert.Equal(RespawnCycleStage.Completed, runtime.Stage);
    }

    private static RespawnCycleRuntime NewRuntime() => new(RespawnCycleId.New(), CancellationToken.None);
    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
