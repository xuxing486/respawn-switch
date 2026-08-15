using System.Runtime.CompilerServices;
using RespawnSwitch.Application.Monitoring;
using RespawnSwitch.Core.Clock;
using RespawnSwitch.Core.Game;
using RespawnSwitch.Riot.Http;
using RespawnSwitch.Riot.Parsing;

namespace RespawnSwitch.Riot.Polling;

public sealed class LeagueGameProbe(RiotLiveClientApi api, RespawnTimerSemantics semantics, LeaguePollingSchedule schedule, TimeProvider timeProvider) : ILeagueGameProbe
{
    private long _sequence;
    private string? _riotId;
    private string _timelineKey = Guid.NewGuid().ToString("N");

    public async ValueTask<LeagueProbeObservation> SampleOnceAsync(CancellationToken cancellationToken)
    {
        var timestamp = timeProvider.GetTimestamp();
        try
        {
            var active = ActivePlayerParser.Parse(await api.GetActivePlayerAsync(cancellationToken).ConfigureAwait(false));
            if (!active.IsSuccess) return Failed(ProbeFailureKind.SchemaChanged, active.Errors[0].Code, timestamp);
            if (!StringComparer.Ordinal.Equals(_riotId, active.Value!.RiotId)) { _riotId = active.Value.RiotId; _timelineKey = Guid.NewGuid().ToString("N"); }
            var players = PlayerListParser.Parse(await api.GetPlayerListAsync(cancellationToken).ConfigureAwait(false), _riotId);
            if (players.IsSuccess)
            {
                var stats = GameStatsParser.Parse(await api.GetGameStatsAsync(cancellationToken).ConfigureAwait(false));
                if (!stats.IsSuccess) return Failed(ProbeFailureKind.SchemaChanged, stats.Errors[0].Code, timestamp);
                return new LeagueSampleObserved(RiotSampleAssembler.Assemble(Interlocked.Increment(ref _sequence), timestamp, players.Value!, stats.Value!, SchemaSource.PlayerList, _timelineKey, semantics));
            }
            var all = AllGameDataParser.Parse(await api.GetAllGameDataAsync(cancellationToken).ConfigureAwait(false), _riotId);
            if (!all.IsSuccess) return Failed(ProbeFailureKind.SchemaChanged, all.Errors[0].Code, timestamp);
            return new LeagueSampleObserved(RiotSampleAssembler.Assemble(Interlocked.Increment(ref _sequence), timestamp, all.Value!.Player, all.Value.Stats, SchemaSource.AllGameData, _timelineKey, semantics));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (RiotApiException ex) { return Failed(ex.Kind, "riot.http", timestamp); }
        catch (Exception) { return Failed(ProbeFailureKind.Unexpected, "riot.unexpected", timestamp); }
    }

    public async IAsyncEnumerable<LeagueProbeObservation> WatchAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            yield return await SampleOnceAsync(cancellationToken).ConfigureAwait(false);
            await Task.Delay(schedule.PlayerListInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private static LeagueProbeFailed Failed(ProbeFailureKind kind, string code, long timestamp) => new(new ProbeFailure(kind, code, "Riot Live Client observation failed.", timestamp));
}
