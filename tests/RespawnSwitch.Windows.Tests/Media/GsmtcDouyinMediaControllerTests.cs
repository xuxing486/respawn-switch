using RespawnSwitch.Application.Media;
using RespawnSwitch.Windows.Media;
using System.Runtime.InteropServices;

namespace RespawnSwitch.Windows.Tests.Media;

public sealed class GsmtcDouyinMediaControllerTests
{
    private static readonly GsmtcMediaProfile Profile = new("douyin.aumid", "fingerprint-v1");

    [Fact]
    public async Task PlayAsync_DoesNotSendACommand_WhenAlreadyPlaying()
    {
        var gateway = new FakeGateway(
            enumerations: [[Session("stable")], [Session("stable")]],
            states: [PlaybackState.Playing, PlaybackState.Playing]);
        var controller = new GsmtcDouyinMediaController(Profile, gateway);

        var result = await controller.PlayAsync(CancellationToken.None);

        Assert.False(result.CommandSent);
        Assert.True(result.StateVerified);
        Assert.Equal(PlaybackState.Playing, result.FinalState);
        Assert.Equal(0, gateway.PlayCalls);
        Assert.Equal(0, gateway.PauseCalls);
    }

    [Fact]
    public async Task PlayAsync_SendsExplicitPlayOnce_AndReReadsFinalState()
    {
        var gateway = new FakeGateway(
            enumerations: [[Session("stable")], [Session("stable")], [Session("stable")]],
            states: [PlaybackState.Paused, PlaybackState.Playing]);
        var controller = new GsmtcDouyinMediaController(Profile, gateway);

        var result = await controller.PlayAsync(CancellationToken.None);

        Assert.True(result.CommandSent);
        Assert.True(result.TargetAccepted);
        Assert.True(result.StateVerified);
        Assert.Equal(1, gateway.PlayCalls);
        Assert.Equal(0, gateway.PauseCalls);
        Assert.Equal(2, gateway.ReadStateCalls);
    }

    [Fact]
    public async Task PauseAsync_IsIdempotentAndUsesOnlyExplicitPause()
    {
        var alreadyPaused = new FakeGateway(
            enumerations: [[Session("stable")], [Session("stable")]],
            states: [PlaybackState.Paused, PlaybackState.Paused]);
        var pausedResult = await new GsmtcDouyinMediaController(Profile, alreadyPaused)
            .PauseAsync(CancellationToken.None);

        var playing = new FakeGateway(
            enumerations: [[Session("stable")], [Session("stable")], [Session("stable")]],
            states: [PlaybackState.Playing, PlaybackState.Paused]);
        var playingResult = await new GsmtcDouyinMediaController(Profile, playing)
            .PauseAsync(CancellationToken.None);

        Assert.False(pausedResult.CommandSent);
        Assert.True(pausedResult.StateVerified);
        Assert.True(playingResult.StateVerified);
        Assert.Equal(1, playing.PauseCalls);
        Assert.Equal(0, playing.PlayCalls);
    }

    [Fact]
    public async Task PlayAsync_ReturnsCommandRejected_WhenProviderRejectsExplicitPlay()
    {
        var gateway = new FakeGateway(
            enumerations: [[Session("stable")], [Session("stable")]],
            states: [PlaybackState.Paused]) { AcceptPlay = false };

        var result = await new GsmtcDouyinMediaController(Profile, gateway)
            .PlayAsync(CancellationToken.None);

        Assert.Equal(MediaFailureKind.CommandRejected, result.FailureKind);
        Assert.False(result.TargetAccepted);
        Assert.False(result.StateVerified);
    }

    [Fact]
    public async Task PlayAsync_ReturnsStateUnverified_WhenAcceptedCommandDoesNotReachPlaying()
    {
        var gateway = new FakeGateway(
            enumerations: [[Session("stable")], [Session("stable")], [Session("stable")]],
            states: [PlaybackState.Paused, PlaybackState.Paused]);

        var result = await new GsmtcDouyinMediaController(Profile, gateway)
            .PlayAsync(CancellationToken.None);

        Assert.True(result.TargetAccepted);
        Assert.False(result.StateVerified);
        Assert.Equal(MediaFailureKind.StateUnverified, result.FailureKind);
    }

    [Fact]
    public async Task PlayAsync_ReturnsTargetChanged_WithoutSending_WhenTokenChangesBeforeCommand()
    {
        var gateway = new FakeGateway(
            enumerations: [[Session("before")], [Session("after")]],
            states: [PlaybackState.Paused]);

        var result = await new GsmtcDouyinMediaController(Profile, gateway)
            .PlayAsync(CancellationToken.None);

        Assert.Equal(MediaFailureKind.TargetChanged, result.FailureKind);
        Assert.Equal(0, gateway.PlayCalls);
    }

    [Fact]
    public async Task ProbeAsync_MapsCancellationToCancelled()
    {
        var gateway = new FakeGateway([], []) { ThrowCancellation = true };

        var result = await new GsmtcDouyinMediaController(Profile, gateway)
            .ProbeAsync(new CancellationToken(canceled: true));

        Assert.Equal(MediaFailureKind.Cancelled, result.FailureKind);
        Assert.False(result.IsUsable);
    }

    [Fact]
    public async Task ProbeAsync_MapsMissingGsmtcServiceToUnsupported()
    {
        var gateway = new FakeGateway([], [])
        {
            EnumerationException = new COMException("Service is not installed", unchecked((int)0x80070424))
        };

        var result = await new GsmtcDouyinMediaController(Profile, gateway)
            .ProbeAsync(CancellationToken.None);

        Assert.Equal(MediaFailureKind.Unsupported, result.FailureKind);
        Assert.False(result.IsUsable);
    }

    private static GsmtcSessionDescriptor Session(string token) =>
        new(token, "douyin.aumid", "fingerprint-v1", PlaybackState.Paused, CanPlay: true, CanPause: true);

    private sealed class FakeGateway(
        IReadOnlyList<IReadOnlyList<GsmtcSessionDescriptor>> enumerations,
        IReadOnlyList<PlaybackState> states) : IGsmtcGateway
    {
        private readonly Queue<IReadOnlyList<GsmtcSessionDescriptor>> _enumerations = new(enumerations);
        private readonly Queue<PlaybackState> _states = new(states);

        public bool AcceptPlay { get; init; } = true;
        public bool AcceptPause { get; init; } = true;
        public bool ThrowCancellation { get; init; }
        public Exception? EnumerationException { get; init; }
        public int PlayCalls { get; private set; }
        public int PauseCalls { get; private set; }
        public int ReadStateCalls { get; private set; }

        public ValueTask<IReadOnlyList<GsmtcSessionDescriptor>> EnumerateAsync(CancellationToken cancellationToken)
        {
            if (EnumerationException is not null)
            {
                throw EnumerationException;
            }

            if (ThrowCancellation)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            return ValueTask.FromResult(_enumerations.Dequeue());
        }

        public ValueTask<bool> TryPlayAsync(string sessionToken, CancellationToken cancellationToken)
        {
            PlayCalls++;
            return ValueTask.FromResult(AcceptPlay);
        }

        public ValueTask<bool> TryPauseAsync(string sessionToken, CancellationToken cancellationToken)
        {
            PauseCalls++;
            return ValueTask.FromResult(AcceptPause);
        }

        public ValueTask<PlaybackState> ReadStateAsync(string sessionToken, CancellationToken cancellationToken)
        {
            ReadStateCalls++;
            return ValueTask.FromResult(_states.Dequeue());
        }
    }
}
