using RespawnSwitch.Application.Media;

namespace RespawnSwitch.Windows.Media;

public sealed class GsmtcDouyinMediaController : IDouyinMediaController
{
    private readonly GsmtcMediaProfile _profile;
    private readonly IGsmtcGateway _gateway;

    public GsmtcDouyinMediaController(GsmtcMediaProfile profile)
        : this(profile, new WinRtGsmtcGateway())
    {
    }

    internal GsmtcDouyinMediaController(GsmtcMediaProfile profile, IGsmtcGateway gateway)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    }

    public string Name => "GSMTC";

    public async ValueTask<MediaProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var sessions = await _gateway.EnumerateAsync(cancellationToken);
            var fingerprints = sessions
                .Select(session => session.DiagnosticFingerprint)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var selection = GsmtcSessionCatalog.Select(sessions, _profile);
            if (selection.SelectedSession is null)
            {
                return new(false, PlaybackState.Unknown, selection.FailureKind, selection.FailureCode, Name, fingerprints);
            }

            var state = await _gateway.ReadStateAsync(selection.SelectedSession.SessionToken, cancellationToken);
            return state == PlaybackState.Unknown
                ? new(false, state, MediaFailureKind.StateUnverified, "gsmtc-state-unknown", Name, fingerprints)
                : new(true, state, MediaFailureKind.None, string.Empty, Name, fingerprints);
        }
        catch (Exception exception)
        {
            var failure = GsmtcFailureMapper.Map(exception, cancellationToken);
            return new(false, PlaybackState.Unknown, failure.Kind, failure.Code, Name, []);
        }
    }

    public ValueTask<MediaCommandResult> PlayAsync(CancellationToken cancellationToken) =>
        SetStateAsync(PlaybackState.Playing, cancellationToken);

    public ValueTask<MediaCommandResult> PauseAsync(CancellationToken cancellationToken) =>
        SetStateAsync(PlaybackState.Paused, cancellationToken);

    public async ValueTask<PlaybackStateResult> GetPlaybackStateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var selection = GsmtcSessionCatalog.Select(
                await _gateway.EnumerateAsync(cancellationToken),
                _profile);
            if (selection.SelectedSession is null)
            {
                return new(PlaybackState.Unknown, false, selection.FailureKind, selection.FailureCode, Name);
            }

            var state = await _gateway.ReadStateAsync(selection.SelectedSession.SessionToken, cancellationToken);
            return state == PlaybackState.Unknown
                ? new(state, false, MediaFailureKind.StateUnverified, "gsmtc-state-unknown", Name)
                : new(state, true, MediaFailureKind.None, string.Empty, Name);
        }
        catch (Exception exception)
        {
            var failure = GsmtcFailureMapper.Map(exception, cancellationToken);
            return new(PlaybackState.Unknown, false, failure.Kind, failure.Code, Name);
        }
    }

    private async ValueTask<MediaCommandResult> SetStateAsync(
        PlaybackState desiredState,
        CancellationToken cancellationToken)
    {
        try
        {
            var initialSelection = GsmtcSessionCatalog.Select(
                await _gateway.EnumerateAsync(cancellationToken),
                _profile);
            if (initialSelection.SelectedSession is null)
            {
                return Failure(initialSelection.FailureKind, initialSelection.FailureCode);
            }

            var initial = initialSelection.SelectedSession;
            var initialState = await _gateway.ReadStateAsync(initial.SessionToken, cancellationToken);
            if (initialState == desiredState)
            {
                return await VerifyFinalStateAsync(initial.SessionToken, desiredState, commandSent: false, accepted: true, cancellationToken);
            }

            var canSend = desiredState == PlaybackState.Playing ? initial.CanPlay : initial.CanPause;
            if (!canSend)
            {
                return Failure(MediaFailureKind.CommandRejected, "gsmtc-control-disabled");
            }

            var preCommandSelection = GsmtcSessionCatalog.Select(
                await _gateway.EnumerateAsync(cancellationToken),
                _profile);
            if (preCommandSelection.SelectedSession is null ||
                !string.Equals(preCommandSelection.SelectedSession.SessionToken, initial.SessionToken, StringComparison.Ordinal))
            {
                return Failure(MediaFailureKind.TargetChanged, "gsmtc-target-changed-before-command");
            }

            var accepted = desiredState == PlaybackState.Playing
                ? await _gateway.TryPlayAsync(initial.SessionToken, cancellationToken)
                : await _gateway.TryPauseAsync(initial.SessionToken, cancellationToken);
            if (!accepted)
            {
                return new(true, false, false, PlaybackState.Unknown, MediaFailureKind.CommandRejected, "gsmtc-command-rejected", Name);
            }

            return await VerifyFinalStateAsync(initial.SessionToken, desiredState, commandSent: true, accepted: true, cancellationToken);
        }
        catch (Exception exception)
        {
            var failure = GsmtcFailureMapper.Map(exception, cancellationToken);
            return Failure(failure.Kind, failure.Code);
        }
    }

    private async ValueTask<MediaCommandResult> VerifyFinalStateAsync(
        string originalSessionToken,
        PlaybackState desiredState,
        bool commandSent,
        bool accepted,
        CancellationToken cancellationToken)
    {
        var finalSelection = GsmtcSessionCatalog.Select(
            await _gateway.EnumerateAsync(cancellationToken),
            _profile);
        if (finalSelection.SelectedSession is null ||
            !string.Equals(finalSelection.SelectedSession.SessionToken, originalSessionToken, StringComparison.Ordinal))
        {
            return new(commandSent, accepted, false, PlaybackState.Unknown, MediaFailureKind.TargetChanged, "gsmtc-target-changed-after-command", Name);
        }

        var finalState = await _gateway.ReadStateAsync(originalSessionToken, cancellationToken);
        return finalState == desiredState
            ? new(commandSent, accepted, true, finalState, MediaFailureKind.None, string.Empty, Name)
            : new(commandSent, accepted, false, finalState, MediaFailureKind.StateUnverified, "gsmtc-final-state-mismatch", Name);
    }

    private MediaCommandResult Failure(MediaFailureKind kind, string code) =>
        new(false, false, false, PlaybackState.Unknown, kind, code, Name);

}
