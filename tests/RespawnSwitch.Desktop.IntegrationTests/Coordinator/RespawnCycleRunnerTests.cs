using RespawnSwitch.App;
using RespawnSwitch.Core.Respawn;

namespace RespawnSwitch.Desktop.IntegrationTests.Coordinator;

public sealed class RespawnCycleRunnerTests
{
    [Fact]
    public async Task Start_DoesNotWaitForBlockedExternalWork_AndCancelPreemptsIt()
    {
        await using var runner = new RespawnCycleRunner();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cycle = RespawnCycleId.New();

        runner.Start(cycle, async token =>
        {
            started.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            catch (OperationCanceledException) { cancelled.TrySetResult(); }
        });

        await started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await runner.CancelAsync(cycle, TimeSpan.FromMilliseconds(500));
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.False(runner.IsRunning(cycle));
    }

    [Fact]
    public async Task Cancel_timeout_keeps_work_tracked_until_it_really_finishes()
    {
        await using var runner = new RespawnCycleRunner();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cycle = RespawnCycleId.New();
        runner.Start(cycle, async _ => { started.SetResult(); await release.Task; });
        await started.Task;

        await runner.CancelAsync(cycle, TimeSpan.FromMilliseconds(20));

        Assert.True(runner.IsRunning(cycle));
        release.SetResult();
        await Task.Delay(30);
        Assert.False(runner.IsRunning(cycle));
    }
}
