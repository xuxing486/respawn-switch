namespace RespawnSwitch.Core.Clock;

public enum TimerSemanticStatus
{
    Unverified,
    VerifiedForCurrentPatch
}

public sealed record RespawnTimerSemantics(
    TimerSemanticStatus Status,
    string PatchLabel,
    double SecondsPerRawUnit,
    string EvidenceReportId);
