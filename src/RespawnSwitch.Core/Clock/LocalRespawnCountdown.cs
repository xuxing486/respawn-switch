namespace RespawnSwitch.Core.Clock;

public sealed record LocalRespawnCountdownFrame(
    int DisplaySeconds,
    double RemainingSeconds,
    bool AwaitingRespawnConfirmation);

public sealed class LocalRespawnCountdown
{
    private readonly TimeProvider time;
    private readonly long anchor;
    private readonly double initialSeconds;

    private LocalRespawnCountdown(TimeProvider time, double initialSeconds)
    {
        this.time = time;
        this.initialSeconds = initialSeconds;
        anchor = time.GetTimestamp();
    }

    public static LocalRespawnCountdown Create(TimeProvider time, double initialSeconds)
    {
        ArgumentNullException.ThrowIfNull(time);
        if (!double.IsFinite(initialSeconds) || initialSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(initialSeconds));
        return new(time, initialSeconds);
    }

    public LocalRespawnCountdownFrame Snapshot()
    {
        var elapsed = Math.Max(0, time.GetElapsedTime(anchor).TotalSeconds);
        var remaining = Math.Max(0, initialSeconds - elapsed);
        return new((int)Math.Ceiling(remaining), remaining, remaining <= 0);
    }
}
