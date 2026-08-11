using RespawnSwitch.Application.Windows;
using RespawnSwitch.Windows.Identity;

namespace RespawnSwitch.Windows.Windows;

public sealed class DouyinWindowLocator(IWindowSnapshotSource windows, IDouyinProcessIdentityReader identities)
{
    public async ValueTask<DouyinWindowTarget?> TryFindAsync(string calibratedExecutablePath, string calibratedWindowClass, CancellationToken cancellationToken)
    {
        var expectedPath = DouyinProcessIdentityReader.NormalizePath(calibratedExecutablePath);
        if (expectedPath is null || string.IsNullOrWhiteSpace(calibratedWindowClass))
        {
            return null;
        }

        var candidates = new List<DouyinWindowTarget>();
        foreach (var window in windows.EnumerateTopLevelWindows().Where(IsEligible).Where(window => string.Equals(window.Identity.WindowClass, calibratedWindowClass, StringComparison.Ordinal)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var identity = await identities.TryReadAsync(window.Identity.ProcessId, cancellationToken);
            if (identity is not null && string.Equals(DouyinProcessIdentityReader.NormalizePath(identity.NormalizedExecutablePath), expectedPath, StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(new DouyinWindowTarget(window.Identity, identity, window.ExtendedFrameBounds));
            }
        }

        return candidates.Count == 1 ? candidates[0] : null;
    }

    private static bool IsEligible(NativeWindowSnapshot window) =>
        window.IsTopLevel && window.IsVisible && !window.IsToolWindow && !window.Identity.Handle.IsNull &&
        window.ExtendedFrameBounds.Width > 0 && window.ExtendedFrameBounds.Height > 0;
}
