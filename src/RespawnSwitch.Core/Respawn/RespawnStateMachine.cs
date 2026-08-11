using RespawnSwitch.Core.Game;

namespace RespawnSwitch.Core.Respawn;

public sealed class RespawnStateMachine
{
    private readonly RespawnStateMachineOptions _options;

    public RespawnStateMachine(RespawnStateMachineOptions options)
        : this(CreateInitialState(), options)
    {
    }

    internal RespawnStateMachine(
        RespawnMachineState initialState,
        RespawnStateMachineOptions options)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        ArgumentNullException.ThrowIfNull(options);

        ValidateOptions(options);
        State = initialState;
        _options = options;
    }

    public RespawnMachineState State { get; private set; }

    public RespawnTransition Apply(StateMachineInput input) => input switch
    {
        SuccessfulSampleInput sample => ApplySuccessfulSample(sample),
        ProbeFailureInput failure => ApplyProbeFailure(failure),
        PresenceCheckInput presence => ApplyPresence(presence),
        TimePulseInput pulse => ApplyTimePulse(pulse),
        _ => throw new ArgumentOutOfRangeException(nameof(input))
    };

    private RespawnTransition ApplySuccessfulSample(SuccessfulSampleInput input)
    {
        ArgumentNullException.ThrowIfNull(input.Sample);

        var previous = State;
        var events = new List<RespawnDomainEvent>();
        var current = previous;
        var resetReason = FindTimelineResetReason(previous, input.Sample);

        if (resetReason is not null)
        {
            events.Add(new TimelineResetRequested(
                previous.ActiveCycleId,
                resetReason,
                input.ObservedAtTimestamp));
            current = current with
            {
                LifeState = LifeState.Unknown,
                LastConfirmedLifeState = LifeState.Unknown,
                ActiveCycleId = null,
                ActiveCycleStatus = ActiveCycleStatus.None,
                AttachmentIssued = false,
                RiotId = null,
                TimelineKey = null,
                LastGameTimeSeconds = null
            };
        }

        var recoveredFromStale = current.ConnectionState == ConnectionState.Stale;
        current = RestoreConnection(current, input.ObservedAtTimestamp, events);
        current = ApplyLifeSample(
            current,
            input.Sample,
            recoveredFromStale && resetReason is null,
            events);

        current = current with
        {
            LastSuccessfulSampleTimestamp = input.ObservedAtTimestamp,
            RiotId = input.Sample.RiotId,
            TimelineKey = input.Sample.TimelineKey,
            LastGameTimeSeconds = input.Sample.GameTimeSeconds
        };

        return Commit(previous, current, events);
    }

    private RespawnTransition ApplyProbeFailure(ProbeFailureInput input)
    {
        ArgumentNullException.ThrowIfNull(input.Failure);

        var previous = State;
        var events = new List<RespawnDomainEvent>();
        var current = MakeStaleIfDue(previous, input.ObservedAtTimestamp, events);
        current = AbandonDeadCycleIfDue(current, input.ObservedAtTimestamp, events);
        return Commit(previous, current, events);
    }

    private RespawnTransition ApplyPresence(PresenceCheckInput input)
    {
        ArgumentNullException.ThrowIfNull(input.Presence);

        var previous = State;
        var events = new List<RespawnDomainEvent>();
        var current = previous;
        var isAbsent = !input.Presence.ProcessPresent && !input.Presence.WindowPresent;

        if (!isAbsent)
        {
            current = current with
            {
                FirstAbsentPresenceTimestamp = null,
                ConsecutiveAbsentPresenceChecks = 0
            };
            return Commit(previous, current, events);
        }

        if (current.ConnectionState == ConnectionState.NoGame)
        {
            return Commit(previous, current, events);
        }

        if (current.FirstAbsentPresenceTimestamp is not { } absentSince)
        {
            current = current with
            {
                FirstAbsentPresenceTimestamp = input.ObservedAtTimestamp,
                ConsecutiveAbsentPresenceChecks = 1
            };
            return Commit(previous, current, events);
        }

        var absenceCount = current.ConsecutiveAbsentPresenceChecks + 1;
        current = current with { ConsecutiveAbsentPresenceChecks = absenceCount };
        if (absenceCount < 2 ||
            !HasElapsedAtLeast(
                absentSince,
                input.ObservedAtTimestamp,
                _options.NoGameConfirmationSpan))
        {
            return Commit(previous, current, events);
        }

        events.Add(new NoGameConfirmed(
            current.ActiveCycleId,
            input.ObservedAtTimestamp));
        current = current with
        {
            LifeState = LifeState.Unknown,
            LastConfirmedLifeState = LifeState.Unknown,
            ConnectionState = ConnectionState.NoGame,
            ActiveCycleId = null,
            ActiveCycleStatus = ActiveCycleStatus.None,
            AttachmentIssued = false,
            LastSuccessfulSampleTimestamp = null,
            StaleSinceTimestamp = null,
            FirstAbsentPresenceTimestamp = null,
            ConsecutiveAbsentPresenceChecks = 0,
            RiotId = null,
            TimelineKey = null,
            LastGameTimeSeconds = null
        };

        return Commit(previous, current, events);
    }

    private RespawnTransition ApplyTimePulse(TimePulseInput input)
    {
        var previous = State;
        var events = new List<RespawnDomainEvent>();
        var current = MakeStaleIfDue(previous, input.ObservedAtTimestamp, events);
        current = AbandonDeadCycleIfDue(current, input.ObservedAtTimestamp, events);
        return Commit(previous, current, events);
    }

    private RespawnMachineState RestoreConnection(
        RespawnMachineState state,
        long timestamp,
        ICollection<RespawnDomainEvent> events)
    {
        if (state.ConnectionState == ConnectionState.Stale)
        {
            events.Add(new ConnectionRestored(timestamp));
        }

        return state with
        {
            ConnectionState = ConnectionState.Online,
            StaleSinceTimestamp = null,
            FirstAbsentPresenceTimestamp = null,
            ConsecutiveAbsentPresenceChecks = 0
        };
    }

    private RespawnMachineState ApplyLifeSample(
        RespawnMachineState state,
        GameSample sample,
        bool recoveredFromStale,
        ICollection<RespawnDomainEvent> events)
    {
        return sample.IsDead
            ? ApplyDeadSample(state, sample, recoveredFromStale, events)
            : ApplyAliveSample(state, sample, events);
    }

    private RespawnMachineState ApplyDeadSample(
        RespawnMachineState state,
        GameSample sample,
        bool recoveredFromStale,
        ICollection<RespawnDomainEvent> events)
    {
        if (state.LifeState is LifeState.Unknown or LifeState.Alive)
        {
            var cycleId = RespawnCycleId.New();
            var isLateDiscovery =
                recoveredFromStale && state.LastConfirmedLifeState == LifeState.Alive;
            events.Add(new DeathConfirmed(cycleId, sample, isLateDiscovery));

            var created = state with
            {
                LifeState = LifeState.Dead,
                LastConfirmedLifeState = LifeState.Dead,
                ActiveCycleId = cycleId,
                ActiveCycleStatus = ActiveCycleStatus.Active,
                AttachmentIssued = false
            };
            return ApplyAttachmentPolicy(created, sample, events);
        }

        var current = state with
        {
            LifeState = LifeState.Dead,
            LastConfirmedLifeState = LifeState.Dead
        };

        if (current.ActiveCycleId is { } existingCycle)
        {
            events.Add(new DeadSampleUpdated(existingCycle, sample));
        }

        return current.ActiveCycleStatus == ActiveCycleStatus.Active
            ? ApplyAttachmentPolicy(current, sample, events)
            : current;
    }

    private RespawnMachineState ApplyAliveSample(
        RespawnMachineState state,
        GameSample sample,
        ICollection<RespawnDomainEvent> events)
    {
        if (state.LifeState == LifeState.Unknown)
        {
            events.Add(new LifeStateSynchronized(
                LifeState.Alive,
                sample.ObservedAtTimestamp));
            return state with
            {
                LifeState = LifeState.Alive,
                LastConfirmedLifeState = LifeState.Alive,
                ActiveCycleId = null,
                ActiveCycleStatus = ActiveCycleStatus.None,
                AttachmentIssued = false
            };
        }

        if (state.LifeState == LifeState.Dead &&
            state.ActiveCycleStatus == ActiveCycleStatus.Active &&
            state.ActiveCycleId is { } cycleId)
        {
            events.Add(new RespawnConfirmed(cycleId, sample));
            return state with
            {
                LifeState = LifeState.Alive,
                LastConfirmedLifeState = LifeState.Alive,
                ActiveCycleStatus = ActiveCycleStatus.Completed
            };
        }

        if (state.LifeState == LifeState.Dead)
        {
            events.Add(new LifeStateSynchronized(
                LifeState.Alive,
                sample.ObservedAtTimestamp));
            return state with
            {
                LifeState = LifeState.Alive,
                LastConfirmedLifeState = LifeState.Alive,
                ActiveCycleStatus = state.ActiveCycleId is null
                    ? ActiveCycleStatus.None
                    : ActiveCycleStatus.Completed
            };
        }

        return state with
        {
            LifeState = LifeState.Alive,
            LastConfirmedLifeState = LifeState.Alive
        };
    }

    private RespawnMachineState ApplyAttachmentPolicy(
        RespawnMachineState state,
        GameSample sample,
        ICollection<RespawnDomainEvent> events)
    {
        if (state.ActiveCycleId is not { } cycleId)
        {
            return state;
        }

        var decision = AttachmentPolicy.Evaluate(
            sample.RespawnTimerSeconds,
            state.AttachmentIssued,
            _options.AttachmentThresholdSeconds);
        if (decision != AttachmentDecision.AttachOnce)
        {
            return state;
        }

        events.Add(new AttachmentRequested(cycleId, sample));
        return state with { AttachmentIssued = true };
    }

    private RespawnMachineState MakeStaleIfDue(
        RespawnMachineState state,
        long timestamp,
        ICollection<RespawnDomainEvent> events)
    {
        if (state.ConnectionState != ConnectionState.Online ||
            state.LastSuccessfulSampleTimestamp is not { } lastSuccess ||
            !HasElapsedMoreThan(lastSuccess, timestamp, _options.StaleAfter))
        {
            return state;
        }

        events.Add(new ConnectionBecameStale(timestamp));
        return state with
        {
            ConnectionState = ConnectionState.Stale,
            StaleSinceTimestamp = timestamp
        };
    }

    private RespawnMachineState AbandonDeadCycleIfDue(
        RespawnMachineState state,
        long timestamp,
        ICollection<RespawnDomainEvent> events)
    {
        if (state.ConnectionState != ConnectionState.Stale ||
            state.LifeState != LifeState.Dead ||
            state.ActiveCycleStatus != ActiveCycleStatus.Active ||
            state.ActiveCycleId is not { } cycleId ||
            state.StaleSinceTimestamp is not { } staleSince ||
            !HasElapsedAtLeast(
                staleSince,
                timestamp,
                _options.AbandonDeadCycleAfter))
        {
            return state;
        }

        events.Add(new AbandonCycleDueToUnknown(cycleId, timestamp));
        return state with { ActiveCycleStatus = ActiveCycleStatus.AbandonedUnknown };
    }

    private bool HasElapsedMoreThan(long start, long end, TimeSpan duration) =>
        end >= start && (end - start) > duration.TotalSeconds * _options.TimestampFrequency;

    private bool HasElapsedAtLeast(long start, long end, TimeSpan duration) =>
        end >= start && (end - start) >= duration.TotalSeconds * _options.TimestampFrequency;

    private static string? FindTimelineResetReason(
        RespawnMachineState state,
        GameSample sample)
    {
        if (state.RiotId is { } riotId &&
            !string.Equals(riotId, sample.RiotId, StringComparison.Ordinal))
        {
            return "riot-id-changed";
        }

        if (state.TimelineKey is { } timelineKey &&
            !string.Equals(timelineKey, sample.TimelineKey, StringComparison.Ordinal))
        {
            return "timeline-key-changed";
        }

        if (state.LastGameTimeSeconds is { } gameTime &&
            sample.GameTimeSeconds < gameTime)
        {
            return "game-time-rollback";
        }

        return null;
    }

    private RespawnTransition Commit(
        RespawnMachineState previous,
        RespawnMachineState current,
        List<RespawnDomainEvent> events)
    {
        State = current;
        return new RespawnTransition(previous, current, events.AsReadOnly());
    }

    private static RespawnMachineState CreateInitialState() =>
        new(
            LifeState.Unknown,
            LifeState.Unknown,
            ConnectionState.NoGame,
            ActiveCycleId: null,
            ActiveCycleStatus.None,
            AttachmentIssued: false,
            LastSuccessfulSampleTimestamp: null,
            StaleSinceTimestamp: null,
            FirstAbsentPresenceTimestamp: null,
            ConsecutiveAbsentPresenceChecks: 0,
            RiotId: null,
            TimelineKey: null,
            LastGameTimeSeconds: null);

    private static void ValidateOptions(RespawnStateMachineOptions options)
    {
        if (options.StaleAfter < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        if (options.AbandonDeadCycleAfter < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        if (options.NoGameConfirmationSpan < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        if (!double.IsFinite(options.AttachmentThresholdSeconds) ||
            options.AttachmentThresholdSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        if (options.TimestampFrequency <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
    }
}
