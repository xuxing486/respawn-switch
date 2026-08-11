using RespawnSwitch.Application.Windows;
using RespawnSwitch.Windows.Identity;

namespace RespawnSwitch.Windows.Windows;

public sealed record NativeWindowState(PixelRect Bounds, bool IsVisible, bool IsTopmost, nint Style);

public sealed class WindowStateReader(IWindowSnapshotSource windows)
{
    public NativeWindowState? TryRead(NativeWindowHandle handle)
    {
        var window = windows.TryGetWindow(handle);
        return window is null ? null : new NativeWindowState(window.ExtendedFrameBounds, window.IsVisible, false, window.Style);
    }
}
