using RespawnSwitch.Core.Game;
namespace RespawnSwitch.Riot.Http;
public sealed class RiotApiException(ProbeFailureKind kind, string message, Exception? innerException = null) : Exception(message, innerException)
{ public ProbeFailureKind Kind { get; } = kind; }
