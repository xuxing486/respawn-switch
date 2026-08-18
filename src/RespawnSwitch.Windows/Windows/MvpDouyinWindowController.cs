using RespawnSwitch.Application.Windows;
using RespawnSwitch.Core.Respawn;
using RespawnSwitch.Windows.Identity;
using RespawnSwitch.Windows.Interop;

namespace RespawnSwitch.Windows.Windows;

public sealed class MvpDouyinWindowController(IWindowSnapshotSource windows, DouyinWindowLocator locator) : IDouyinWindowController
{
    private sealed record SavedWindow(NativeWindowHandle Handle, PixelRect Bounds, bool Visible, bool Minimized, nint Style, nint ExtendedStyle);
    private readonly Dictionary<RespawnCycleId, SavedWindow> saved = new();

    public async ValueTask<WindowOperationResult> AttachAsync(WindowAttachRequest request, CancellationToken cancellationToken)
    {
        var target = await locator.TryFindAsync(request.CalibratedExecutablePath, request.CalibratedWindowClass, cancellationToken);
        if (target is null) return new WindowOperationResult(false, false, null, "not-found");

        var snapshot = windows.TryGetWindow(target.Identity.Handle);
        if (snapshot is null) return new WindowOperationResult(false, false, target.Identity.Handle, "window-gone");
        var restoreBounds = snapshot.RestoreBounds.Width > 0 && snapshot.RestoreBounds.Height > 0 ? snapshot.RestoreBounds : snapshot.Bounds;
        saved[request.CycleId] = new SavedWindow(target.Identity.Handle, restoreBounds, snapshot.IsVisible,
            snapshot.IsMinimized, snapshot.Style, snapshot.ExtendedStyle);
        var sourceBounds = snapshot.IsMinimized ? restoreBounds : snapshot.Bounds;
        var bounds = DouyinWindowPlacement.PlaceOnRight(request.TargetWorkArea, sourceBounds);
        _ = User32.ShowWindowAsync(target.Identity.Handle.Value, User32.SwRestore);
        if (snapshot.IsMinimized) await WaitUntilAsync(target.Identity.Handle, current => !current.IsMinimized, TimeSpan.FromMilliseconds(250), cancellationToken);
        else await Task.Delay(30, cancellationToken);
        _ = WindowActivation.TryActivate(target.Identity.Handle.Value);
        var issued = false;
        var verified = false;
        for (var attempt = 0; attempt < 3 && !verified; attempt++)
        {
            _ = User32.SetWindowLongPtr(target.Identity.Handle.Value, User32.GwlExStyle, snapshot.ExtendedStyle | User32.WsExTopmost);
            issued |= User32.SetWindowPos(target.Identity.Handle.Value, User32.HwndTopmost, bounds.Left, bounds.Top, bounds.Width, bounds.Height,
                User32.SwpNoActivate | User32.SwpShowWindow);
            if (attempt == 0 && issued) _ = WindowActivation.TryActivate(target.Identity.Handle.Value);
            if (issued) verified = await WaitUntilAsync(target.Identity.Handle,
                current => DouyinWindowPostcondition.IsAttached(current, bounds), TimeSpan.FromMilliseconds(120), cancellationToken);
        }
        if (!verified)
        {
            _ = await RestoreAsync(request.CycleId, CancellationToken.None);
        }
        return new WindowOperationResult(issued, verified, target.Identity.Handle, verified ? "" : issued ? "topmost-not-confirmed" : "move-failed");
    }

    public ValueTask<WindowOperationResult> RestoreAsync(RespawnCycleId cycleId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!saved.Remove(cycleId, out var initial)) return ValueTask.FromResult(new WindowOperationResult(false, false, null, "no-saved-state"));
        var current = windows.TryGetWindow(initial.Handle);
        if (current is null) return ValueTask.FromResult(new WindowOperationResult(false, false, initial.Handle, "window-gone"));
        _ = User32.SetWindowLongPtr(initial.Handle.Value, User32.GwlStyle, initial.Style);
        _ = User32.SetWindowLongPtr(initial.Handle.Value, User32.GwlExStyle, initial.ExtendedStyle);
        var insertAfter = (initial.ExtendedStyle & User32.WsExTopmost) != 0 ? User32.HwndTopmost : User32.HwndNoTopmost;
        var ok = User32.SetWindowPos(initial.Handle.Value, insertAfter, initial.Bounds.Left, initial.Bounds.Top, initial.Bounds.Width, initial.Bounds.Height,
            User32.SwpNoActivate | (initial.Visible ? User32.SwpShowWindow : 0));
        if (initial.Minimized) ok &= User32.ShowWindowAsync(initial.Handle.Value, User32.SwMinimize);
        else if (!initial.Visible) ok &= User32.ShowWindowAsync(initial.Handle.Value, User32.SwHide);
        return ValueTask.FromResult(new WindowOperationResult(ok, ok, initial.Handle, ok ? "" : "restore-failed"));
    }

    private async Task<bool> WaitUntilAsync(NativeWindowHandle handle, Func<NativeWindowSnapshot, bool> predicate, TimeSpan timeout, CancellationToken token)
    {
        var until = DateTime.UtcNow + timeout;
        do
        {
            token.ThrowIfCancellationRequested();
            var current = windows.TryGetWindow(handle);
            if (current is not null && predicate(current)) return true;
            await Task.Delay(15, token);
        } while (DateTime.UtcNow < until);
        return false;
    }
}
