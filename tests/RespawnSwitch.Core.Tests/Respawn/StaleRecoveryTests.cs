using RespawnSwitch.Core.Game;
using RespawnSwitch.Core.Respawn;

namespace RespawnSwitch.Core.Tests.Respawn;

public sealed class StaleRecoveryTests
{
    private static readonly RespawnCycleId OriginalCycle = new(
        Guid.Parse("00000000-0000-0000-0000-000000000001"));

    [Fact]
    public void ProbeFailure_WhileDead_DoesNotPublishRespawnOrChangeLifeState()
    {
        var machine = StateMachineScenario.DeadOnline();
        var transition = machine.Apply(new ProbeFailureInput(
            new ProbeFailure(
                ProbeFailureKind.Timeout,
                "riot.timeout",
                "连接超时",
                ObservedAtTimestamp: 2_000)));

        Assert.Equal(LifeState.Dead, transition.Current.LifeState);
        Assert.Equal(LifeState.Dead, transition.Current.LastConfirmedLifeState);
        Assert.DoesNotContain(
            transition.Events,
            item => item is RespawnConfirmed);
    }

    [Fact]
    public void OnlineBecomesStaleOnlyWhenElapsedTimeIsGreaterThanOneSecond()
    {
        var machine = StateMachineScenario.DeadOnline();

        var boundary = machine.Apply(new ProbeFailureInput(Failure(2_000)));
        var beyond = machine.Apply(new ProbeFailureInput(Failure(2_001)));

        Assert.Equal(ConnectionState.Online, boundary.Current.ConnectionState);
        Assert.Empty(boundary.Events.OfType<ConnectionBecameStale>());
        Assert.Equal(ConnectionState.Stale, beyond.Current.ConnectionState);
        Assert.Single(beyond.Events.OfType<ConnectionBecameStale>());
        Assert.Equal(2_001, beyond.Current.StaleSinceTimestamp);
        Assert.Equal(LifeState.Dead, beyond.Current.LastConfirmedLifeState);
    }

    [Fact]
    public void StaleRecovery_LastAliveThenDead_CreatesLateCycle()
    {
        var machine = StateMachineScenario.FromState(RespawnMachineState.CreateForTest(
            lifeState: LifeState.Alive,
            lastConfirmedLifeState: LifeState.Alive,
            connectionState: ConnectionState.Stale,
            activeCycleStatus: ActiveCycleStatus.None,
            activeCycleId: null,
            lastSuccessfulSampleTimestamp: 1_000,
            staleSinceTimestamp: 2_001));

        var transition = machine.Apply(new SuccessfulSampleInput(
            StateMachineScenario.Sample(
                isDead: true,
                timestamp: 3_000,
                verifiedTimerSeconds: 3.0)));

        Assert.Single(transition.Events.OfType<ConnectionRestored>());
        var death = Assert.Single(transition.Events.OfType<DeathConfirmed>());
        Assert.True(death.IsLateDiscovery);
        Assert.Single(transition.Events.OfType<AttachmentRequested>());
        Assert.Equal(LifeState.Dead, transition.Current.LifeState);
        Assert.Equal(ActiveCycleStatus.Active, transition.Current.ActiveCycleStatus);
    }

    [Fact]
    public void StaleRecovery_LastDeadThenAlive_CleansOriginalCycle()
    {
        var machine = DeadStale(ActiveCycleStatus.Active, attachmentIssued: true);

        var transition = machine.Apply(new SuccessfulSampleInput(
            StateMachineScenario.Sample(isDead: false, timestamp: 3_000)));

        Assert.Single(transition.Events.OfType<ConnectionRestored>());
        var respawn = Assert.Single(transition.Events.OfType<RespawnConfirmed>());
        Assert.Equal(OriginalCycle, respawn.CycleId);
        Assert.Equal(ActiveCycleStatus.Completed, transition.Current.ActiveCycleStatus);
        Assert.Equal(LifeState.Alive, transition.Current.LifeState);
    }

    [Fact]
    public void StaleRecovery_LastDeadThenDead_DoesNotReplayAttachment()
    {
        var machine = DeadStale(ActiveCycleStatus.Active, attachmentIssued: true);

        var transition = machine.Apply(new SuccessfulSampleInput(
            StateMachineScenario.Sample(
                isDead: true,
                timestamp: 3_000,
                verifiedTimerSeconds: 5.0)));

        Assert.Single(transition.Events.OfType<ConnectionRestored>());
        Assert.Single(transition.Events.OfType<DeadSampleUpdated>());
        Assert.Empty(transition.Events.OfType<AttachmentRequested>());
        Assert.Equal(OriginalCycle, transition.Current.ActiveCycleId);
        Assert.True(transition.Current.AttachmentIssued);
    }

    [Fact]
    public void StaleRecovery_AbandonedDead_UpdatesWithoutReopening()
    {
        var machine = DeadStale(
            ActiveCycleStatus.AbandonedUnknown,
            attachmentIssued: true);

        var transition = machine.Apply(new SuccessfulSampleInput(
            StateMachineScenario.Sample(
                isDead: true,
                timestamp: 8_000,
                verifiedTimerSeconds: 5.0)));

        Assert.Single(transition.Events.OfType<ConnectionRestored>());
        Assert.Single(transition.Events.OfType<DeadSampleUpdated>());
        Assert.Empty(transition.Events.OfType<DeathConfirmed>());
        Assert.Empty(transition.Events.OfType<AttachmentRequested>());
        Assert.Equal(ActiveCycleStatus.AbandonedUnknown, transition.Current.ActiveCycleStatus);
        Assert.Equal(LifeState.Dead, transition.Current.LifeState);
    }

    private static RespawnStateMachine DeadStale(
        ActiveCycleStatus status,
        bool attachmentIssued) =>
        StateMachineScenario.FromState(RespawnMachineState.CreateForTest(
            lifeState: LifeState.Dead,
            lastConfirmedLifeState: LifeState.Dead,
            connectionState: ConnectionState.Stale,
            activeCycleStatus: status,
            activeCycleId: OriginalCycle,
            lastSuccessfulSampleTimestamp: 1_000,
            staleSinceTimestamp: 2_001,
            attachmentIssued: attachmentIssued));

    private static ProbeFailure Failure(long timestamp) =>
        new(
            ProbeFailureKind.Timeout,
            "riot.timeout",
            "连接超时",
            timestamp);
}
