using RespawnSwitch.App.Browser;

namespace RespawnSwitch.Desktop.IntegrationTests.Browser;

public sealed class BrowserBridgeStateTests
{
    [Fact]
    public async Task IssueAsync_WaitsForMatchingVerifiedResult()
    {
        var state = new BrowserBridgeState();
        var cycle = Guid.NewGuid();
        var pending = state.IssueAsync(cycle, "play", TimeSpan.FromSeconds(1), CancellationToken.None);
        var command = state.ReadAfter(0);
        Assert.NotNull(command);
        Assert.Equal("play", command.Command);

        state.Publish(new(cycle, command.Sequence, true, "playing", "chrome", 1, ""));

        var result = await pending;
        Assert.True(result.Ok);
        Assert.Equal("playing", result.State);
    }

    [Fact]
    public void ReadAfter_DoesNotRepeatAcknowledgedCommand()
    {
        var state = new BrowserBridgeState();
        _ = state.IssueAsync(Guid.NewGuid(), "pause", TimeSpan.FromMilliseconds(10), CancellationToken.None);
        var command = state.ReadAfter(0)!;
        Assert.Null(state.ReadAfter(command.Sequence));
    }

    [Fact]
    public async Task Pause_supersedes_unconfirmed_play_and_stale_publish_is_ignored()
    {
        var state = new BrowserBridgeState();
        var cycle = Guid.NewGuid();
        var play = state.IssueAsync(cycle, "play", TimeSpan.FromSeconds(1), CancellationToken.None);
        var playCommand = state.ReadAfter(0)!;
        var pause = state.IssueAsync(cycle, "pause", TimeSpan.FromSeconds(1), CancellationToken.None);
        var pauseCommand = state.ReadAfter(playCommand.Sequence)!;

        state.Publish(new(cycle, playCommand.Sequence, true, "playing", "chrome", 1, ""));
        Assert.False((await play).Ok);
        Assert.False(pause.IsCompleted);

        state.Publish(new(cycle, pauseCommand.Sequence, true, "paused", "chrome", 1, ""));
        Assert.True((await pause).Ok);
    }

    [Fact]
    public async Task Publish_from_another_cycle_cannot_complete_current_command()
    {
        var state = new BrowserBridgeState();
        var cycle = Guid.NewGuid();
        var pending = state.IssueAsync(cycle, "pause", TimeSpan.FromSeconds(1), CancellationToken.None);
        var command = state.ReadAfter(0)!;

        state.Publish(new(Guid.NewGuid(), command.Sequence, true, "paused", "chrome", 1, ""));
        Assert.False(pending.IsCompleted);
        state.Publish(new(cycle, command.Sequence, true, "paused", "chrome", 1, ""));
        Assert.True((await pending).Ok);
    }
}
