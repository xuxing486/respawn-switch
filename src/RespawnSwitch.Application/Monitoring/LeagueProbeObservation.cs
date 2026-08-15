using RespawnSwitch.Core.Game;

namespace RespawnSwitch.Application.Monitoring;

public abstract record LeagueProbeObservation(long ObservedAtTimestamp);

public sealed record LeagueSampleObserved(GameSample Sample) : LeagueProbeObservation(Sample.ObservedAtTimestamp);

public sealed record LeagueProbeFailed(ProbeFailure Failure) : LeagueProbeObservation(Failure.ObservedAtTimestamp);
