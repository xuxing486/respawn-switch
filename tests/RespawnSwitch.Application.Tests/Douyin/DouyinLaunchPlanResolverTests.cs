using RespawnSwitch.Application.Douyin;

namespace RespawnSwitch.Application.Tests.Douyin;

public sealed class DouyinLaunchPlanResolverTests
{
    [Fact]
    public void Resolve_FoundCandidate_UsesDesktopAndNeverWeb()
    {
        var candidate = Candidate();
        var plan = DouyinLaunchPlanResolver.Resolve(
            Result(DouyinDiscoveryStatus.Found, candidate),
            new(DouyinDiscoveryMode.Auto, OpenWebFallback: true));

        Assert.Equal(DouyinLaunchMode.Desktop, plan.Mode);
        Assert.Equal(candidate, plan.DesktopCandidate);
        Assert.Null(plan.WebUri);
    }

    [Theory]
    [InlineData(DouyinDiscoveryStatus.NotFound)]
    [InlineData(DouyinDiscoveryStatus.Scanning)]
    [InlineData(DouyinDiscoveryStatus.Ambiguous)]
    [InlineData(DouyinDiscoveryStatus.Failed)]
    public void Resolve_NoSafeDesktopAndFallbackEnabled_UsesOfficialWebsite(DouyinDiscoveryStatus status)
    {
        var plan = DouyinLaunchPlanResolver.Resolve(
            Result(status, null),
            new(DouyinDiscoveryMode.Auto, OpenWebFallback: true));

        Assert.Equal(DouyinLaunchMode.Web, plan.Mode);
        Assert.Equal(new Uri("https://www.douyin.com/"), plan.WebUri);
    }

    [Fact]
    public void Resolve_FallbackDisabled_ReturnsUnavailable()
    {
        var plan = DouyinLaunchPlanResolver.Resolve(
            Result(DouyinDiscoveryStatus.NotFound, null),
            new(DouyinDiscoveryMode.Auto, OpenWebFallback: false));

        Assert.Equal(DouyinLaunchMode.Unavailable, plan.Mode);
    }

    [Fact]
    public void Resolve_WebOnly_IgnoresDesktopCandidate()
    {
        var plan = DouyinLaunchPlanResolver.Resolve(
            Result(DouyinDiscoveryStatus.Found, Candidate()),
            new(DouyinDiscoveryMode.WebOnly, OpenWebFallback: true));

        Assert.Equal(DouyinLaunchMode.Web, plan.Mode);
        Assert.Null(plan.DesktopCandidate);
    }

    [Fact]
    public void WebFallbackCycleGuard_AllowsOnlyOnceUntilCompleted()
    {
        var guard = new WebFallbackCycleGuard();
        var cycle = RespawnSwitch.Core.Respawn.RespawnCycleId.New();

        Assert.True(guard.TryBegin(cycle));
        Assert.False(guard.TryBegin(cycle));
        guard.Complete(cycle);
        Assert.True(guard.TryBegin(cycle));
    }

    private static DouyinCandidate Candidate() =>
        new(@"D:\Douyin\douyin.exe", DouyinDiscoverySource.FullDisk, false, true, "ABC", new Version(1, 0), DateTimeOffset.UnixEpoch, "Douyin", "Douyin");

    private static DouyinDiscoveryResult Result(DouyinDiscoveryStatus status, DouyinCandidate? selected) =>
        new(status, selected, selected is null ? [] : [selected], DouyinScanProgress.Empty, "test");
}
