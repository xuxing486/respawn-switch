using System.Diagnostics;
using RespawnSwitch.Application.Windows;
using RespawnSwitch.Core.Respawn;
using RespawnSwitch.Windows.Identity;
using RespawnSwitch.Windows.Interop;

namespace RespawnSwitch.Windows.Windows;

public sealed class MvpDouyinWindowController(IWindowSnapshotSource windows, DouyinWindowLocator locator) : IDouyinWindowController
{
    private sealed record SavedWindow(NativeWindowHandle Handle, PixelRect Bounds, bool Visible, nint Style);
    private readonly Dictionary<RespawnCycleId, SavedWindow> saved = new();

    public async ValueTask<WindowOperationResult> AttachAsync(WindowAttachRequest request, CancellationToken cancellationToken)
    {
        var target = await locator.TryFindAsync(request.CalibratedExecutablePath, request.CalibratedWindowClass, cancellationToken);
        if (target is null && request.AllowLaunch && string.Equals(DouyinProcessIdentityReader.NormalizePath(request.CalibratedExecutablePath), "d:\\douyin\\douyin.exe", StringComparison.OrdinalIgnoreCase))
        {
            _ = Process.Start(new ProcessStartInfo(request.CalibratedExecutablePath) { UseShellExecute = true });
            return new WindowOperationResult(true, false, null, "launching");
        }
        if (target is null) return new WindowOperationResult(false, false, null, "not-found");

        var snapshot = windows.TryGetWindow(target.Identity.Handle);
        if (snapshot is null) return new WindowOperationResult(false, false, target.Identity.Handle, "window-gone");
        saved[request.CycleId] = new SavedWindow(target.Identity.Handle, snapshot.ExtendedFrameBounds, snapshot.IsVisible, snapshot.Style);
        var bounds = DouyinWindowPlacement.PlaceOnRight(request.TargetWorkArea, snapshot.ExtendedFrameBounds);
        var ok = User32.ShowWindowAsync(target.Identity.Handle.Value, User32.SwShownoactivate) &&
                 User32.SetWindowPos(target.Identity.Handle.Value, User32.HwndNoTopmost, bounds.Left, bounds.Top, bounds.Width, bounds.Height, User32.SwpNoActivate | User32.SwpShowWindow);
        return new WindowOperationResult(ok, ok, target.Identity.Handle, ok ? "" : "move-failed");
    }

    public ValueTask<WindowOperationResult> RestoreAsync(RespawnCycleId cycleId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!saved.Remove(cycleId, out var initial)) return ValueTask.FromResult(new WindowOperationResult(false, false, null, "no-saved-state"));
        var current = windows.TryGetWindow(initial.Handle);
        if (current is null) return ValueTask.FromResult(new WindowOperationResult(false, false, initial.Handle, "window-gone"));
        _ = User32.SetWindowLongPtr(initial.Handle.Value, User32.GwlStyle, initial.Style);
        var ok = User32.SetWindowPos(initial.Handle.Value, User32.HwndNoTopmost, initial.Bounds.Left, initial.Bounds.Top, initial.Bounds.Width, initial.Bounds.Height, User32.SwpNoActivate | (initial.Visible ? User32.SwpShowWindow : 0));
        if (!initial.Visible) ok &= User32.ShowWindowAsync(initial.Handle.Value, User32.SwHide);
        return ValueTask.FromResult(new WindowOperationResult(ok, ok, initial.Handle, ok ? "" : "restore-failed"));
    }
}
