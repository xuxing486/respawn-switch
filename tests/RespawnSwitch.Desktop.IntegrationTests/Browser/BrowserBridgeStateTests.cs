using RespawnSwitch.App.Browser;

namespace RespawnSwitch.Desktop.IntegrationTests.Browser;

public sealed class BrowserBridgeStateTests
{
    [Fact]
    public async Task IssueAsync_WaitsForMatchingVerifiedResult()
    {
        var state = new BrowserBridgeState();
        var pending = state.IssueAsync("play", TimeSpan.FromSeconds(1), CancellationToken.None);
        var command = state.ReadAfter(0);
        Assert.NotNull(command);
        Assert.Equal("play", command.Command);

        state.Publish(new(command.Sequence, true, "playing", "chrome", 1, ""));

        var result = await pending;
        Assert.True(result.Ok);
        Assert.Equal("playing", result.State);
    }

    [Fact]
    public void ReadAfter_DoesNotRepeatAcknowledgedCommand()
    {
        var state = new BrowserBridgeState();
        _ = state.IssueAsync("pause", TimeSpan.FromMilliseconds(10), CancellationToken.None);
        var command = state.ReadAfter(0)!;
        Assert.Null(state.ReadAfter(command.Sequence));
    }
}
