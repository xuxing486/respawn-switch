using RespawnSwitch.Riot.Parsing;

namespace RespawnSwitch.Riot.Tests.Parsing;

public sealed class PlayerListParserTests
{
    [Fact]
    public void PlayerList_UsesFullRiotIdAndNeverChampionNameOrArrayPosition()
    {
        var result = PlayerListParser.Parse(Fixture.Read("playerlist-dead.json"), "Player#NA1");

        Assert.True(result.IsSuccess);
        Assert.Equal("Player#NA1", result.Value!.RiotId);
        Assert.True(result.Value.IsDead);
        Assert.Equal(18.75, result.Value.RespawnTimerRaw);
        Assert.Equal(1, result.Value.Deaths);
    }

    [Fact]
    public void PlayerList_MatchesTheFullRiotIdWhenGameNameHasAnotherTag()
    {
        var result = PlayerListParser.Parse(Fixture.Read("playerlist-same-name-different-tag.json"), "Player#EUW");

        Assert.True(result.IsSuccess);
        Assert.Equal("Player#EUW", result.Value!.RiotId);
        Assert.True(result.Value.IsDead);
    }

    [Fact]
    public void PlayerList_DoesNotUseDuplicateChampionNameAsIdentity()
    {
        var result = PlayerListParser.Parse(Fixture.Read("playerlist-duplicate-champion.json"), "Target#NA1");

        Assert.True(result.IsSuccess);
        Assert.Equal("Target#NA1", result.Value!.RiotId);
        Assert.True(result.Value.IsDead);
    }

    [Fact]
    public void PlayerList_ReturnsStructuredFailureWhenTargetIsAbsent()
    {
        var result = PlayerListParser.Parse(Fixture.Read("playerlist-dead.json"), "Missing#NA1");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Code == "riot.schema.playerlist.player-not-found");
    }

    [Fact]
    public void PlayerList_ReturnsStructuredFailureWhenExactRiotIdIsAmbiguous()
    {
        var result = PlayerListParser.Parse(Fixture.Read("playerlist-duplicate-riotid.json"), "Player#NA1");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Code == "riot.schema.playerlist.riotid-ambiguous");
    }

    [Fact]
    public void PlayerList_ReturnsStructuredFailureForMissingIsDead()
    {
        var result = PlayerListParser.Parse(Fixture.Read("playerlist-missing-isdead.json"), "Player#NA1");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Code == "riot.schema.playerlist.isdead-missing" && error.JsonPath == "$[0].isDead");
    }

    [Fact]
    public void PlayerList_DoesNotCoerceBooleanOrNumberStrings()
    {
        var result = PlayerListParser.Parse(Fixture.Read("playerlist-invalid-types.json"), "Player#NA1");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Code == "riot.schema.playerlist.isdead-invalid-type");
        Assert.Contains(result.Errors, error => error.Code == "riot.schema.playerlist.respawntimer-invalid-type");
        Assert.Contains(result.Errors, error => error.Code == "riot.schema.playerlist.deaths-invalid-type");
    }

    [Fact]
    public void PlayerList_PreservesPositiveTimerForAnAlivePlayerWithoutInferringDeath()
    {
        var result = PlayerListParser.Parse(Fixture.Read("playerlist-positive-timer-alive.json"), "Player#NA1");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsDead);
        Assert.Equal(8.5, result.Value.RespawnTimerRaw);
    }

    [Fact]
    public void PlayerList_RejectsIncorrectPropertyCasing()
    {
        const string json = "[{\"riotid\":\"Player#NA1\",\"isDead\":true,\"respawnTimer\":1,\"scores\":{\"deaths\":0}}]";

        var result = PlayerListParser.Parse(json, "Player#NA1");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Code == "riot.schema.playerlist.riotid-missing");
    }
}
