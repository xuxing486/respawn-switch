using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace RespawnSwitch.Windows.Interop;

internal static class Kernel32Toolhelp
{
    internal const uint Th32csSnapProcess = 0x00000002;

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern SafeToolhelpSnapshotHandle CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Process32FirstW(SafeToolhelpSnapshotHandle snapshot, ref PROCESSENTRY32W entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Process32NextW(SafeToolhelpSnapshotHandle snapshot, ref PROCESSENTRY32W entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(nint handle);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct PROCESSENTRY32W
    {
        internal uint dwSize;
        internal uint cntUsage;
        internal uint th32ProcessID;
        internal nint th32DefaultHeapID;
        internal uint th32ModuleID;
        internal uint cntThreads;
        internal uint th32ParentProcessID;
        internal int pcPriClassBase;
        internal uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] internal string szExeFile;

        internal static PROCESSENTRY32W Create() => new() { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32W>(), szExeFile = string.Empty };
    }

    internal sealed class SafeToolhelpSnapshotHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeToolhelpSnapshotHandle() : base(true) { }
        protected override bool ReleaseHandle() => CloseHandle(handle);
    }
}
