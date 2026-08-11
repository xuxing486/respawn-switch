namespace RespawnSwitch.Core.Respawn;

public sealed record RespawnMachineState(
    LifeState LifeState,
    LifeState LastConfirmedLifeState,
    ConnectionState ConnectionState,
    RespawnCycleId? ActiveCycleId,
    ActiveCycleStatus ActiveCycleStatus,
    bool AttachmentIssued,
    long? LastSuccessfulSampleTimestamp,
    long? StaleSinceTimestamp,
    long? FirstAbsentPresenceTimestamp,
    int ConsecutiveAbsentPresenceChecks,
    string? RiotId,
    string? TimelineKey,
    double? LastGameTimeSeconds)
{
    internal static RespawnMachineState CreateForTest(
        LifeState lifeState,
        LifeState lastConfirmedLifeState,
        ConnectionState connectionState,
        ActiveCycleStatus activeCycleStatus,
        RespawnCycleId? activeCycleId,
        long? lastSuccessfulSampleTimestamp,
        long? staleSinceTimestamp,
        bool attachmentIssued = false,
        long? firstAbsentPresenceTimestamp = null,
        int consecutiveAbsentPresenceChecks = 0,
        string? riotId = "Player#NA1",
        string? timelineKey = "test-timeline",
        double? lastGameTimeSeconds = 100)
    {
        if (!Enum.IsDefined(lifeState))
        {
            throw new ArgumentOutOfRangeException(nameof(lifeState));
        }

        if (!Enum.IsDefined(lastConfirmedLifeState))
        {
            throw new ArgumentOutOfRangeException(nameof(lastConfirmedLifeState));
        }

        if (!Enum.IsDefined(connectionState))
        {
            throw new ArgumentOutOfRangeException(nameof(connectionState));
        }

        if (!Enum.IsDefined(activeCycleStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(activeCycleStatus));
        }

        var hasCycle = activeCycleId is not null;
        if ((activeCycleStatus == ActiveCycleStatus.None) == hasCycle)
        {
            throw new ArgumentException(
                "Cycle identity and cycle status must agree.",
                nameof(activeCycleId));
        }

        if (attachmentIssued && !hasCycle)
        {
            throw new ArgumentException(
                "An attachment cannot be issued without a cycle.",
                nameof(attachmentIssued));
        }

        if (connectionState == ConnectionState.Stale && staleSinceTimestamp is null)
        {
            throw new ArgumentException(
                "A stale connection requires its starting timestamp.",
                nameof(staleSinceTimestamp));
        }

        if (connectionState != ConnectionState.Stale && staleSinceTimestamp is not null)
        {
            throw new ArgumentException(
                "Only a stale connection can have a stale starting timestamp.",
                nameof(staleSinceTimestamp));
        }

        if (consecutiveAbsentPresenceChecks < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(consecutiveAbsentPresenceChecks));
        }

        if ((consecutiveAbsentPresenceChecks == 0) !=
            (firstAbsentPresenceTimestamp is null))
        {
            throw new ArgumentException(
                "Presence absence count and starting timestamp must agree.",
                nameof(firstAbsentPresenceTimestamp));
        }

        return new RespawnMachineState(
            lifeState,
            lastConfirmedLifeState,
            connectionState,
            activeCycleId,
            activeCycleStatus,
            attachmentIssued,
            lastSuccessfulSampleTimestamp,
            staleSinceTimestamp,
            firstAbsentPresenceTimestamp,
            consecutiveAbsentPresenceChecks,
            riotId,
            timelineKey,
            lastGameTimeSeconds);
    }
}
