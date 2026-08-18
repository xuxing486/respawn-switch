using RespawnSwitch.Application.Windows;
using RespawnSwitch.Windows.Interop;

namespace RespawnSwitch.Windows.Identity;

public sealed class NativeWindowSnapshotSource : IWindowSnapshotSource
{
    public IReadOnlyList<NativeWindowSnapshot> EnumerateTopLevelWindows()
    {
        var handles = new List<nint>();
        _ = User32.EnumWindows((handle, _) => { handles.Add(handle); return true; }, 0);
        return handles.Select(Create).Where(snapshot => snapshot is not null).Cast<NativeWindowSnapshot>().ToArray();
    }

    public NativeWindowSnapshot? TryGetWindow(NativeWindowHandle handle) => handle.IsNull ? null : Create(handle.Value);

    private static NativeWindowSnapshot? Create(nint handle)
    {
        if (!User32.IsWindow(handle) || !User32.GetWindowRect(handle, out var windowRect))
        {
            return null;
        }

        _ = User32.GetWindowThreadProcessId(handle, out var processId);
        var className = new char[256];
        var length = User32.GetClassNameW(handle, className, className.Length);
        if (processId == 0 || length == 0)
        {
            return null;
        }

        var style = User32.GetWindowLongPtr(handle, User32.GwlStyle);
        var exStyle = User32.GetWindowLongPtr(handle, User32.GwlExStyle);
        var clientRect = GetClientBounds(handle, windowRect);
        var frameRect = GetExtendedFrameBounds(handle, windowRect);
        var monitorRect = GetMonitorBounds(handle, windowRect);
        var restoreRect = GetRestoreBounds(handle, windowRect);
        return new NativeWindowSnapshot(
            new WindowIdentity(new NativeWindowHandle(handle), unchecked((int)processId), new string(className, 0, length)),
            User32.GetWindow(handle, User32.GwOwner) == 0,
            User32.IsWindowVisible(handle),
            (exStyle & User32.WsExToolWindow) != 0,
            ToPixelRect(windowRect), clientRect, frameRect, monitorRect, style, exStyle, User32.IsIconic(handle), restoreRect,
            User32.GetForegroundWindow() == handle);
    }

    private static PixelRect GetClientBounds(nint handle, User32.RECT fallback)
    {
        if (!User32.GetClientRect(handle, out var client)) return ToPixelRect(fallback);
        var origin = new User32.POINT { X = client.Left, Y = client.Top };
        var opposite = new User32.POINT { X = client.Right, Y = client.Bottom };
        return User32.ClientToScreen(handle, ref origin) && User32.ClientToScreen(handle, ref opposite)
            ? new PixelRect(origin.X, origin.Y, opposite.X, opposite.Y)
            : ToPixelRect(fallback);
    }

    private static PixelRect GetExtendedFrameBounds(nint handle, User32.RECT fallback) =>
        DwmApi.DwmGetWindowAttribute(handle, DwmApi.DwmwaExtendedFrameBounds, out var frame, (uint)System.Runtime.InteropServices.Marshal.SizeOf<User32.RECT>()) == 0
            ? ToPixelRect(frame) : ToPixelRect(fallback);

    private static PixelRect GetMonitorBounds(nint handle, User32.RECT fallback)
    {
        var monitor = User32.MonitorFromWindow(handle, User32.MonitorDefaultToNearest);
        var info = new User32.MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<User32.MONITORINFO>() };
        return monitor != 0 && User32.GetMonitorInfoW(monitor, ref info) ? ToPixelRect(info.rcMonitor) : ToPixelRect(fallback);
    }

    private static PixelRect GetRestoreBounds(nint handle, User32.RECT fallback)
    {
        var placement = new User32.WINDOWPLACEMENT { length = (uint)System.Runtime.InteropServices.Marshal.SizeOf<User32.WINDOWPLACEMENT>() };
        return User32.GetWindowPlacement(handle, ref placement) ? ToPixelRect(placement.rcNormalPosition) : ToPixelRect(fallback);
    }

    private static PixelRect ToPixelRect(User32.RECT value) => new(value.Left, value.Top, value.Right, value.Bottom);
}
