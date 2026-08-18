// Author: Stress Monster
namespace RespawnSwitch.Application.Audio;

public interface IProcessAudioMuteController
{
    ValueTask<IProcessAudioMuteLease> MuteAsync(int processId, CancellationToken cancellationToken);
}

public interface IProcessAudioMuteLease : IAsyncDisposable
{
    int ProcessId { get; }
    int ChangedSessionCount { get; }
}
