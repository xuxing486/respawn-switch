namespace RespawnSwitch.Riot.Polling;
public sealed class ProbeSequenceGuard { private long _accepted; public bool TryAccept(long sequence) { lock (this) { if (sequence <= _accepted) return false; _accepted = sequence; return true; } } }
