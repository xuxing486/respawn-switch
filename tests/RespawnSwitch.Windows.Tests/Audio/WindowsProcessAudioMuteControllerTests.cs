// Author: Stress Monster
using RespawnSwitch.Windows.Audio;

namespace RespawnSwitch.Windows.Tests.Audio;

public sealed class WindowsProcessAudioMuteControllerTests
{
    [Fact]
    public void Core_audio_interop_enumerates_active_render_sessions_without_error()
    {
        var sessions = new CoreAudioProcessSessionAccessor();

        var missingProcess = sessions.Snapshot(int.MaxValue);

        Assert.Empty(missingProcess);
    }

    [Fact]
    public async Task Mute_changes_only_the_target_process_and_restore_preserves_preexisting_mute()
    {
        var leagueAudible = new ProcessAudioSessionKey("speakers", "league-audible");
        var leagueAlreadyMuted = new ProcessAudioSessionKey("speakers", "league-muted");
        var douyin = new ProcessAudioSessionKey("speakers", "douyin");
        var sessions = new FakeProcessAudioSessionAccessor(
            new(leagueAudible, 101, false),
            new(leagueAlreadyMuted, 101, true),
            new(douyin, 202, false));
        var controller = new WindowsProcessAudioMuteController(sessions);

        var lease = await controller.MuteAsync(101, CancellationToken.None);

        Assert.True(sessions.IsMuted(leagueAudible));
        Assert.True(sessions.IsMuted(leagueAlreadyMuted));
        Assert.False(sessions.IsMuted(douyin));
        Assert.Equal(1, lease.ChangedSessionCount);

        await lease.DisposeAsync();

        Assert.False(sessions.IsMuted(leagueAudible));
        Assert.True(sessions.IsMuted(leagueAlreadyMuted));
        Assert.False(sessions.IsMuted(douyin));
    }

    [Fact]
    public async Task Restore_does_not_force_a_session_that_the_user_unmuted_during_douyin()
    {
        var league = new ProcessAudioSessionKey("headset", "league");
        var sessions = new FakeProcessAudioSessionAccessor(new ProcessAudioSessionSnapshot(league, 303, false));
        var controller = new WindowsProcessAudioMuteController(sessions);
        var lease = await controller.MuteAsync(303, CancellationToken.None);
        sessions.SetExternally(league, false);

        await lease.DisposeAsync();

        Assert.False(sessions.IsMuted(league));
    }

    private sealed class FakeProcessAudioSessionAccessor(params ProcessAudioSessionSnapshot[] initial) : IProcessAudioSessionAccessor
    {
        private readonly Dictionary<ProcessAudioSessionKey, ProcessAudioSessionSnapshot> sessions = initial.ToDictionary(x => x.Key);

        public IReadOnlyList<ProcessAudioSessionSnapshot> Snapshot(int processId) => sessions.Values.Where(x => x.ProcessId == processId).ToArray();

        public bool TryChangeMute(ProcessAudioSessionKey key, int processId, bool expectedCurrentMute, bool targetMute)
        {
            if (!sessions.TryGetValue(key, out var current) || current.ProcessId != processId || current.IsMuted != expectedCurrentMute) return false;
            sessions[key] = current with { IsMuted = targetMute };
            return true;
        }

        public bool IsMuted(ProcessAudioSessionKey key) => sessions[key].IsMuted;
        public void SetExternally(ProcessAudioSessionKey key, bool muted) => sessions[key] = sessions[key] with { IsMuted = muted };
    }
}
