namespace RespawnSwitch.Core.Game;

public sealed record GamePresenceSnapshot(
    bool ProcessPresent,
    bool WindowPresent,
    int? ProcessId,
    string? InstanceKey);
