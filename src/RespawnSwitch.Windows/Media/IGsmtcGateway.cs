using RespawnSwitch.Application.Media;

namespace RespawnSwitch.Windows.Media;

internal interface IGsmtcGateway
{
    ValueTask<IReadOnlyList<GsmtcSessionDescriptor>> EnumerateAsync(
        CancellationToken cancellationToken);

    ValueTask<bool> TryPlayAsync(
        string sessionToken,
        CancellationToken cancellationToken);

    ValueTask<bool> TryPauseAsync(
        string sessionToken,
        CancellationToken cancellationToken);

    ValueTask<PlaybackState> ReadStateAsync(
        string sessionToken,
        CancellationToken cancellationToken);
}
