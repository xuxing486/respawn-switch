using RespawnSwitch.Application.Windows;
using RespawnSwitch.Windows.Identity;
using RespawnSwitch.Windows.Windows;

namespace RespawnSwitch.Windows.Tests.Windows;

public sealed class LeagueWindowLocatorTests
{
    [Fact]
    public async Task TryFindAsync_returns_the_unique_borderless_League_game_window_without_a_process_path()
    {
        var windows = new StubWindows(
            Window(100, 7, "RiotWindow", new PixelRect(0, 0, 1000, 700), 0),
            Window(101, 8, "LeagueWindowClass", new PixelRect(0, 0, 1920, 1080), 0));
        var processes = new StubProcesses(
            new ToolhelpProcessEntry(7, "RiotClientServices.exe"),
            new ToolhelpProcessEntry(8, "League of Legends.exe"));
        var locator = new LeagueWindowLocator(windows, processes);

        var target = await locator.TryFindAsync("game-1", CancellationToken.None);

        Assert.NotNull(target);
        Assert.Equal(101, target.Identity.Handle.Value);
        Assert.Equal("League of Legends.exe", target.ProcessName);
        Assert.Equal("game-1", target.TimelineKey);
        Assert.True(target.IsBorderless);
    }

    [Fact]
    public async Task TryFindAsync_returns_null_for_a_decorated_League_window()
    {
        const nint captionStyle = 0x00C00000;
        var locator = new LeagueWindowLocator(
            new StubWindows(Window(101, 8, "LeagueWindowClass", new PixelRect(0, 0, 1200, 700), captionStyle)),
            new StubProcesses(new ToolhelpProcessEntry(8, "League of Legends.exe")));

        var target = await locator.TryFindAsync("game-1", CancellationToken.None);

        Assert.Null(target);
    }

    [Fact]
    public async Task TryFindAsync_returns_null_when_multiple_game_windows_are_equally_eligible()
    {
        var locator = new LeagueWindowLocator(
            new StubWindows(
                Window(101, 8, "LeagueWindowClass", new PixelRect(0, 0, 1000, 700), 0),
                Window(102, 9, "LeagueWindowClass", new PixelRect(1000, 0, 1920, 700), 0)),
            new StubProcesses(
                new ToolhelpProcessEntry(8, "League of Legends.exe"),
                new ToolhelpProcessEntry(9, "League of Legends.exe")));

        Assert.Null(await locator.TryFindAsync("game-1", CancellationToken.None));
    }

    private static NativeWindowSnapshot Window(nint handle, int pid, string windowClass, PixelRect bounds, nint style) => new(
        new WindowIdentity(new NativeWindowHandle(handle), pid, windowClass), true, true, false,
        bounds, bounds, bounds, new PixelRect(0, 0, 1920, 1080), style);

    private sealed class StubWindows(params NativeWindowSnapshot[] values) : IWindowSnapshotSource
    {
        public IReadOnlyList<NativeWindowSnapshot> EnumerateTopLevelWindows() => values;
        public NativeWindowSnapshot? TryGetWindow(NativeWindowHandle handle) => values.SingleOrDefault(window => window.Identity.Handle == handle);
    }

    private sealed class StubProcesses(params ToolhelpProcessEntry[] values) : IToolhelpProcessSnapshot
    {
        public IReadOnlyList<ToolhelpProcessEntry> EnumerateProcesses() => values;
    }
}
