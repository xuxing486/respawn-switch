using RespawnSwitch.Riot.Polling;

namespace RespawnSwitch.Riot.Tests.Polling;

public sealed class ProbeSequenceGuardTests
{
    [Fact]
    public void TryAccept_SuppressesOlderAndDuplicateCompletions()
    {
        var guard = new ProbeSequenceGuard();

        Assert.True(guard.TryAccept(2));
        Assert.False(guard.TryAccept(1));
        Assert.False(guard.TryAccept(2));
        Assert.True(guard.TryAccept(3));
    }
}
