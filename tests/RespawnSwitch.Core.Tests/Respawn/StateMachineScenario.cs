using RespawnSwitch.Core.Game;
using RespawnSwitch.Core.Respawn;

namespace RespawnSwitch.Core.Tests.Respawn;

internal static class StateMachineScenario
{
    public static RespawnStateMachine DeadOnline(
        long timestampFrequency = 1_000) =>
        new(
            RespawnMachineState.CreateForTest(
                lifeState: LifeState.Dead,
                lastConfirmedLifeState: LifeState.Dead,
                connectionState: ConnectionState.Online,
                activeCycleStatus: ActiveCycleStatus.Active,
                activeCycleId: new RespawnCycleId(
                    Guid.Parse("00000000-0000-0000-0000-000000000001")),
                lastSuccessfulSampleTimestamp: 1_000,
                staleSinceTimestamp: null),
            Options(timestampFrequency));

    public static RespawnStateMachine DeadStale(
        long staleSinceTimestamp,
        long timestampFrequency) =>
        new(
            RespawnMachineState.CreateForTest(
                lifeState: LifeState.Dead,
                lastConfirmedLifeState: LifeState.Dead,
                connectionState: ConnectionState.Stale,
                activeCycleStatus: ActiveCycleStatus.Active,
                activeCycleId: new RespawnCycleId(
                    Guid.Parse("00000000-0000-0000-0000-000000000001")),
                lastSuccessfulSampleTimestamp: 0,
                staleSinceTimestamp: staleSinceTimestamp),
            Options(timestampFrequency));

    public static RespawnStateMachine FromState(
        RespawnMachineState state,
        long timestampFrequency = 1_000) =>
        new(state, Options(timestampFrequency));

    public static GameSample Sample(
        bool isDead,
        long timestamp = 2_000,
        double? verifiedTimerSeconds = null,
        string riotId = "Player#NA1",
        string timelineKey = "test-timeline",
        double gameTimeSeconds = 101,
        double? rawTimer = null) =>
        new(
            SampleId: timestamp,
            ObservedAtTimestamp: timestamp,
            RiotId: riotId,
            IsDead: isDead,
            RespawnTimerRaw: rawTimer ?? verifiedTimerSeconds,
            RespawnTimerSeconds: verifiedTimerSeconds,
            GameTimeSeconds: gameTimeSeconds,
            GameMode: "PRACTICETOOL",
            SchemaSource: SchemaSource.PlayerList,
            TimelineKey: timelineKey);

    private static RespawnStateMachineOptions Options(long frequency) =>
        new(
            StaleAfter: TimeSpan.FromSeconds(1),
            AbandonDeadCycleAfter: TimeSpan.FromSeconds(5),
            NoGameConfirmationSpan: TimeSpan.FromSeconds(2),
            AttachmentThresholdSeconds: 2.0,
            TimestampFrequency: frequency);
}
