using RespawnSwitch.Application.Media;

namespace RespawnSwitch.Windows.Media;

internal sealed record GsmtcSessionDescriptor(
    string SessionToken,
    string SourceAppUserModelId,
    string DiagnosticFingerprint,
    PlaybackState PlaybackState,
    bool CanPlay,
    bool CanPause);
