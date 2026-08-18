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

    [Fact]
    public void Constructor_PreservesChampionAndKdaForOverlay()
    {
        var sample = new GameSample(
            8, 20_000, "Player#NA1", true, 12.2, 12.2, 800, "CLASSIC",
            SchemaSource.PlayerList, "timeline", "Ahri", 7, 3, 9);

        Assert.Equal("Ahri", sample.ChampionName);
        Assert.Equal((7, 3, 9), (sample.Kills, sample.Deaths, sample.Assists));
    }
}
