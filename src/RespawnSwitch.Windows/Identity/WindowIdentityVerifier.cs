using RespawnSwitch.Application.Windows;

namespace RespawnSwitch.Windows.Identity;

public sealed record NativeWindowSnapshot(
    WindowIdentity Identity,
    bool IsTopLevel,
    bool IsVisible,
    bool IsToolWindow,
    PixelRect Bounds,
    PixelRect ClientBounds,
    PixelRect ExtendedFrameBounds,
    PixelRect MonitorBounds,
    nint Style);

public interface IWindowSnapshotSource
{
    IReadOnlyList<NativeWindowSnapshot> EnumerateTopLevelWindows();
    NativeWindowSnapshot? TryGetWindow(NativeWindowHandle handle);
}

public sealed class WindowIdentityVerifier(IWindowSnapshotSource windows)
{
    public bool Matches(WindowIdentity expected)
    {
        if (expected.Handle.IsNull)
        {
            return false;
        }

        var current = windows.TryGetWindow(expected.Handle);
        return current is not null && current.Identity.ProcessId == expected.ProcessId &&
            string.Equals(current.Identity.WindowClass, expected.WindowClass, StringComparison.Ordinal);
    }

    public bool Matches(WindowIdentity expected, ProcessIdentity expectedProcess, ProcessIdentity currentProcess) =>
        Matches(expected) && expectedProcess.ProcessId == expected.ProcessId && currentProcess.ProcessId == expected.ProcessId &&
        expectedProcess.StartedAtUtc == currentProcess.StartedAtUtc &&
        string.Equals(expectedProcess.NormalizedExecutablePath, currentProcess.NormalizedExecutablePath, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(expectedProcess.SignatureSubject, currentProcess.SignatureSubject, StringComparison.Ordinal) &&
        string.Equals(expectedProcess.SignatureThumbprint, currentProcess.SignatureThumbprint, StringComparison.Ordinal);
}
