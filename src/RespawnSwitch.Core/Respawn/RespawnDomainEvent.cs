using RespawnSwitch.Core.Game;

namespace RespawnSwitch.Core.Respawn;

public abstract record RespawnDomainEvent(long OccurredAtTimestamp);

public sealed record LifeStateSynchronized(
    LifeState State,
    long Timestamp) : RespawnDomainEvent(Timestamp);

public sealed record DeathConfirmed(
    RespawnCycleId CycleId,
    GameSample Sample,
    bool IsLateDiscovery) : RespawnDomainEvent(Sample.ObservedAtTimestamp);

public sealed record AttachmentRequested(
    RespawnCycleId CycleId,
    GameSample Sample) : RespawnDomainEvent(Sample.ObservedAtTimestamp);

public sealed record DeadSampleUpdated(
    RespawnCycleId CycleId,
    GameSample Sample) : RespawnDomainEvent(Sample.ObservedAtTimestamp);

public sealed record RespawnConfirmed(
    RespawnCycleId CycleId,
    GameSample Sample) : RespawnDomainEvent(Sample.ObservedAtTimestamp);

public sealed record ConnectionBecameStale(long Timestamp)
    : RespawnDomainEvent(Timestamp);

public sealed record ConnectionRestored(long Timestamp)
    : RespawnDomainEvent(Timestamp);

public sealed record AbandonCycleDueToUnknown(
    RespawnCycleId CycleId,
    long Timestamp) : RespawnDomainEvent(Timestamp);

public sealed record NoGameConfirmed(
    RespawnCycleId? PriorCycleId,
    long Timestamp) : RespawnDomainEvent(Timestamp);

public sealed record TimelineResetRequested(
    RespawnCycleId? PriorCycleId,
    string ReasonCode,
    long Timestamp) : RespawnDomainEvent(Timestamp);
