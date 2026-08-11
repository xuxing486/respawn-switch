namespace RespawnSwitch.Core.Respawn;

public enum LifeState
{
    Unknown,
    Alive,
    Dead
}

public enum ConnectionState
{
    NoGame,
    Online,
    Stale
}

public enum ActiveCycleStatus
{
    None,
    Active,
    AbandonedUnknown,
    Completed
}
