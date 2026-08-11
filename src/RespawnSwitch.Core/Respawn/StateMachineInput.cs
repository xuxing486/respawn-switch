using RespawnSwitch.Core.Game;

namespace RespawnSwitch.Core.Respawn;

public abstract record StateMachineInput(long ObservedAtTimestamp);

public sealed record SuccessfulSampleInput(GameSample Sample)
    : StateMachineInput(Sample.ObservedAtTimestamp);

public sealed record ProbeFailureInput(ProbeFailure Failure)
    : StateMachineInput(Failure.ObservedAtTimestamp);

public sealed record PresenceCheckInput(
    GamePresenceSnapshot Presence,
    long Timestamp)
    : StateMachineInput(Timestamp);

public sealed record TimePulseInput(long Timestamp)
    : StateMachineInput(Timestamp);
