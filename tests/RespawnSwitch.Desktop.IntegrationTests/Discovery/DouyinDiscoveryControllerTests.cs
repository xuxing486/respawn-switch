// Author: Stress Monster
using RespawnSwitch.App;
using RespawnSwitch.Application.Douyin;

namespace RespawnSwitch.Desktop.IntegrationTests.Discovery;

public sealed class DouyinDiscoveryControllerTests
{
    [Fact]
    public async Task StartAsync_ReturnsBeforeBlockedDetectionCompletes_AndPublishesResult()
    {
        var detector = new BlockingDetector();
        await using var controller = new DouyinDiscoveryController(detector);
        var changed = new List<DouyinDiscoveryResult>();
        controller.Changed += (_, result) => changed.Add(result);

        await controller.StartAsync(@"D:\Preferred\douyin.exe");

        Assert.Equal(DouyinDiscoveryStatus.Scanning, controller.CurrentResult.Status);
        await detector.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        detector.Complete(Found(@"E:\Real\douyin.exe"));
        await controller.WaitForIdleAsync();
        Assert.Equal(DouyinDiscoveryStatus.Found, controller.CurrentResult.Status);
        Assert.Contains(changed, result => result.Status == DouyinDiscoveryStatus.Found);
    }

    [Fact]
    public async Task RescanAsync_CancelsPreviousDetectionAndStartsAnother()
    {
        var detector = new SequenceDetector();
        await using var controller = new DouyinDiscoveryController(detector);
        await controller.StartAsync(null);

        await controller.RescanAsync(null);
        await detector.SecondStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(detector.FirstCancelled);
        Assert.Equal(2, detector.CallCount);
    }

    [Fact]
    public async Task Progress_BecomesImmutableCurrentSnapshot()
    {
        var detector = new ProgressDetector();
        await using var controller = new DouyinDiscoveryController(detector);

        await controller.StartAsync(null);
        await controller.WaitForIdleAsync();

        Assert.Equal(19, controller.CurrentResult.Progress.DirectoriesScanned);
        Assert.Equal("E:\\", controller.CurrentResult.Progress.CurrentDrive);
    }

    private static DouyinDiscoveryResult Found(string path)
    {
        var candidate = new DouyinCandidate(path, DouyinDiscoverySource.FullDisk, false, true, "ABC", new Version(1, 0), DateTimeOffset.UnixEpoch, "Douyin", "Douyin");
        return new(DouyinDiscoveryStatus.Found, candidate, [candidate], DouyinScanProgress.Empty, "found");
    }

    private sealed class BlockingDetector : IDouyinInstallationDetector
    {
        private readonly TaskCompletionSource<DouyinDiscoveryResult> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Complete(DouyinDiscoveryResult result) => completion.TrySetResult(result);
        public async Task<DouyinDiscoveryResult> DetectAsync(string? savedPath, IProgress<DouyinScanProgress>? progress, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            return await completion.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class SequenceDetector : IDouyinInstallationDetector
    {
        public int CallCount { get; private set; }
        public bool FirstCancelled { get; private set; }
        public TaskCompletionSource SecondStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<DouyinDiscoveryResult> DetectAsync(string? savedPath, IProgress<DouyinScanProgress>? progress, CancellationToken cancellationToken)
        {
            CallCount++;
            if (CallCount == 1)
            {
                try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
                catch (OperationCanceledException) { FirstCancelled = true; throw; }
            }

            SecondStarted.TrySetResult();

            return new(DouyinDiscoveryStatus.NotFound, null, [], DouyinScanProgress.Empty, "not-found");
        }
    }

    private sealed class ProgressDetector : IDouyinInstallationDetector
    {
        public Task<DouyinDiscoveryResult> DetectAsync(string? savedPath, IProgress<DouyinScanProgress>? progress, CancellationToken cancellationToken)
        {
            var value = new DouyinScanProgress("E:\\", @"E:\Apps", 19, 2, 0);
            progress?.Report(value);
            return Task.FromResult(new DouyinDiscoveryResult(DouyinDiscoveryStatus.NotFound, null, [], value, "not-found"));
        }
    }
}
