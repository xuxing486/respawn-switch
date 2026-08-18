// Author: Stress Monster
using RespawnSwitch.Application.Audio;
using System.Runtime.InteropServices;

namespace RespawnSwitch.Windows.Audio;

public readonly record struct ProcessAudioSessionKey(string EndpointId, string SessionInstanceId);
public readonly record struct ProcessAudioSessionSnapshot(ProcessAudioSessionKey Key, int ProcessId, bool IsMuted);

public interface IProcessAudioSessionAccessor
{
    IReadOnlyList<ProcessAudioSessionSnapshot> Snapshot(int processId);
    bool TryChangeMute(ProcessAudioSessionKey key, int processId, bool expectedCurrentMute, bool targetMute);
}

public sealed class WindowsProcessAudioMuteController : IProcessAudioMuteController
{
    private readonly IProcessAudioSessionAccessor sessions;

    public WindowsProcessAudioMuteController() : this(new CoreAudioProcessSessionAccessor()) { }
    public WindowsProcessAudioMuteController(IProcessAudioSessionAccessor sessions) => this.sessions = sessions;

    public ValueTask<IProcessAudioMuteLease> MuteAsync(int processId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (processId <= 0) return ValueTask.FromResult<IProcessAudioMuteLease>(new Lease(sessions, processId, []));

        var changed = new List<ProcessAudioSessionKey>();
        try
        {
            foreach (var session in sessions.Snapshot(processId))
            {
                if (!session.IsMuted && sessions.TryChangeMute(session.Key, processId, false, true))
                    changed.Add(session.Key);
            }
        }
        catch (COMException) { }
        catch (InvalidCastException) { }
        catch (OverflowException) { }
        return ValueTask.FromResult<IProcessAudioMuteLease>(new Lease(sessions, processId, changed));
    }

    private sealed class Lease(IProcessAudioSessionAccessor sessions, int processId, IReadOnlyList<ProcessAudioSessionKey> changed) : IProcessAudioMuteLease
    {
        private IReadOnlyList<ProcessAudioSessionKey>? changedSessions = changed;
        public int ProcessId { get; } = processId;
        public int ChangedSessionCount => Volatile.Read(ref changedSessions)?.Count ?? 0;

        public ValueTask DisposeAsync()
        {
            var restore = Interlocked.Exchange(ref changedSessions, null);
            if (restore is null) return ValueTask.CompletedTask;
            foreach (var key in restore)
                sessions.TryChangeMute(key, ProcessId, true, false);
            return ValueTask.CompletedTask;
        }
    }
}
