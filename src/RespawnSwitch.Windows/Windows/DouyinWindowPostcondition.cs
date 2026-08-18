using RespawnSwitch.Application.Windows;
using RespawnSwitch.Windows.Identity;
using RespawnSwitch.Windows.Interop;

namespace RespawnSwitch.Windows.Windows;

public static class DouyinWindowPostcondition
{
    private const int FrameTolerance = 16;

    public static bool IsAttached(NativeWindowSnapshot window, PixelRect desired) =>
        window.IsVisible && !window.IsMinimized &&
        ((window.ExtendedStyle & User32.WsExTopmost) != 0 || window.IsForeground) &&
        Close(window.ExtendedFrameBounds.Left, desired.Left) &&
        Close(window.ExtendedFrameBounds.Top, desired.Top) &&
        Close(window.ExtendedFrameBounds.Right, desired.Right) &&
        Close(window.ExtendedFrameBounds.Bottom, desired.Bottom);

    private static bool Close(int actual, int desired) => Math.Abs(actual - desired) <= FrameTolerance;
}
