namespace RespawnSwitch.Application.Readiness;

public enum Subsystem { LeagueClient, LeagueGameData, LeagueWindow, DouyinDesktop, DouyinWeb, MediaControl, WindowsFocus, Overlay }
public enum ReadinessLevel { Waiting, Ready, Blocked }

public sealed record ReadinessIssue(Subsystem Subsystem, string Code, string Message, string Action);

public sealed record ComponentReadiness(
    Subsystem Subsystem,
    ReadinessLevel Level,
    string Summary,
    ReadinessIssue? Issue)
{
    public static ComponentReadiness Ready(Subsystem subsystem, string summary) => new(subsystem, ReadinessLevel.Ready, summary, null);
    public static ComponentReadiness Waiting(Subsystem subsystem, string summary) => new(subsystem, ReadinessLevel.Waiting, summary, null);
    public static ComponentReadiness Blocked(Subsystem subsystem, string code, string message, string action) =>
        new(subsystem, ReadinessLevel.Blocked, message, new(subsystem, code, message, action));
}

public sealed record ProductReadiness(
    ReadinessLevel Level,
    IReadOnlyList<ComponentReadiness> Components,
    IReadOnlyList<ReadinessIssue> Issues)
{
    public static ProductReadiness Aggregate(IReadOnlyList<ComponentReadiness> components)
    {
        var issues = components.Where(x => x.Issue is not null).Select(x => x.Issue!).ToArray();
        var level = issues.Length > 0
            ? ReadinessLevel.Blocked
            : components.All(x => x.Level == ReadinessLevel.Ready) ? ReadinessLevel.Ready : ReadinessLevel.Waiting;
        return new(level, components.ToArray(), issues);
    }
}
