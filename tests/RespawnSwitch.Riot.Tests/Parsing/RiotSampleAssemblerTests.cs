using RespawnSwitch.Core.Clock;
using RespawnSwitch.Core.Game;
using RespawnSwitch.Riot.Parsing;

namespace RespawnSwitch.Riot.Tests.Parsing;

public sealed class RiotSampleAssemblerTests
{
    [Fact]
    public void Assemble_PreservesRawTimerAndLeavesSecondsNullForUnverifiedSemantics()
    {
        var sample = RiotSampleAssembler.Assemble(
            12, 34, new RiotPlayerSnapshot("Player#NA1", true, 18.75, 1),
            new RiotGameStatsSnapshot(123.5, "CLASSIC"), SchemaSource.PlayerList, "match-1",
            new RespawnTimerSemantics(TimerSemanticStatus.Unverified, "14.1", 1, "probe-1"));

        Assert.Equal(18.75, sample.RespawnTimerRaw);
        Assert.Null(sample.RespawnTimerSeconds);
        Assert.Equal(SchemaSource.PlayerList, sample.SchemaSource);
    }

    [Fact]
    public void Assemble_NormalizesTimerOnlyForVerifiedCurrentPatchSemantics()
    {
        var sample = RiotSampleAssembler.Assemble(
            12, 34, new RiotPlayerSnapshot("Player#NA1", true, 18.75, 1),
            new RiotGameStatsSnapshot(123.5, "CLASSIC"), SchemaSource.AllGameData, "match-1",
            new RespawnTimerSemantics(TimerSemanticStatus.VerifiedForCurrentPatch, "14.1", 2, "probe-1"));

        Assert.Equal(18.75, sample.RespawnTimerRaw);
        Assert.Equal(37.5, sample.RespawnTimerSeconds);
        Assert.Equal(SchemaSource.AllGameData, sample.SchemaSource);
    }
}
