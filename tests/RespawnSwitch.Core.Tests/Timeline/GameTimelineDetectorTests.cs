using RespawnSwitch.Core.Game;
using RespawnSwitch.Core.Timeline;

namespace RespawnSwitch.Core.Tests.Timeline;

public sealed class GameTimelineDetectorTests
{
    [Fact]
    public void Observe_DetectsIdentityTimelineAndLargeGameTimeResets()
    {
        var detector = new GameTimelineDetector(rollbackThresholdSeconds: 10);

        Assert.Equal(GameTimelineDecisionKind.FirstObservation, detector.Observe(Sample(100, "A#1", "one")).Kind);
        Assert.Equal(GameTimelineDecisionKind.Continue, detector.Observe(Sample(91, "A#1", "one")).Kind);
        Assert.Equal(GameTimelineDecisionKind.ResetForGameTimeRollback, detector.Observe(Sample(80, "A#1", "one")).Kind);
        Assert.Equal(GameTimelineDecisionKind.ResetForRiotId, detector.Observe(Sample(81, "B#1", "one")).Kind);
        Assert.Equal(GameTimelineDecisionKind.ResetForTimelineKey, detector.Observe(Sample(82, "B#1", "two")).Kind);
    }

    [Fact]
    public void Reset_MakesNextSampleFirstObservation()
    {
        var detector = new GameTimelineDetector(10);
        detector.Observe(Sample(100, "A#1", "one"));

        detector.Reset();

        Assert.Equal(GameTimelineDecisionKind.FirstObservation, detector.Observe(Sample(1, "A#1", "one")).Kind);
    }

    private static GameSample Sample(double gameTime, string riotId, string timelineKey) =>
        new(1, 1, riotId, false, 0, null, gameTime, "CLASSIC", SchemaSource.PlayerList, timelineKey);
}
