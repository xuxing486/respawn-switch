using RespawnSwitch.Application.Windows;
using RespawnSwitch.Windows.Identity;

namespace RespawnSwitch.Windows.Tests.Identity;

public sealed class WindowIdentityVerifierTests
{
    [Fact]
    public void Matches_rejects_a_reused_handle_when_its_window_class_changes()
    {
        var verifier = new WindowIdentityVerifier(new StubWindowSnapshotSource(new NativeWindowSnapshot(
            new WindowIdentity(new NativeWindowHandle(42), 401, "DifferentWindowClass"), true, true, false,
            new PixelRect(10, 10, 210, 110), new PixelRect(10, 10, 210, 110),
            new PixelRect(10, 10, 210, 110), new PixelRect(0, 0, 1920, 1080), 0)));

        var expected = new WindowIdentity(new NativeWindowHandle(42), 401, "ExpectedWindowClass");

        Assert.False(verifier.Matches(expected));
    }

    [Fact]
    public void Matches_rejects_a_reused_handle_when_its_process_id_changes()
    {
        var verifier = new WindowIdentityVerifier(new StubWindowSnapshotSource(new NativeWindowSnapshot(
            new WindowIdentity(new NativeWindowHandle(42), 402, "ExpectedWindowClass"), true, true, false,
            new PixelRect(10, 10, 210, 110), new PixelRect(10, 10, 210, 110),
            new PixelRect(10, 10, 210, 110), new PixelRect(0, 0, 1920, 1080), 0)));

        var expected = new WindowIdentity(new NativeWindowHandle(42), 401, "ExpectedWindowClass");

        Assert.False(verifier.Matches(expected));
    }

    private sealed class StubWindowSnapshotSource(params NativeWindowSnapshot[] windows) : IWindowSnapshotSource
    {
        public IReadOnlyList<NativeWindowSnapshot> EnumerateTopLevelWindows() => windows;
        public NativeWindowSnapshot? TryGetWindow(NativeWindowHandle handle) => windows.SingleOrDefault(window => window.Identity.Handle == handle);
    }
}
