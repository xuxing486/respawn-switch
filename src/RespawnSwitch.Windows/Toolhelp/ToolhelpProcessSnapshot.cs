using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace RespawnSwitch.Windows.Identity;

public sealed record ToolhelpProcessEntry(int ProcessId, string ExecutableName);

public interface IToolhelpProcessSnapshot
{
    IReadOnlyList<ToolhelpProcessEntry> EnumerateProcesses();
}

public sealed class ToolhelpProcessSnapshot : IToolhelpProcessSnapshot
{
    public IReadOnlyList<ToolhelpProcessEntry> EnumerateProcesses()
    {
        using var snapshot = Interop.Kernel32Toolhelp.CreateToolhelp32Snapshot(Interop.Kernel32Toolhelp.Th32csSnapProcess, 0);
        if (snapshot.IsInvalid)
        {
            return [];
        }

        var entry = Interop.Kernel32Toolhelp.PROCESSENTRY32W.Create();
        var values = new List<ToolhelpProcessEntry>();
        if (!Interop.Kernel32Toolhelp.Process32FirstW(snapshot, ref entry))
        {
            return values;
        }

        do
        {
            values.Add(new ToolhelpProcessEntry(unchecked((int)entry.th32ProcessID), entry.szExeFile));
            entry.dwSize = (uint)Marshal.SizeOf<Interop.Kernel32Toolhelp.PROCESSENTRY32W>();
        }
        while (Interop.Kernel32Toolhelp.Process32NextW(snapshot, ref entry));

        return values;
    }
}
