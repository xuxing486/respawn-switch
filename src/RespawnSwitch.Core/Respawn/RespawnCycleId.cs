namespace RespawnSwitch.Core.Respawn;

public readonly record struct RespawnCycleId(Guid Value)
{
    public static RespawnCycleId New() => new(Guid.NewGuid());
}
