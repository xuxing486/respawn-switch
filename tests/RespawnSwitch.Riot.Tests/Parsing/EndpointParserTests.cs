using RespawnSwitch.Riot.Parsing;

namespace RespawnSwitch.Riot.Tests.Parsing;

public sealed class EndpointParserTests
{
    [Fact]
    public void ActivePlayer_ParsesExactRiotId()
    {
        var result = ActivePlayerParser.Parse("{\"riotId\":\"Player#NA1\"}");

        Assert.True(result.IsSuccess);
        Assert.Equal("Player#NA1", result.Value!.RiotId);
    }

    [Fact]
    public void ActivePlayerName_ParsesTheDocumentedJsonStringShape()
    {
        var result = ActivePlayerParser.Parse("\"Player#NA1\"");

        Assert.True(result.IsSuccess);
        Assert.Equal("Player#NA1", result.Value!.RiotId);
    }

    [Fact]
    public void ActivePlayer_RejectsWrongCaseAndNonStringRiotId()
    {
        var wrongCase = ActivePlayerParser.Parse("{\"riotid\":\"Player#NA1\"}");
        var wrongType = ActivePlayerParser.Parse("{\"riotId\":4}");

        Assert.Contains(wrongCase.Errors, error => error.Code == "riot.schema.activeplayer.riotid-missing");
        Assert.Contains(wrongType.Errors, error => error.Code == "riot.schema.activeplayer.riotid-invalid-type");
    }

    [Fact]
    public void GameStats_ParsesFiniteNumericTimeAndExactCaseProperties()
    {
        var result = GameStatsParser.Parse(Fixture.Read("gamestats-valid.json"));

        Assert.True(result.IsSuccess);
        Assert.Equal(123.5, result.Value!.GameTimeSeconds);
        Assert.Equal("CLASSIC", result.Value.GameMode);
    }

    [Fact]
    public void GameStats_RejectsStringAndNonFiniteGameTime()
    {
        var stringTime = GameStatsParser.Parse("{\"gameTime\":\"123\",\"gameMode\":\"CLASSIC\"}");
        var nonFiniteTime = GameStatsParser.Parse("{\"gameTime\":1e999,\"gameMode\":\"CLASSIC\"}");

        Assert.Contains(stringTime.Errors, error => error.Code == "riot.schema.gamestats.gametime-invalid-type");
        Assert.Contains(nonFiniteTime.Errors, error => error.Code == "riot.schema.gamestats.gametime-nonfinite");
    }

    [Fact]
    public void AllGameData_ParsesTheExactPlayerAndCompatibleGameStats()
    {
        var result = AllGameDataParser.Parse(Fixture.Read("allgamedata-compatible.json"), "Player#NA1");

        Assert.True(result.IsSuccess);
        Assert.Equal("Player#NA1", result.Value!.Player.RiotId);
        Assert.True(result.Value.Player.IsDead);
        Assert.Equal(18.75, result.Value.Player.RespawnTimerRaw);
        Assert.Equal(321.25, result.Value.Stats.GameTimeSeconds);
        Assert.Equal("ARAM", result.Value.Stats.GameMode);
    }
}
