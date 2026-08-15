using System.Security.Cryptography;
using System.Text;
using RespawnSwitch.Application.Media;
using Windows.Media.Control;

namespace RespawnSwitch.Windows.Media;

internal sealed class WinRtGsmtcGateway : IGsmtcGateway
{
    private const string FingerprintSchema = "respawnswitch-gsmtc-fingerprint-v1";
    private const string SessionTokenSchema = "respawnswitch-gsmtc-session-token-v1";

    public async ValueTask<IReadOnlyList<GsmtcSessionDescriptor>> EnumerateAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        cancellationToken.ThrowIfCancellationRequested();

        return manager.GetSessions()
            .Select(ToDescriptor)
            .ToArray();
    }

    public async ValueTask<bool> TryPlayAsync(string sessionToken, CancellationToken cancellationToken)
    {
        var session = await ResolveUniqueAsync(sessionToken, cancellationToken);
        return session is not null && await session.TryPlayAsync();
    }

    public async ValueTask<bool> TryPauseAsync(string sessionToken, CancellationToken cancellationToken)
    {
        var session = await ResolveUniqueAsync(sessionToken, cancellationToken);
        return session is not null && await session.TryPauseAsync();
    }

    public async ValueTask<PlaybackState> ReadStateAsync(
        string sessionToken,
        CancellationToken cancellationToken)
    {
        var session = await ResolveUniqueAsync(sessionToken, cancellationToken);
        return session is null ? PlaybackState.Unknown : MapState(session.GetPlaybackInfo().PlaybackStatus);
    }

    private static async ValueTask<GlobalSystemMediaTransportControlsSession?> ResolveUniqueAsync(
        string sessionToken,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        cancellationToken.ThrowIfCancellationRequested();

        var matches = manager.GetSessions()
            .Where(session => string.Equals(
                CreateHash(SessionTokenSchema, session.SourceAppUserModelId),
                sessionToken,
                StringComparison.Ordinal))
            .Take(2)
            .ToArray();

        return matches.Length == 1 ? matches[0] : null;
    }

    private static GsmtcSessionDescriptor ToDescriptor(GlobalSystemMediaTransportControlsSession session)
    {
        var source = session.SourceAppUserModelId;
        var playbackInfo = session.GetPlaybackInfo();
        return new(
            CreateHash(SessionTokenSchema, source),
            source,
            CreateHash(FingerprintSchema, source),
            MapState(playbackInfo.PlaybackStatus),
            playbackInfo.Controls.IsPlayEnabled,
            playbackInfo.Controls.IsPauseEnabled);
    }

    private static string CreateHash(string schema, string sourceAppUserModelId)
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(schema, "\0", sourceAppUserModelId));
        return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
    }

    private static PlaybackState MapState(GlobalSystemMediaTransportControlsSessionPlaybackStatus status) =>
        status switch
        {
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => PlaybackState.Playing,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => PlaybackState.Paused,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => PlaybackState.Stopped,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed => PlaybackState.Stopped,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Opened => PlaybackState.Stopped,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Changing => PlaybackState.Unknown,
            _ => PlaybackState.Unknown
        };
}
