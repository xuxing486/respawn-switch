namespace RespawnSwitch.Application.Media;

public enum PlaybackState { Unknown, Playing, Paused, Stopped }

public enum MediaFailureKind
{
    None,
    NotConfigured,
    NoMatch,
    AmbiguousMatch,
    PermissionDenied,
    Unsupported,
    TimedOut,
    ProviderHung,
    CommandRejected,
    StateUnverified,
    TargetChanged,
    Cancelled,
    Unexpected
}

public abstract record MediaControlProfile(string ControllerName);

public sealed record GsmtcMediaProfile(
    string SourceAppUserModelId,
    string DiagnosticFingerprint)
    : MediaControlProfile("GSMTC");

public sealed record UiaMediaProfile(
    string NormalizedExecutablePath,
    string WindowClass,
    string SelectorVersion,
    string StateProperty,
    string PlaySelector,
    string PauseSelector)
    : MediaControlProfile("UIA");

public sealed record MediaProbeResult(
    bool IsUsable,
    PlaybackState State,
    MediaFailureKind FailureKind,
    string FailureCode,
    string ControllerName,
    IReadOnlyList<string> DiagnosticFingerprints);

public sealed record PlaybackStateResult(
    PlaybackState State,
    bool IsVerified,
    MediaFailureKind FailureKind,
    string FailureCode,
    string ControllerName);

public sealed record MediaCommandResult(
    bool CommandSent,
    bool TargetAccepted,
    bool StateVerified,
    PlaybackState FinalState,
    MediaFailureKind FailureKind,
    string FailureCode,
    string ControllerName);

public interface IDouyinMediaController
{
    string Name { get; }
    ValueTask<MediaProbeResult> ProbeAsync(CancellationToken cancellationToken);
    ValueTask<MediaCommandResult> PlayAsync(CancellationToken cancellationToken);
    ValueTask<MediaCommandResult> PauseAsync(CancellationToken cancellationToken);
    ValueTask<PlaybackStateResult> GetPlaybackStateAsync(CancellationToken cancellationToken);
}

public interface IMediaControllerFactory
{
    ValueTask<IDouyinMediaController?> CreateAsync(
        IReadOnlyList<MediaControlProfile> profilesInPriorityOrder,
        CancellationToken cancellationToken);
}
