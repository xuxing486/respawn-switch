using System.Runtime.InteropServices;

namespace RespawnSwitch.Windows.Interop;

internal static class DwmApi
{
    internal const uint DwmwaExtendedFrameBounds = 9;

    [DllImport("dwmapi.dll", SetLastError = true)]
    internal static extern int DwmGetWindowAttribute(nint hWnd, uint attribute, out User32.RECT value, uint valueSize);
}
