namespace RespawnSwitch.Core.Timeline;

public enum GameTimelineDecisionKind
{
    FirstObservation,
    Continue,
    ResetForRiotId,
    ResetForTimelineKey,
    ResetForGameTimeRollback
}

public sealed record GameTimelineDecision(
    GameTimelineDecisionKind Kind,
    string TimelineKey,
    string ReasonCode);
