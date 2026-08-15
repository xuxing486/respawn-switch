using RespawnSwitch.Application.Windows;
using RespawnSwitch.Windows.Identity;

namespace RespawnSwitch.Windows.Windows;

public sealed record DouyinWindowCandidate(string ExecutablePath, string WindowClass, bool IsVisible, bool IsTopLevel, bool IsToolWindow, NativeWindowHandle Handle);

public static class DouyinWindowCalibration
{
    public static string? SelectUniqueDouyinWindowClass(IEnumerable<DouyinWindowCandidate> candidates, string executablePath)
    {
        var expected = DouyinProcessIdentityReader.NormalizePath(executablePath);
        if (expected is null) return null;
        var classes = candidates.Where(x => x.IsVisible && x.IsTopLevel && !x.IsToolWindow && !x.Handle.IsNull &&
                string.Equals(DouyinProcessIdentityReader.NormalizePath(x.ExecutablePath), expected, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.WindowClass).Take(2).ToArray();
        return classes.Length == 1 ? classes[0] : null;
    }
}
