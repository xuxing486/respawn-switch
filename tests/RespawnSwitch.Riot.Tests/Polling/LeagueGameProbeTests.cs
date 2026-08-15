using RespawnSwitch.Application.Monitoring;
using RespawnSwitch.Core.Clock;
using RespawnSwitch.Core.Game;
using RespawnSwitch.Riot.Http;
using RespawnSwitch.Riot.Polling;
using RespawnSwitch.Riot.Tests.Parsing;
using RespawnSwitch.Riot.Tests.TestHttp;

namespace RespawnSwitch.Riot.Tests.Polling;

public sealed class LeagueGameProbeTests
{
    [Fact]
    public async Task SampleOnce_UsesExactActiveRiotIdAndAvoidsFallbackOnValidPrimaryData()
    {
        var handler = Handler(playerList: Fixture.Read("playerlist-dead.json"));
        var probe = CreateProbe(handler);

        var observation = await probe.SampleOnceAsync(CancellationToken.None);

        var sample = Assert.IsType<LeagueSampleObserved>(observation).Sample;
        Assert.Equal("Player#NA1", sample.RiotId);
        Assert.True(sample.IsDead);
        Assert.Equal(SchemaSource.PlayerList, sample.SchemaSource);
        Assert.Equal(0, handler.Count("/liveclientdata/allgamedata"));
    }

    [Fact]
    public async Task SampleOnce_MakesOneBoundedAllGameDataAttemptForIncompatiblePlayerList()
    {
        var handler = Handler(playerList: "{}");
        var probe = CreateProbe(handler);

        var observation = await probe.SampleOnceAsync(CancellationToken.None);

        var sample = Assert.IsType<LeagueSampleObserved>(observation).Sample;
        Assert.Equal(SchemaSource.AllGameData, sample.SchemaSource);
        Assert.Equal(1, handler.Count("/liveclientdata/allgamedata"));
    }

    [Fact]
    public async Task SampleOnce_ConvertsSchemaFailureToTypedFailureNeverAlive()
    {
        var handler = Handler(playerList: "{}", allGameData: "{}");
        var probe = CreateProbe(handler);

        var observation = await probe.SampleOnceAsync(CancellationToken.None);

        var failure = Assert.IsType<LeagueProbeFailed>(observation).Failure;
        Assert.Equal(ProbeFailureKind.SchemaChanged, failure.Kind);
        Assert.Equal(1, handler.Count("/liveclientdata/allgamedata"));
    }

    private static LeagueGameProbe CreateProbe(RouteHttpMessageHandler handler)
    {
        var timeout = TimeSpan.FromSeconds(1);
        var api = new RiotLiveClientApi(
            new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan },
            new RiotRequestTimeouts(timeout, timeout, timeout, timeout, timeout));
        return new LeagueGameProbe(
            api,
            new RespawnTimerSemantics(TimerSemanticStatus.Unverified, "mvp", 1, "none"),
            new LeaguePollingSchedule(TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)),
            TimeProvider.System);
    }

    private static RouteHttpMessageHandler Handler(string playerList, string? allGameData = null) =>
        new((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath switch
        {
            "/liveclientdata/activeplayername" => RouteHttpMessageHandler.Json("\"Player#NA1\""),
            "/liveclientdata/playerlist" => RouteHttpMessageHandler.Json(playerList),
            "/liveclientdata/gamestats" => RouteHttpMessageHandler.Json(Fixture.Read("gamestats-valid.json")),
            "/liveclientdata/allgamedata" => RouteHttpMessageHandler.Json(allGameData ?? Fixture.Read("allgamedata-compatible.json")),
            _ => RouteHttpMessageHandler.Json("{}")
        }));
}
