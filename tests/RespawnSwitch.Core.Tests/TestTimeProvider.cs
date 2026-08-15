namespace RespawnSwitch.Core.Tests;

internal sealed class TestTimeProvider(long frequency = TimeSpan.TicksPerSecond) : TimeProvider
{
    private long _timestamp;
    private DateTimeOffset _utcNow = DateTimeOffset.UnixEpoch;

    public override long TimestampFrequency { get; } = frequency;

    public override long GetTimestamp() => _timestamp;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan duration)
    {
        _timestamp += (long)(duration.TotalSeconds * TimestampFrequency);
        _utcNow += duration;
    }

    public void JumpWallClock(TimeSpan duration) => _utcNow += duration;
}
