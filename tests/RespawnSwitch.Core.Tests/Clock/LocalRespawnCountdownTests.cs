using RespawnSwitch.Core.Clock;

namespace RespawnSwitch.Core.Tests.Clock;

public sealed class LocalRespawnCountdownTests
{
    [Fact]
    public void Snapshot_UsesOneMonotonicAnchorUntilConfirmation()
    {
        var time = new TestTimeProvider();
        var countdown = LocalRespawnCountdown.Create(time, 12.2);

        Assert.Equal(13, countdown.Snapshot().DisplaySeconds);
        time.Advance(TimeSpan.FromMilliseconds(300));
        Assert.Equal(12, countdown.Snapshot().DisplaySeconds);
        time.Advance(TimeSpan.FromSeconds(12));

        var finished = countdown.Snapshot();
        Assert.Equal(0, finished.DisplaySeconds);
        Assert.True(finished.AwaitingRespawnConfirmation);
    }

    [Fact]
    public void Create_RejectsInvalidTimer()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LocalRespawnCountdown.Create(new TestTimeProvider(), double.NaN));
    }
}
