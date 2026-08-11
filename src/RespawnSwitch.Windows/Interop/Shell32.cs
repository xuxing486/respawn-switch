using System.Runtime.InteropServices;

namespace RespawnSwitch.Windows.Interop;

internal static class Shell32
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint ShellExecuteW(nint hwnd, string operation, string file, string? parameters, string? directory, int showCommand);
}
