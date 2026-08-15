using RespawnSwitch.Application.Media;

namespace RespawnSwitch.Windows.Media;

public sealed record DouyinMediaDiscovery(string SourceAppUserModelId, string DiagnosticFingerprint, PlaybackState State);

public static class DouyinGsmTcDiscovery
{
    public static async ValueTask<IReadOnlyList<DouyinMediaDiscovery>> DiscoverAsync(CancellationToken cancellationToken)
    {
        var sessions = await new WinRtGsmtcGateway().EnumerateAsync(cancellationToken).ConfigureAwait(false);
        return sessions.Where(x => x.SourceAppUserModelId.Contains("douyin", StringComparison.OrdinalIgnoreCase))
            .Select(x => new DouyinMediaDiscovery(x.SourceAppUserModelId, x.DiagnosticFingerprint, x.PlaybackState)).ToArray();
    }
}
