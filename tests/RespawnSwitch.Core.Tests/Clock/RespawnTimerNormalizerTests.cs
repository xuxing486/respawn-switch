using RespawnSwitch.Core.Clock;

namespace RespawnSwitch.Core.Tests.Clock;

public sealed class RespawnTimerNormalizerTests
{
    [Fact]
    public void TryNormalize_UnverifiedSemantics_ReturnsFalse()
    {
        var normalizer = new RespawnTimerNormalizer();
        var semantics = new RespawnTimerSemantics(
            TimerSemanticStatus.Unverified,
            PatchLabel: "16.15",
            SecondsPerRawUnit: 1.0,
            EvidenceReportId: "none");

        Assert.False(normalizer.TryNormalize(10.0, semantics, out _));
    }

    [Theory]
    [MemberData(nameof(InvalidRawValues))]
    public void TryNormalize_VerifiedSemanticsWithInvalidRaw_ReturnsFalse(double? raw)
    {
        var normalizer = new RespawnTimerNormalizer();
        var semantics = new RespawnTimerSemantics(
            TimerSemanticStatus.VerifiedForCurrentPatch,
            PatchLabel: "16.15",
            SecondsPerRawUnit: 1.0,
            EvidenceReportId: "probe-16.15-01");

        Assert.False(normalizer.TryNormalize(raw, semantics, out _));
    }

    [Fact]
    public void TryNormalize_VerifiedFiniteNonNegativeValue_ConvertsToSeconds()
    {
        var normalizer = new RespawnTimerNormalizer();
        var semantics = new RespawnTimerSemantics(
            TimerSemanticStatus.VerifiedForCurrentPatch,
            PatchLabel: "16.15",
            SecondsPerRawUnit: 0.5,
            EvidenceReportId: "probe-16.15-01");

        var normalized = normalizer.TryNormalize(4.0, semantics, out var seconds);

        Assert.True(normalized);
        Assert.Equal(2.0, seconds);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-0.01)]
    public void TryNormalize_VerifiedInvalidMultiplier_ReturnsFalse(double multiplier)
    {
        var normalizer = new RespawnTimerNormalizer();
        var semantics = new RespawnTimerSemantics(
            TimerSemanticStatus.VerifiedForCurrentPatch,
            PatchLabel: "16.15",
            SecondsPerRawUnit: multiplier,
            EvidenceReportId: "probe-16.15-01");

        Assert.False(normalizer.TryNormalize(4.0, semantics, out _));
    }

    [Fact]
    public void TryNormalize_ProductIsInfinite_ReturnsFalse()
    {
        var normalizer = new RespawnTimerNormalizer();
        var semantics = new RespawnTimerSemantics(
            TimerSemanticStatus.VerifiedForCurrentPatch,
            PatchLabel: "16.15",
            SecondsPerRawUnit: double.MaxValue,
            EvidenceReportId: "probe-16.15-01");

        Assert.False(normalizer.TryNormalize(2.0, semantics, out _));
    }

    public static TheoryData<double?> InvalidRawValues => new()
    {
        null,
        double.NaN,
        double.PositiveInfinity,
        double.NegativeInfinity,
        -0.01
    };
}
