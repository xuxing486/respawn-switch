using RespawnSwitch.Core.Respawn;

namespace RespawnSwitch.Core.Tests.Respawn;

public sealed class AttachmentPolicyTests
{
    [Theory]
    [InlineData(null, false, AttachmentDecision.WaitForVerifiedTimer)]
    [InlineData(1.999, false, AttachmentDecision.CountdownOnly)]
    [InlineData(2.0, false, AttachmentDecision.AttachOnce)]
    [InlineData(9.0, true, AttachmentDecision.AlreadyIssued)]
    public void Evaluate_UsesExactTwoSecondBoundary(
        double? verifiedSeconds,
        bool attachmentIssued,
        AttachmentDecision expected)
    {
        Assert.Equal(
            expected,
            AttachmentPolicy.Evaluate(
                verifiedSeconds,
                attachmentIssued,
                thresholdSeconds: 2.0));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-0.01)]
    public void Evaluate_InvalidTimer_ReturnsWaitForVerifiedTimer(double verifiedSeconds)
    {
        Assert.Equal(
            AttachmentDecision.WaitForVerifiedTimer,
            AttachmentPolicy.Evaluate(
                verifiedSeconds,
                attachmentIssued: false,
                thresholdSeconds: 2.0));
    }
}
