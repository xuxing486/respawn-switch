// Author: Stress Monster
using System.Collections.Concurrent;
using RespawnSwitch.Core.Respawn;

namespace RespawnSwitch.Application.Audio;

public sealed class RespawnAudioMuteCoordinator(IProcessAudioMuteController controller) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<RespawnCycleId, CycleState> cycles = new();
    private int disposed;

    public async ValueTask BeginAsync(RespawnCycleId cycleId, int processId, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        var state = cycles.GetOrAdd(cycleId, static _ => new CycleState());
        var lease = await controller.MuteAsync(processId, cancellationToken).ConfigureAwait(false);
        IProcessAudioMuteLease? previous = null;
        var restoreImmediately = false;

        lock (state.Sync)
        {
            if (state.Completed || Volatile.Read(ref disposed) != 0)
            {
                restoreImmediately = true;
            }
            else
            {
                previous = state.Lease;
                state.Lease = lease;
            }
        }

        if (previous is not null) await previous.DisposeAsync().ConfigureAwait(false);
        if (restoreImmediately) await lease.DisposeAsync().ConfigureAwait(false);
    }

    public async ValueTask CompleteAsync(RespawnCycleId cycleId)
    {
        var state = cycles.GetOrAdd(cycleId, static _ => new CycleState());
        IProcessAudioMuteLease? lease;
        lock (state.Sync)
        {
            state.Completed = true;
            lease = state.Lease;
            state.Lease = null;
        }

        if (lease is not null) await lease.DisposeAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        var leases = new List<IProcessAudioMuteLease>();
        foreach (var state in cycles.Values)
        {
            lock (state.Sync)
            {
                state.Completed = true;
                if (state.Lease is not null) leases.Add(state.Lease);
                state.Lease = null;
            }
        }
        cycles.Clear();
        foreach (var lease in leases) await lease.DisposeAsync().ConfigureAwait(false);
    }

    private sealed class CycleState
    {
        public object Sync { get; } = new();
        public bool Completed { get; set; }
        public IProcessAudioMuteLease? Lease { get; set; }
    }
}
