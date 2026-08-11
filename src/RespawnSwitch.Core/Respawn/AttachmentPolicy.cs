namespace RespawnSwitch.Core.Respawn;

public enum AttachmentDecision
{
    WaitForVerifiedTimer,
    CountdownOnly,
    AttachOnce,
    AlreadyIssued
}

public static class AttachmentPolicy
{
    public static AttachmentDecision Evaluate(
        double? verifiedSeconds,
        bool attachmentIssued,
        double thresholdSeconds)
    {
        if (attachmentIssued)
        {
            return AttachmentDecision.AlreadyIssued;
        }

        if (verifiedSeconds is not { } seconds ||
            !double.IsFinite(seconds) ||
            seconds < 0)
        {
            return AttachmentDecision.WaitForVerifiedTimer;
        }

        return seconds < thresholdSeconds
            ? AttachmentDecision.CountdownOnly
            : AttachmentDecision.AttachOnce;
    }
}
