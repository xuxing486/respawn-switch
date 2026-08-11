using RespawnSwitch.Core.Respawn;

namespace RespawnSwitch.Core.Tests.Respawn;

public sealed class RespawnStateMachineTests
{
    [Fact]
    public void UnknownOnlineAlive_SynchronizesWithoutPublishingRespawn()
    {
        var machine = StateMachineScenario.FromState(State(
            LifeState.Unknown,
            LifeState.Unknown,
            ConnectionState.Online));

        var transition = machine.Apply(new SuccessfulSampleInput(
            StateMachineScenario.Sample(isDead: false)));

        Assert.Equal(LifeState.Alive, transition.Current.LifeState);
        Assert.Equal(LifeState.Alive, transition.Current.LastConfirmedLifeState);
        Assert.Single(transition.Events.OfType<LifeStateSynchronized>());
        Assert.Empty(transition.Events.OfType<RespawnConfirmed>());
        Assert.Empty(transition.Events.OfType<DeathConfirmed>());
    }

    [Fact]
    public void UnknownOnlineDead_CreatesCycleAndUsesAttachmentPolicy()
    {
        var machine = StateMachineScenario.FromState(State(
            LifeState.Unknown,
            LifeState.Unknown,
            ConnectionState.Online));

        var transition = machine.Apply(new SuccessfulSampleInput(
            StateMachineScenario.Sample(
                isDead: true,
                verifiedTimerSeconds: 2.0)));

        var death = Assert.Single(transition.Events.OfType<DeathConfirmed>());
        var attachment = Assert.Single(transition.Events.OfType<AttachmentRequested>());
        Assert.Equal(death.CycleId, attachment.CycleId);
        Assert.Equal(death.CycleId, transition.Current.ActiveCycleId);
        Assert.Equal(ActiveCycleStatus.Active, transition.Current.ActiveCycleStatus);
        Assert.True(transition.Current.AttachmentIssued);
    }

    [Fact]
    public void AliveOnlineDead_CreatesExactlyOneCycle()
    {
        var machine = StateMachineScenario.FromState(State(
            LifeState.Alive,
            LifeState.Alive,
            ConnectionState.Online));

        var first = machine.Apply(new SuccessfulSampleInput(
            StateMachineScenario.Sample(isDead: true, timestamp: 2_000)));
        var second = machine.Apply(new SuccessfulSampleInput(
            StateMachineScenario.Sample(isDead: true, timestamp: 2_100)));

        Assert.Single(first.Events.OfType<DeathConfirmed>());
        Assert.Empty(second.Events.OfType<DeathConfirmed>());
        Assert.Equal(first.Current.ActiveCycleId, second.Current.ActiveCycleId);
        Assert.Single(second.Events.OfType<DeadSampleUpdated>());
    }

    [Fact]
    public void DeadOnlineDead_DoesNotCreateSecondCycle()
    {
        var machine = StateMachineScenario.DeadOnline();
        var originalCycle = machine.State.ActiveCycleId;

        var transition = machine.Apply(new SuccessfulSampleInput(
            StateMachineScenario.Sample(
                isDead: true,
                verifiedTimerSeconds: 3.0)));

        Assert.Equal(originalCycle, transition.Current.ActiveCycleId);
        Assert.Empty(transition.Events.OfType<DeathConfirmed>());
        Assert.Single(transition.Events.OfType<DeadSampleUpdated>());
    }

    [Fact]
    public void DeadOnlineAlive_PublishesRespawnExactlyOnce()
    {
        var machine = StateMachineScenario.DeadOnline();

        var first = machine.Apply(new SuccessfulSampleInput(
            StateMachineScenario.Sample(isDead: false, timestamp: 2_000)));
        var second = machine.Apply(new SuccessfulSampleInput(
            StateMachineScenario.Sample(isDead: false, timestamp: 2_100)));

        Assert.Single(first.Events.OfType<RespawnConfirmed>());
        Assert.Empty(second.Events.OfType<RespawnConfirmed>());
        Assert.Equal(LifeState.Alive, second.Current.LifeState);
        Assert.Equal(ActiveCycleStatus.Completed, second.Current.ActiveCycleStatus);
    }

