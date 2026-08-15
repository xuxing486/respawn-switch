using RespawnSwitch.Core.Clock;
using RespawnSwitch.Core.Respawn;

namespace RespawnSwitch.Core.Tests.Clock;

public sealed class RespawnClockTests
{
    private static readonly RespawnCycleId CycleOne =
        new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    [Fact]
    public void Read_UsesCeilingAndBecomesStaleOnlyAfterThreshold()
    {
        var time = new TestTimeProvider(frequency: 1_000);
        var clock = new RespawnClock(time, TimeSpan.FromSeconds(1));

        clock.Reanchor(CycleOne, 2.1, time.GetTimestamp());
        Assert.Equal(3, clock.Read().DisplaySeconds);

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(RespawnClockStatus.Running, clock.Read().Status);

        time.Advance(TimeSpan.FromMilliseconds(1));
        var stale = clock.Read();
        Assert.Equal(RespawnClockStatus.Stale, stale.Status);
        Assert.Null(stale.DisplaySeconds);
        Assert.Null(stale.InterpolatedSeconds);
    }

    [Fact]
    public void Read_InterpolatesFromMonotonicTimeAndClampsAtZero()
    {
        var time = new TestTimeProvider(frequency: 1_000);
        var clock = new RespawnClock(time, TimeSpan.FromSeconds(10));
        clock.Reanchor(CycleOne, 0.15, time.GetTimestamp());

        time.JumpWallClock(TimeSpan.FromDays(10));
        time.Advance(TimeSpan.FromMilliseconds(100));
        var frame = clock.Read();

        Assert.Equal(1, frame.DisplaySeconds);
        Assert.Equal(0.05, frame.InterpolatedSeconds!.Value, precision: 6);

        time.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(0, clock.Read().DisplaySeconds);
    }

    [Fact]
    public void Reanchor_SuppressesOlderSamplesAndAcceptsNewerSamples()
    {
        var time = new TestTimeProvider(frequency: 1_000);
        var clock = new RespawnClock(time, TimeSpan.FromSeconds(10));
        clock.Reanchor(CycleOne, 5, observedAtTimestamp: 0);
        clock.Reanchor(CycleOne, 20, observedAtTimestamp: -1);
        Assert.Equal(5, clock.Read().DisplaySeconds);

        clock.Reanchor(CycleOne, 2.2, observedAtTimestamp: 1);
        Assert.Equal(3, clock.Read().DisplaySeconds);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-0.001)]
    public void Reanchor_RejectsInvalidRemainingSeconds(double value)
    {
        var clock = new RespawnClock(new TestTimeProvider(), TimeSpan.FromSeconds(1));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            clock.Reanchor(CycleOne, value, observedAtTimestamp: 0));
    }

    [Fact]
    public void WaitingStaleAndClear_RespectCycleOwnership()
    {
        var clock = new RespawnClock(new TestTimeProvider(), TimeSpan.FromSeconds(1));
        var other = new RespawnCycleId(Guid.Parse("00000000-0000-0000-0000-000000000002"));

        clock.MarkWaiting(CycleOne);
        Assert.Equal(RespawnClockStatus.WaitingForVerifiedTimer, clock.Read().Status);
        clock.MarkStale(CycleOne);
        Assert.Equal(RespawnClockStatus.Stale, clock.Read().Status);

        clock.Clear(other);
        Assert.Equal(RespawnClockStatus.Stale, clock.Read().Status);
        clock.Clear(CycleOne);
        Assert.Equal(RespawnClockStatus.Inactive, clock.Read().Status);
    }
}
