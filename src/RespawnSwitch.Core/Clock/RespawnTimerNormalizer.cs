namespace RespawnSwitch.Core.Clock;

public sealed class RespawnTimerNormalizer
{
    public bool TryNormalize(
        double? raw,
        RespawnTimerSemantics semantics,
        out double seconds)
    {
        seconds = default;

        if (semantics.Status != TimerSemanticStatus.VerifiedForCurrentPatch ||
            raw is not { } rawValue ||
            !double.IsFinite(rawValue) ||
            rawValue < 0 ||
            !double.IsFinite(semantics.SecondsPerRawUnit) ||
            semantics.SecondsPerRawUnit < 0)
        {
            return false;
        }

        var normalizedSeconds = rawValue * semantics.SecondsPerRawUnit;
        if (!double.IsFinite(normalizedSeconds))
        {
            return false;
        }

        seconds = normalizedSeconds;
        return true;
    }
}
