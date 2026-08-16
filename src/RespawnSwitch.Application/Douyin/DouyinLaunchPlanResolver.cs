using RespawnSwitch.Core.Respawn;

namespace RespawnSwitch.Application.Douyin;

public enum DouyinLaunchMode
{
    Desktop,
    Web,
    Unavailable
}

public sealed record DouyinRuntimePreferences(
    DouyinDiscoveryMode DiscoveryMode,
    bool OpenWebFallback);

public sealed record DouyinLaunchPlan(
    DouyinLaunchMode Mode,
    DouyinCandidate? DesktopCandidate,
    Uri? WebUri);

public interface IDouyinWebFallbackLauncher
{
    Task<bool> OpenAsync(CancellationToken cancellationToken);
}

public static class DouyinLaunchPlanResolver
{
    public static Uri OfficialWebUri { get; } = new("https://www.douyin.com/");

    public static DouyinLaunchPlan Resolve(
        DouyinDiscoveryResult discovery,
        DouyinRuntimePreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(preferences);

        if (preferences.DiscoveryMode != DouyinDiscoveryMode.WebOnly &&
            discovery.Status == DouyinDiscoveryStatus.Found &&
            discovery.Selected is not null)
        {
            return new(DouyinLaunchMode.Desktop, discovery.Selected, null);
        }

        return preferences.OpenWebFallback || preferences.DiscoveryMode == DouyinDiscoveryMode.WebOnly
            ? new(DouyinLaunchMode.Web, null, OfficialWebUri)
            : new(DouyinLaunchMode.Unavailable, null, null);
    }
}

public sealed class WebFallbackCycleGuard
{
    private readonly object syncRoot = new();
    private readonly HashSet<RespawnCycleId> activeCycles = [];

    public bool TryBegin(RespawnCycleId cycleId)
    {
        lock (syncRoot)
        {
            return activeCycles.Add(cycleId);
        }
    }

    public void Complete(RespawnCycleId cycleId)
    {
        lock (syncRoot)
        {
            activeCycles.Remove(cycleId);
        }
    }
}
