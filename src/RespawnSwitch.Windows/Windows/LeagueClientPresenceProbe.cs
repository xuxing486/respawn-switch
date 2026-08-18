using RespawnSwitch.Windows.Identity;

namespace RespawnSwitch.Windows.Windows;

public sealed record LeagueClientPresence(bool IsReady, string Code);

public sealed class LeagueClientPresenceProbe(IWindowSnapshotSource windows, IToolhelpProcessSnapshot processes)
{
    public LeagueClientPresence Probe()
    {
        var ids = processes.EnumerateProcesses()
            .Where(x => string.Equals(x.ExecutableName, "LeagueClientUx.exe", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.ProcessId)
            .ToHashSet();
        var ready = windows.EnumerateTopLevelWindows().Any(x => ids.Contains(x.Identity.ProcessId) && x.IsTopLevel && x.IsVisible && !x.IsToolWindow);
        return ready
            ? new(true, "league.client.ready")
            : new(false, "league.client.not-running");
    }
}
