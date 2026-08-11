using RespawnSwitch.Core.Game;
using RespawnSwitch.Core.Respawn;

namespace RespawnSwitch.Core.Tests.Respawn;

public sealed class AbandonCycleTests
{
    [Fact]
    public void DeadStaleForFiveSeconds_AbandonsOnceWithoutBecomingAlive()
    {
        var machine = StateMachineScenario.DeadStale(
            staleSinceTimestamp: 1_000,
            timestampFrequency: 1_000);

        var first = machine.Apply(new TimePulseInput(6_000));
        var second = machine.Apply(new TimePulseInput(7_000));

        Assert.Single(first.Events.OfType<AbandonCycleDueToUnknown>());
        Assert.Empty(second.Events.OfType<AbandonCycleDueToUnknown>());
        Assert.Equal(LifeState.Dead, second.Current.LifeState);
        Assert.Equal(ActiveCycleStatus.AbandonedUnknown, second.Current.ActiveCycleStatus);
    }

    [Fact]
    public void NoGameRequiresProcessAndWindowAbsentTwiceAcrossTwoSeconds()
    {
        var machine = StateMachineScenario.DeadOnline();
        var absent = new GamePresenceSnapshot(
            ProcessPresent: false,
            WindowPresent: false,
            ProcessId: null,
            InstanceKey: null);

        var first = machine.Apply(new PresenceCheckInput(absent, Timestamp: 2_000));
        var second = machine.Apply(new PresenceCheckInput(absent, Timestamp: 4_000));

        Assert.Equal(ConnectionState.Online, first.Current.ConnectionState);
        Assert.Empty(first.Events.OfType<NoGameConfirmed>());
        var noGame = Assert.Single(second.Events.OfType<NoGameConfirmed>());
        Assert.Equal(first.Current.ActiveCycleId, noGame.PriorCycleId);
        Assert.Equal(ConnectionState.NoGame, second.Current.ConnectionState);
        Assert.Equal(LifeState.Unknown, second.Current.LifeState);
        Assert.Null(second.Current.ActiveCycleId);
        Assert.Equal(ActiveCycleStatus.None, second.Current.ActiveCycleStatus);
    }

    [Fact]
    public void NoGameTransientAbsence_DoesNotTransition()
    {
        var machine = StateMachineScenario.DeadOnline();
        var absent = new GamePresenceSnapshot(false, false, null, null);
        var present = new GamePresenceSnapshot(true, true, 42, "42:test");

        machine.Apply(new PresenceCheckInput(absent, Timestamp: 2_000));
        var recovered = machine.Apply(new PresenceCheckInput(present, Timestamp: 3_000));
        var nextAbsence = machine.Apply(new PresenceCheckInput(absent, Timestamp: 5_000));

        Assert.Equal(ConnectionState.Online, recovered.Current.ConnectionState);
        Assert.Null(recovered.Current.FirstAbsentPresenceTimestamp);
        Assert.Equal(0, recovered.Current.ConsecutiveAbsentPresenceChecks);
        Assert.Equal(ConnectionState.Online, nextAbsence.Current.ConnectionState);
        Assert.Empty(nextAbsence.Events.OfType<NoGameConfirmed>());
        Assert.Equal(1, nextAbsence.Current.ConsecutiveAbsentPresenceChecks);
    }
}
