using RespawnSwitch.Core.Game;

namespace RespawnSwitch.Core.Timeline;

public sealed class GameTimelineDetector
{
    private readonly double _rollbackThresholdSeconds;
    private GameSample? _previous;

    public GameTimelineDetector(double rollbackThresholdSeconds)
    {
        if (!double.IsFinite(rollbackThresholdSeconds) || rollbackThresholdSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rollbackThresholdSeconds));
        }

        _rollbackThresholdSeconds = rollbackThresholdSeconds;
    }

    public GameTimelineDecision Observe(GameSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        var kind = _previous switch
        {
            null => GameTimelineDecisionKind.FirstObservation,
            { } previous when !StringComparer.Ordinal.Equals(previous.RiotId, sample.RiotId)
                => GameTimelineDecisionKind.ResetForRiotId,
            { } previous when !StringComparer.Ordinal.Equals(previous.TimelineKey, sample.TimelineKey)
                => GameTimelineDecisionKind.ResetForTimelineKey,
            { } previous when previous.GameTimeSeconds - sample.GameTimeSeconds > _rollbackThresholdSeconds
                => GameTimelineDecisionKind.ResetForGameTimeRollback,
            _ => GameTimelineDecisionKind.Continue
        };

        _previous = sample;
        return new GameTimelineDecision(kind, sample.TimelineKey, Reason(kind));
    }

    public void Reset() => _previous = null;

    private static string Reason(GameTimelineDecisionKind kind) => kind switch
    {
        GameTimelineDecisionKind.FirstObservation => "timeline.first-observation",
        GameTimelineDecisionKind.Continue => "timeline.continue",
        GameTimelineDecisionKind.ResetForRiotId => "timeline.riot-id-changed",
        GameTimelineDecisionKind.ResetForTimelineKey => "timeline.key-changed",
        GameTimelineDecisionKind.ResetForGameTimeRollback => "timeline.game-time-rollback",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
