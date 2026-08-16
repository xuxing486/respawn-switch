using RespawnSwitch.Application.Douyin;

namespace RespawnSwitch.App;

public sealed class DouyinDiscoveryController : IAsyncDisposable
{
    private readonly IDouyinInstallationDetector detector;
    private readonly SynchronizationContext? synchronizationContext;
    private readonly object syncRoot = new();
    private CancellationTokenSource? scanCancellation;
    private Task? scanTask;
    private long generation;
    private bool disposed;
    private DouyinDiscoveryResult currentResult = new(
        DouyinDiscoveryStatus.NotStarted,
        null,
        [],
        DouyinScanProgress.Empty,
        "douyin.discovery.not-started");

    public DouyinDiscoveryController(IDouyinInstallationDetector detector)
    {
        this.detector = detector ?? throw new ArgumentNullException(nameof(detector));
        synchronizationContext = SynchronizationContext.Current;
    }

    public event EventHandler<DouyinDiscoveryResult>? Changed;

    public DouyinDiscoveryResult CurrentResult
    {
        get { lock (syncRoot) { return currentResult; } }
    }

    public Task StartAsync(string? savedPath)
    {
        lock (syncRoot)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (scanTask is not null && !scanTask.IsCompleted)
            {
                return Task.CompletedTask;
            }

            StartScanLocked(savedPath);
        }

        return Task.CompletedTask;
    }

    public async Task RescanAsync(string? savedPath)
    {
        Task? previous;
        lock (syncRoot)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            scanCancellation?.Cancel();
            previous = scanTask;
        }

        if (previous is not null)
        {
            try { await previous.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        lock (syncRoot)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            StartScanLocked(savedPath);
        }
    }

    public async Task CancelAsync()
    {
        Task? active;
        lock (syncRoot)
        {
            scanCancellation?.Cancel();
            active = scanTask;
        }

        if (active is not null)
        {
            try { await active.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
    }

    public Task WaitForIdleAsync()
    {
        lock (syncRoot)
        {
            return scanTask ?? Task.CompletedTask;
        }
    }

    private void StartScanLocked(string? savedPath)
    {
        scanCancellation?.Dispose();
        scanCancellation = new CancellationTokenSource();
        var token = scanCancellation.Token;
        var thisGeneration = ++generation;
        PublishLocked(new(
            DouyinDiscoveryStatus.Scanning,
            null,
            [],
            DouyinScanProgress.Empty,
            "douyin.discovery.scanning"));
        scanTask = Task.Run(async () =>
        {
            var progress = new ImmediateProgress(value => PublishProgress(thisGeneration, value));
            DouyinDiscoveryResult result;
            try
            {
                result = await detector.DetectAsync(savedPath, progress, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                result = new(
                    DouyinDiscoveryStatus.Cancelled,
                    null,
                    [],
                    CurrentResult.Progress,
                    "douyin.discovery.cancelled");
            }
            catch (Exception)
            {
                result = new(
                    DouyinDiscoveryStatus.Failed,
                    null,
                    [],
                    CurrentResult.Progress,
                    "douyin.discovery.failed");
            }

            PublishIfCurrent(thisGeneration, result);
        }, CancellationToken.None);
    }

    private void PublishProgress(long thisGeneration, DouyinScanProgress progress)
    {
        DouyinDiscoveryResult snapshot;
        lock (syncRoot)
        {
            if (thisGeneration != generation || disposed)
            {
                return;
            }

            snapshot = currentResult with { Progress = progress };
            currentResult = snapshot;
        }

        RaiseChanged(snapshot);
    }

    private void PublishIfCurrent(long thisGeneration, DouyinDiscoveryResult result)
    {
        lock (syncRoot)
        {
            if (thisGeneration != generation || disposed)
            {
                return;
            }

            currentResult = result;
        }

        RaiseChanged(result);
    }

    private void PublishLocked(DouyinDiscoveryResult result)
    {
        currentResult = result;
        RaiseChanged(result);
    }

    private void RaiseChanged(DouyinDiscoveryResult result)
    {
        void Raise() => Changed?.Invoke(this, result);
        if (synchronizationContext is null || SynchronizationContext.Current == synchronizationContext)
        {
            Raise();
        }
        else
        {
            synchronizationContext.Post(_ => Raise(), null);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task? active;
        lock (syncRoot)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            scanCancellation?.Cancel();
            active = scanTask;
        }

        if (active is not null)
        {
            try { await active.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        lock (syncRoot)
        {
            scanCancellation?.Dispose();
            scanCancellation = null;
        }
    }

    private sealed class ImmediateProgress(Action<DouyinScanProgress> report) : IProgress<DouyinScanProgress>
    {
        public void Report(DouyinScanProgress value) => report(value);
    }
}
