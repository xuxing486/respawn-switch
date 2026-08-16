using RespawnSwitch.Application.Douyin;
using RespawnSwitch.Windows.DouyinDiscovery;

namespace RespawnSwitch.Windows.Tests.DouyinDiscovery;

public sealed class WindowsDouyinInstallationDetectorTests
{
    [Fact]
    public async Task DetectAsync_ValidQuickCandidate_DoesNotStartFullDiskScan()
    {
        var quick = new FakeQuickSource([Candidate(@"D:\Running\douyin.exe", DouyinDiscoverySource.RunningProcess, true)]);
        var full = new FakeFullScanner([Candidate(@"C:\Found\douyin.exe", DouyinDiscoverySource.FullDisk, false)]);
        var detector = new WindowsDouyinInstallationDetector(quick, full);

        var result = await detector.DetectAsync(null, null, CancellationToken.None);

        Assert.Equal(DouyinDiscoveryStatus.Found, result.Status);
        Assert.Equal(@"D:\Running\douyin.exe", result.Selected?.NormalizedPath);
        Assert.Equal(0, full.CallCount);
    }

    [Fact]
    public async Task DetectAsync_NoQuickCandidate_ScansAllFixedRootsThroughFullScanner()
    {
        var full = new FakeFullScanner([Candidate(@"E:\Unexpected\Nested\douyin.exe", DouyinDiscoverySource.FullDisk, false)]);
        var detector = new WindowsDouyinInstallationDetector(new FakeQuickSource([]), full);

        var result = await detector.DetectAsync(null, null, CancellationToken.None);

        Assert.Equal(DouyinDiscoveryStatus.Found, result.Status);
        Assert.Equal(@"E:\Unexpected\Nested\douyin.exe", result.Selected?.NormalizedPath);
        Assert.Equal(1, full.CallCount);
    }

    [Fact]
    public async Task DetectAsync_CancelledFullScan_ReturnsCancelledNotNotFound()
    {
        var detector = new WindowsDouyinInstallationDetector(
            new FakeQuickSource([]),
            new FakeFullScanner([], cancel: true));

        var result = await detector.DetectAsync(null, null, CancellationToken.None);

        Assert.Equal(DouyinDiscoveryStatus.Cancelled, result.Status);
        Assert.Equal("douyin.discovery.cancelled", result.Code);
    }

    [Fact]
    public async Task DetectAsync_NoCandidateAnywhere_ReturnsNotFound()
    {
        var detector = new WindowsDouyinInstallationDetector(
            new FakeQuickSource([]),
            new FakeFullScanner([]));

        var result = await detector.DetectAsync(null, null, CancellationToken.None);

        Assert.Equal(DouyinDiscoveryStatus.NotFound, result.Status);
        Assert.Empty(result.Candidates);
    }

    private static DouyinCandidate Candidate(string path, DouyinDiscoverySource source, bool running) =>
        new(path, source, running, true, "ABC", new Version(1, 0), DateTimeOffset.UnixEpoch, "Douyin", "Douyin");

    private sealed class FakeQuickSource(IReadOnlyList<DouyinCandidate> candidates) : IDouyinQuickCandidateSource
    {
        public Task<IReadOnlyList<DouyinCandidate>> FindAsync(string? savedPath, CancellationToken cancellationToken) =>
            Task.FromResult(candidates);
    }

    private sealed class FakeFullScanner(IReadOnlyList<DouyinCandidate> candidates, bool cancel = false) : IDouyinFullDiskScanner
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<DouyinCandidate>> ScanAsync(
            IProgress<DouyinScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return cancel
                ? Task.FromCanceled<IReadOnlyList<DouyinCandidate>>(new CancellationToken(canceled: true))
                : Task.FromResult(candidates);
        }
    }
}
