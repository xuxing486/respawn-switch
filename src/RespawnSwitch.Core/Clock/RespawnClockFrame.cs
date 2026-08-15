using RespawnSwitch.Core.Respawn;

namespace RespawnSwitch.Core.Clock;

public sealed record RespawnClockFrame(
    RespawnClockStatus Status,
    RespawnCycleId? CycleId,
    int? DisplaySeconds,
    double? InterpolatedSeconds,
    long ReadAtTimestamp);
