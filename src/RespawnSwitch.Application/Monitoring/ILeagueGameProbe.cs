namespace RespawnSwitch.Application.Monitoring;

public interface ILeagueGameProbe
{
    ValueTask<LeagueProbeObservation> SampleOnceAsync(CancellationToken cancellationToken);
    IAsyncEnumerable<LeagueProbeObservation> WatchAsync(CancellationToken cancellationToken);
}
