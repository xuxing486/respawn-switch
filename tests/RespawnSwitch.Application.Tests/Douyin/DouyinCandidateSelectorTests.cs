using RespawnSwitch.Application.Douyin;

namespace RespawnSwitch.Application.Tests.Douyin;

public sealed class DouyinCandidateSelectorTests
{
    [Fact]
    public void Select_RunningCandidateWinsOverSavedAndFullDisk()
    {
        var running = Candidate(@"D:\Apps\Douyin\douyin.exe", DouyinDiscoverySource.RunningProcess, isRunning: true);

        var result = DouyinCandidateSelector.Select([
            Candidate(@"C:\Saved\douyin.exe", DouyinDiscoverySource.SavedPath),
            Candidate(@"E:\Found\douyin.exe", DouyinDiscoverySource.FullDisk),
            running
        ]);

        Assert.Equal(DouyinDiscoveryStatus.Found, result.Status);
        Assert.Equal(running.NormalizedPath, result.Selected?.NormalizedPath);
    }

    [Fact]
    public void Select_TwoDifferentRunningCandidatesWithSameRank_ReturnsAmbiguous()
    {
        var result = DouyinCandidateSelector.Select([
            Candidate(@"C:\Apps\Douyin\douyin.exe", DouyinDiscoverySource.RunningProcess, isRunning: true),
            Candidate(@"D:\Apps\Douyin\douyin.exe", DouyinDiscoverySource.RunningProcess, isRunning: true)
        ]);

        Assert.Equal(DouyinDiscoveryStatus.Ambiguous, result.Status);
        Assert.Null(result.Selected);
        Assert.Equal(2, result.Candidates.Count);
    }

    [Fact]
    public void Select_SameNormalizedPathDifferentCase_IsDeduplicated()
    {
        var result = DouyinCandidateSelector.Select([
            Candidate(@"C:\Apps\Douyin\douyin.exe", DouyinDiscoverySource.FullDisk),
            Candidate(@"c:\apps\douyin\DOUYIN.EXE", DouyinDiscoverySource.RunningProcess, isRunning: true)
        ]);

        Assert.Equal(DouyinDiscoveryStatus.Found, result.Status);
        Assert.Single(result.Candidates);
        Assert.True(result.Selected?.IsRunning);
    }

    [Fact]
    public void Select_FullDiskCandidates_SelectsNewerVersion()
    {
        var result = DouyinCandidateSelector.Select([
            Candidate(@"C:\Apps\Douyin\douyin.exe", DouyinDiscoverySource.FullDisk, version: new Version(1, 2, 0, 0)),
            Candidate(@"D:\Apps\Douyin\douyin.exe", DouyinDiscoverySource.FullDisk, version: new Version(2, 0, 0, 0))
        ]);

        Assert.Equal(@"D:\Apps\Douyin\douyin.exe", result.Selected?.NormalizedPath);
    }

    private static DouyinCandidate Candidate(
        string path,
        DouyinDiscoverySource source,
        bool isRunning = false,
        Version? version = null) =>
        new(
            path,
            source,
            isRunning,
            HasTrustedSignature: true,
            SignatureThumbprint: "ABC",
            version ?? new Version(1, 0, 0, 0),
            DateTimeOffset.Parse("2026-08-15T00:00:00Z"),
            "Douyin",
            "Douyin Desktop");
}
