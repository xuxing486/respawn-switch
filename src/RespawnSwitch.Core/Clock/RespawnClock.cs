using RespawnSwitch.Core.Respawn;

namespace RespawnSwitch.Core.Clock;

public sealed class RespawnClock
{
    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _staleAfter;
    private RespawnClockStatus _status;
    private RespawnCycleId? _cycleId;
    private double _anchorRemainingSeconds;
    private long _anchorTimestamp;

    public RespawnClock(TimeProvider timeProvider, TimeSpan staleAfter)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (staleAfter < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(staleAfter));
        }

        _timeProvider = timeProvider;
        _staleAfter = staleAfter;
    }

    public void Reanchor(RespawnCycleId cycleId, double remainingSeconds, long observedAtTimestamp)
    {
        if (!double.IsFinite(remainingSeconds) || remainingSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(remainingSeconds));
        }

        lock (_gate)
        {
            if (_status == RespawnClockStatus.Running &&
                _cycleId == cycleId &&
                observedAtTimestamp < _anchorTimestamp)
            {
                return;
            }

            _cycleId = cycleId;
            _anchorRemainingSeconds = remainingSeconds;
            _anchorTimestamp = observedAtTimestamp;
            _status = RespawnClockStatus.Running;
        }
    }

    public void MarkWaiting(RespawnCycleId cycleId)
    {
        lock (_gate)
        {
            _cycleId = cycleId;
            _status = RespawnClockStatus.WaitingForVerifiedTimer;
        }
    }

    public void MarkStale(RespawnCycleId cycleId)
    {
        lock (_gate)
        {
            _cycleId = cycleId;
            _status = RespawnClockStatus.Stale;
        }
    }

    public void Clear(RespawnCycleId? expectedCycleId)
    {
        lock (_gate)
        {
            if (expectedCycleId is not null && expectedCycleId != _cycleId)
            {
                return;
            }

            _cycleId = null;
            _status = RespawnClockStatus.Inactive;
            _anchorRemainingSeconds = 0;
            _anchorTimestamp = 0;
        }
    }

    public RespawnClockFrame Read()
    {
        lock (_gate)
        {
            var now = _timeProvider.GetTimestamp();
            if (_status != RespawnClockStatus.Running)
            {
                return new RespawnClockFrame(_status, _cycleId, null, null, now);
            }

            var elapsed = _timeProvider.GetElapsedTime(_anchorTimestamp, now);
            if (elapsed > _staleAfter)
            {
                _status = RespawnClockStatus.Stale;
                return new RespawnClockFrame(_status, _cycleId, null, null, now);
            }

            var interpolated = Math.Max(0, _anchorRemainingSeconds - Math.Max(0, elapsed.TotalSeconds));
            return new RespawnClockFrame(
                _status,
                _cycleId,
                (int)Math.Ceiling(interpolated),
                interpolated,
                now);
        }
    }
}
