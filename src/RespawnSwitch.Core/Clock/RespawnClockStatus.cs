namespace RespawnSwitch.Core.Clock;

public enum RespawnClockStatus
{
    Inactive,
    WaitingForVerifiedTimer,
    Running,
    Stale
}
