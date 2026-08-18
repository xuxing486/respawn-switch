using RespawnSwitch.Application.Windows;
using RespawnSwitch.Windows.Identity;
using RespawnSwitch.Windows.Windows;

namespace RespawnSwitch.Windows.Tests.Windows;

public sealed class LeagueClientPresenceProbeTests
{
    [Fact]
    public void Probe_RequiresVisibleWindowOwnedByLeagueClientUx()
    {
        var windows = new StubWindows(new NativeWindowSnapshot(new(new(1), 77, "Chrome_WidgetWin_1"), true, true, false,
            new(0, 0, 1000, 700), new(0, 0, 1000, 700), new(0, 0, 1000, 700), new(0, 0, 1920, 1080), 0));
        var processes = new StubProcesses([new(77, "LeagueClientUx.exe")]);

        var result = new LeagueClientPresenceProbe(windows, processes).Probe();

        Assert.True(result.IsReady);
        Assert.Equal("league.client.ready", result.Code);
    }

    [Fact]
    public void Probe_ExplainsMissingClient()
    {
        var result = new LeagueClientPresenceProbe(new StubWindows(), new StubProcesses([])).Probe();
        Assert.False(result.IsReady);
        Assert.Equal("league.client.not-running", result.Code);
    }

    private sealed class StubWindows(params NativeWindowSnapshot[] windows) : IWindowSnapshotSource
    {
        public IReadOnlyList<NativeWindowSnapshot> EnumerateTopLevelWindows() => windows;
        public NativeWindowSnapshot? TryGetWindow(NativeWindowHandle handle) => windows.SingleOrDefault(x => x.Identity.Handle == handle);
    }
    private sealed class StubProcesses(IReadOnlyList<ToolhelpProcessEntry> entries) : IToolhelpProcessSnapshot
    {
        public IReadOnlyList<ToolhelpProcessEntry> EnumerateProcesses() => entries;
    }
}
