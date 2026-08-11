namespace RespawnSwitch.Core.Game;

public sealed record GameInstanceKey(
    int ProcessId,
    string RiotId,
    string TimelineKey);
