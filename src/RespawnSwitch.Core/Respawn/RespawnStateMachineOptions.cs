namespace RespawnSwitch.Core.Respawn;

public sealed record RespawnStateMachineOptions(
    TimeSpan StaleAfter,
    TimeSpan AbandonDeadCycleAfter,
    TimeSpan NoGameConfirmationSpan,
    double AttachmentThresholdSeconds,
    long TimestampFrequency);
