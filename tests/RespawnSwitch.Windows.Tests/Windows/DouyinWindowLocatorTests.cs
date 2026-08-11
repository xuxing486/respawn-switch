using RespawnSwitch.Application.Windows;
using RespawnSwitch.Windows.Identity;
using RespawnSwitch.Windows.Windows;

namespace RespawnSwitch.Windows.Tests.Windows;

public sealed class DouyinWindowLocatorTests
{
    [Fact]
    public async Task TryFindAsync_requires_an_exact_normalized_path_and_calibrated_window_class()
    {
        var identity = Process(77, "C:\\Apps\\Douyin\\Douyin.exe");
        var locator = new DouyinWindowLocator(
            new StubWindows(
                Window(1, 77, "WrongClass"),
                Window(2, 77, "DouyinMain")),
            new StubIdentityReader(identity));

        var result = await locator.TryFindAsync("c:/apps/douyin/douyin.exe", "DouyinMain", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.Identity.Handle.Value);
        Assert.Equal(identity, result.Process);
    }

    [Fact]
    public async Task TryFindAsync_fails_closed_when_two_windows_match_the_same_calibration()
    {
        var identity = Process(77, "C:\\Apps\\Douyin\\Douyin.exe");
        var locator = new DouyinWindowLocator(
            new StubWindows(Window(1, 77, "DouyinMain"), Window(2, 77, "DouyinMain")),
            new StubIdentityReader(identity));

        Assert.Null(await locator.TryFindAsync("C:\\Apps\\Douyin\\Douyin.exe", "DouyinMain", CancellationToken.None));
    }

    private static ProcessIdentity Process(int pid, string path) => new(pid, new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero), path, "ByteDance", "ABC");
    private static NativeWindowSnapshot Window(nint handle, int pid, string windowClass) => new(
        new WindowIdentity(new NativeWindowHandle(handle), pid, windowClass), true, true, false,
        new PixelRect(10, 10, 300, 200), new PixelRect(10, 10, 300, 200), new PixelRect(10, 10, 300, 200), new PixelRect(0, 0, 1920, 1080), 0);

    private sealed class StubWindows(params NativeWindowSnapshot[] values) : IWindowSnapshotSource
    {
        public IReadOnlyList<NativeWindowSnapshot> EnumerateTopLevelWindows() => values;
        public NativeWindowSnapshot? TryGetWindow(NativeWindowHandle handle) => values.SingleOrDefault(window => window.Identity.Handle == handle);
    }

    private sealed class StubIdentityReader(ProcessIdentity identity) : IDouyinProcessIdentityReader
    {
        public ValueTask<ProcessIdentity?> TryReadAsync(int processId, CancellationToken cancellationToken) => ValueTask.FromResult<ProcessIdentity?>(processId == identity.ProcessId ? identity : null);
    }
}
