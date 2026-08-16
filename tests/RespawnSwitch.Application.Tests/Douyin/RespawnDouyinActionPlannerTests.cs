using RespawnSwitch.Application.Douyin;

namespace RespawnSwitch.Application.Tests.Douyin;

public sealed class RespawnDouyinActionPlannerTests
{
    [Fact]
    public void Plan_FoundDesktopCandidate_AttachesDesktop()
    {
        var candidate = Candidate();
        var plan = RespawnDouyinActionPlanner.Plan(Found(candidate), new(DouyinDiscoveryMode.Auto, true));

        Assert.Equal(DouyinLaunchMode.Desktop, plan.Mode);
        Assert.Equal(candidate, plan.DesktopCandidate);
    }

    [Theory]
    [InlineData(DouyinDiscoveryStatus.Scanning)]
    [InlineData(DouyinDiscoveryStatus.NotFound)]
    public void Plan_NoDesktopWhileFallbackEnabled_OpensWeb(DouyinDiscoveryStatus status)
    {
        var result = new DouyinDiscoveryResult(status, null, [], DouyinScanProgress.Empty, "test");
        Assert.Equal(DouyinLaunchMode.Web, RespawnDouyinActionPlanner.Plan(result, new(DouyinDiscoveryMode.Auto, true)).Mode);
    }

    [Fact]
    public void Plan_FallbackDisabled_CountdownOnly()
    {
        var result = new DouyinDiscoveryResult(DouyinDiscoveryStatus.NotFound, null, [], DouyinScanProgress.Empty, "test");
        Assert.Equal(DouyinLaunchMode.Unavailable, RespawnDouyinActionPlanner.Plan(result, new(DouyinDiscoveryMode.Auto, false)).Mode);
    }

    private static DouyinCandidate Candidate() =>
        new(@"D:\Douyin\douyin.exe", DouyinDiscoverySource.FullDisk, false, true, "ABC", new Version(1, 0), DateTimeOffset.UnixEpoch, "Douyin", "Douyin");

    private static DouyinDiscoveryResult Found(DouyinCandidate candidate) =>
        new(DouyinDiscoveryStatus.Found, candidate, [candidate], DouyinScanProgress.Empty, "found");
}
