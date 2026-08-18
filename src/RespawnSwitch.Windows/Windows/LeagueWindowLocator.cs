using RespawnSwitch.Application.Windows;
using RespawnSwitch.Windows.Identity;
using RespawnSwitch.Windows.Interop;

namespace RespawnSwitch.Windows.Windows;

public sealed class LeagueWindowLocator(IWindowSnapshotSource windows, IToolhelpProcessSnapshot processes) : ILeagueWindowController
{
    private const nint WsCaption = 0x00C00000;

    public ValueTask<GameWindowTarget?> TryFindAsync(string timelineKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var gameProcessIds = processes.EnumerateProcesses()
            .Where(process => string.Equals(process.ExecutableName, "League of Legends.exe", StringComparison.OrdinalIgnoreCase))
            .Select(process => process.ProcessId)
            .ToHashSet();

        var candidates = windows.EnumerateTopLevelWindows()
            .Where(window => gameProcessIds.Contains(window.Identity.ProcessId))
            .Where(IsEligible)
            .Select(window => new GameWindowTarget(window.Identity, window.ExtendedFrameBounds, "League of Legends.exe", timelineKey, IsBorderless(window)))
            .ToArray();

        return ValueTask.FromResult<GameWindowTarget?>(candidates.Length == 1 ? candidates[0] : null);
    }

    public ValueTask<bool> TryRestoreFocusOnceAsync(GameWindowTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (target.Identity.Handle.IsNull) return ValueTask.FromResult(false);
        var handle = target.Identity.Handle.Value;
        if (User32.GetForegroundWindow() == handle) return ValueTask.FromResult(true);
        _ = User32.ShowWindowAsync(handle, User32.SwShownoactivate);
        var requested = User32.SetForegroundWindow(handle);
        return ValueTask.FromResult(requested && User32.GetForegroundWindow() == handle);
    }
    public ValueTask FlashTaskbarAsync(GameWindowTarget target, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    private static bool IsEligible(NativeWindowSnapshot window) =>
        window.IsTopLevel && window.IsVisible && !window.IsToolWindow && !window.Identity.Handle.IsNull &&
        window.ExtendedFrameBounds.Width > 0 && window.ExtendedFrameBounds.Height > 0 && IsBorderless(window);

    private static bool IsBorderless(NativeWindowSnapshot window) =>
        (window.Style & WsCaption) == 0 && IsClientFrameAligned(window.ClientBounds, window.ExtendedFrameBounds);

    private static bool IsClientFrameAligned(PixelRect client, PixelRect frame) =>
        Math.Abs(client.Left - frame.Left) <= 16 && Math.Abs(client.Top - frame.Top) <= 16 &&
        Math.Abs(client.Right - frame.Right) <= 16 && Math.Abs(client.Bottom - frame.Bottom) <= 16;
}
