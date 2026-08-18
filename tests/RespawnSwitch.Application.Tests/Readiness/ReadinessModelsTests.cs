using RespawnSwitch.Application.Readiness;

namespace RespawnSwitch.Application.Tests.Readiness;

public sealed class ReadinessModelsTests
{
    [Fact]
    public void Aggregate_AttributesBlockingIssueToItsSubsystem()
    {
        var result = ProductReadiness.Aggregate([
            ComponentReadiness.Ready(Subsystem.LeagueClient, "客户端已检测"),
            ComponentReadiness.Blocked(Subsystem.DouyinWeb, "web.no-tab", "未打开抖音网页", "请打开抖音视频")
        ]);

        Assert.Equal(ReadinessLevel.Blocked, result.Level);
        Assert.Equal(Subsystem.DouyinWeb, Assert.Single(result.Issues).Subsystem);
        Assert.Equal("web.no-tab", result.Issues[0].Code);
    }

    [Fact]
    public void Aggregate_AllReady_IsPrematchReady()
    {
        var result = ProductReadiness.Aggregate([
            ComponentReadiness.Ready(Subsystem.LeagueClient, "客户端已检测"),
            ComponentReadiness.Ready(Subsystem.MediaControl, "Play/Pause 可用"),
            ComponentReadiness.Ready(Subsystem.Overlay, "悬浮层可用")
        ]);

        Assert.Equal(ReadinessLevel.Ready, result.Level);
        Assert.Empty(result.Issues);
    }
}
