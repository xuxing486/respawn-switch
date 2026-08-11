using RespawnSwitch.Core.Game;

namespace RespawnSwitch.Core.Tests.Game;

public sealed class GameSampleTests
{
    [Fact]
    public void Constructor_PreservesRawAndVerifiedTimerSeparately()
    {
        var sample = new GameSample(
            SampleId: 7,
            ObservedAtTimestamp: 12_345,
            RiotId: "Player#NA1",
            IsDead: true,
            RespawnTimerRaw: 18.75,
            RespawnTimerSeconds: null,
            GameTimeSeconds: 1_234.5,
            GameMode: "PRACTICETOOL",
            SchemaSource: SchemaSource.PlayerList,
            TimelineKey: "42:Player#NA1:0");

        Assert.Equal(18.75, sample.RespawnTimerRaw);
        Assert.Null(sample.RespawnTimerSeconds);
        Assert.True(sample.IsDead);
    }
}
