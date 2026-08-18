// Author: Stress Monster
using RespawnSwitch.Application.Audio;
using RespawnSwitch.Core.Respawn;

namespace RespawnSwitch.Application.Tests.Audio;

public sealed class RespawnAudioMuteCoordinatorTests
{
    [Fact]
    public async Task Begin_mutes_the_exact_game_process_and_complete_restores_it()
    {
        var controller = new RecordingAudioMuteController();
        await using var cycles = new RespawnAudioMuteCoordinator(controller);
        var cycle = new RespawnCycleId(Guid.NewGuid());

        await cycles.BeginAsync(cycle, 31415, CancellationToken.None);

        Assert.Equal(31415, controller.Lease.ProcessId);
        Assert.True(controller.Lease.IsMuted);

        await cycles.CompleteAsync(cycle);

        Assert.False(controller.Lease.IsMuted);
    }

    [Fact]
    public async Task Complete_wins_a_race_with_a_late_mute_and_does_not_leave_game_audio_muted()
    {
        var controller = new DelayedAudioMuteController();
        await using var cycles = new RespawnAudioMuteCoordinator(controller);
        var cycle = new RespawnCycleId(Guid.NewGuid());
        var begin = cycles.BeginAsync(cycle, 27182, CancellationToken.None).AsTask();

        await cycles.CompleteAsync(cycle);
        controller.ReleaseMute();
        await begin;

        Assert.False(controller.Lease.IsMuted);
    }

    [Fact]
    public async Task Begin_after_cycle_completion_immediately_restores_the_late_mute()
    {
        var controller = new RecordingAudioMuteController();
        await using var cycles = new RespawnAudioMuteCoordinator(controller);
        var cycle = new RespawnCycleId(Guid.NewGuid());

        await cycles.CompleteAsync(cycle);
        await cycles.BeginAsync(cycle, 16180, CancellationToken.None);

        Assert.False(controller.Lease.IsMuted);
    }

    private sealed class RecordingAudioMuteController : IProcessAudioMuteController
    {
        public RecordingLease Lease { get; private set; } = new(0);
        public ValueTask<IProcessAudioMuteLease> MuteAsync(int processId, CancellationToken cancellationToken)
        {
            Lease = new(processId);
            return ValueTask.FromResult<IProcessAudioMuteLease>(Lease);
        }
    }

    private sealed class DelayedAudioMuteController : IProcessAudioMuteController
    {
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public RecordingLease Lease { get; private set; } = new(0);
        public void ReleaseMute() => release.TrySetResult();
        public async ValueTask<IProcessAudioMuteLease> MuteAsync(int processId, CancellationToken cancellationToken)
        {
            await release.Task.WaitAsync(cancellationToken);
            Lease = new(processId);
            return Lease;
        }
    }

    private sealed class RecordingLease(int processId) : IProcessAudioMuteLease
    {
        public int ProcessId { get; } = processId;
        public int ChangedSessionCount => IsMuted ? 1 : 0;
        public bool IsMuted { get; private set; } = true;
        public ValueTask DisposeAsync() { IsMuted = false; return ValueTask.CompletedTask; }
    }
}
