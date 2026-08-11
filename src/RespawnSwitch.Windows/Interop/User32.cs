using System.Runtime.InteropServices;

namespace RespawnSwitch.Windows.Interop;

internal static class User32
{
    internal const int GwOwner = 4;
    internal const int GwlStyle = -16;
    internal const int GwlExStyle = -20;
    internal const nint WsExToolWindow = 0x00000080;
    internal const nint WsExTopmost = 0x00000008;
    internal const uint MonitorDefaultToNearest = 2;

    internal delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint GetWindow(nint hWnd, uint command);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int GetClassNameW(nint hWnd, [Out] char[] className, int maxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(nint hWnd, out RECT rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetClientRect(nint hWnd, out RECT rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ClientToScreen(nint hWnd, ref POINT point);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static extern nint GetWindowLongPtr(nint hWnd, int index);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint MonitorFromWindow(nint hWnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfoW(nint monitor, ref MONITORINFO monitorInfo);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(nint hWnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindowAsync(nint hWnd, int command);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(nint hWnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(nint hWnd, int id);

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT { internal int X; internal int Y; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT { internal int Left; internal int Top; internal int Right; internal int Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MONITORINFO
    {
        internal uint cbSize;
        internal RECT rcMonitor;
        internal RECT rcWork;
        internal uint dwFlags;
    }
}
