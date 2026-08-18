using RespawnSwitch.Windows.Interop;

namespace RespawnSwitch.Windows.Windows;

internal static class WindowActivation
{
    internal static bool TryActivate(nint target)
    {
        if (target == 0 || !User32.IsWindow(target)) return false;
        var foreground = User32.GetForegroundWindow();
        if (foreground == target) return true;
        var currentThread = User32.GetCurrentThreadId();
        var foregroundThread = foreground == 0 ? 0 : User32.GetWindowThreadProcessId(foreground, out _);
        var attached = foregroundThread != 0 && foregroundThread != currentThread &&
            User32.AttachThreadInput(currentThread, foregroundThread, true);
        try
        {
            _ = User32.BringWindowToTop(target);
            _ = User32.SetForegroundWindow(target);
            return User32.GetForegroundWindow() == target;
        }
        finally
        {
            if (attached) _ = User32.AttachThreadInput(currentThread, foregroundThread, false);
        }
    }
}
