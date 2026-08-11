namespace RespawnSwitch.Core.Respawn;

public sealed record RespawnTransition(
    RespawnMachineState Previous,
    RespawnMachineState Current,
    IReadOnlyList<RespawnDomainEvent> Events);
