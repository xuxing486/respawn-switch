namespace RespawnSwitch.Core.Game;

public enum ProbeFailureKind
{
    ConnectionRefused,
    Timeout,
    TlsRejected,
    InvalidJson,
    SchemaChanged,
    PlayerNotFound,
    AmbiguousPlayer,
    Cancelled,
    Unexpected
}

public sealed record ProbeFailure(
    ProbeFailureKind Kind,
    string Code,
    string Message,
    long ObservedAtTimestamp);