    [Fact]
    public void PositiveTimerAlone_CannotTriggerDeath()
    {
        var machine = StateMachineScenario.FromState(State(
            LifeState.Alive,
            LifeState.Alive,
            ConnectionState.Online));

        var transition = machine.Apply(new SuccessfulSampleInput(
            StateMachineScenario.Sample(
                isDead: false,
                rawTimer: 15.0)));

        Assert.Equal(LifeState.Alive, transition.Current.LifeState);
        Assert.Empty(transition.Events.OfType<DeathConfirmed>());
        Assert.Empty(transition.Events.OfType<AttachmentRequested>());
    }

    [Fact]
    public void IsDeadFalseWithPositiveTimer_TrustsAliveAndSuppressesTimer()
    {
        var machine = StateMachineScenario.FromState(State(
            LifeState.Alive,
            LifeState.Alive,
            ConnectionState.Online));

        var transition = machine.Apply(new SuccessfulSampleInput(
            StateMachineScenario.Sample(
                isDead: false,
                verifiedTimerSeconds: 15.0)));

        Assert.Equal(LifeState.Alive, transition.Current.LifeState);
        Assert.Equal(ActiveCycleStatus.None, transition.Current.ActiveCycleStatus);
        Assert.False(transition.Current.AttachmentIssued);
        Assert.Empty(transition.Events.OfType<DeathConfirmed>());
        Assert.Empty(transition.Events.OfType<AttachmentRequested>());
    }

    [Theory]
    [InlineData("Other#EUW", "test-timeline", 101)]
    [InlineData("Player#NA1", "test-timeline", 90)]
    [InlineData("Player#NA1", "new-timeline", 101)]
    public void NewRiotIdOrGameTimeRollback_PublishesTimelineResetNotRespawn(
        string riotId,
        string timelineKey,
        double gameTimeSeconds)
    {
        var machine = StateMachineScenario.DeadOnline();

        var transition = machine.Apply(new SuccessfulSampleInput(
            StateMachineScenario.Sample(
                isDead: false,
                riotId: riotId,
                timelineKey: timelineKey,
                gameTimeSeconds: gameTimeSeconds)));

        var reset = Assert.Single(transition.Events.OfType<TimelineResetRequested>());
        Assert.False(string.IsNullOrWhiteSpace(reset.ReasonCode));
        Assert.Empty(transition.Events.OfType<RespawnConfirmed>());
        Assert.Equal(LifeState.Alive, transition.Current.LifeState);
        Assert.Null(transition.Current.ActiveCycleId);
        Assert.Equal(ActiveCycleStatus.None, transition.Current.ActiveCycleStatus);
    }

    [Fact]
    public void CreateForTest_ActiveWithoutCycleId_Throws()
    {
        Assert.Throws<ArgumentException>(() => RespawnMachineState.CreateForTest(
            lifeState: LifeState.Dead,
            lastConfirmedLifeState: LifeState.Dead,
            connectionState: ConnectionState.Online,
            activeCycleStatus: ActiveCycleStatus.Active,
            activeCycleId: null,
            lastSuccessfulSampleTimestamp: 1_000,
            staleSinceTimestamp: null));
    }

    private static RespawnMachineState State(
        LifeState lifeState,
        LifeState lastConfirmedLifeState,
        ConnectionState connectionState) =>
        RespawnMachineState.CreateForTest(
            lifeState,
            lastConfirmedLifeState,
            connectionState,
            ActiveCycleStatus.None,
            activeCycleId: null,
            lastSuccessfulSampleTimestamp: 1_000,
            staleSinceTimestamp: null);
}
