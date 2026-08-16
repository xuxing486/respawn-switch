using RespawnSwitch.Application.Douyin;

namespace RespawnSwitch.Windows.DouyinDiscovery;

public interface IDouyinQuickCandidateSource
{
    Task<IReadOnlyList<DouyinCandidate>> FindAsync(string? savedPath, CancellationToken cancellationToken);
}

public interface IDouyinFullDiskScanner
{
    Task<IReadOnlyList<DouyinCandidate>> ScanAsync(
        IProgress<DouyinScanProgress>? progress,
        CancellationToken cancellationToken);
}

public sealed class WindowsDouyinInstallationDetector(
    IDouyinQuickCandidateSource quickCandidateSource,
    IDouyinFullDiskScanner fullDiskScanner) : IDouyinInstallationDetector
{
    public WindowsDouyinInstallationDetector(string? trustedSignatureThumbprint = null)
        : this(CreateQuickSources(trustedSignatureThumbprint), CreateFullScanner(trustedSignatureThumbprint))
    {
    }

    public async Task<DouyinDiscoveryResult> DetectAsync(
        string? savedPath,
        IProgress<DouyinScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var latestProgress = DouyinScanProgress.Empty;
        var forwardingProgress = new Progress<DouyinScanProgress>(value =>
        {
            latestProgress = value;
            progress?.Report(value);
        });

        try
        {
            IReadOnlyList<DouyinCandidate> quickCandidates;
            try
            {
                quickCandidates = await quickCandidateSource.FindAsync(savedPath, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                quickCandidates = [];
            }

            var quickResult = DouyinCandidateSelector.Select(quickCandidates);
            if (quickResult.Status == DouyinDiscoveryStatus.Found)
            {
                return quickResult with { Progress = latestProgress };
            }

            var diskCandidates = await fullDiskScanner.ScanAsync(forwardingProgress, cancellationToken).ConfigureAwait(false);
            return DouyinCandidateSelector.Select(quickCandidates.Concat(diskCandidates)) with { Progress = latestProgress };
        }
        catch (OperationCanceledException)
        {
            return new(
                DouyinDiscoveryStatus.Cancelled,
                null,
                [],
                latestProgress,
                "douyin.discovery.cancelled");
        }
        catch (Exception)
        {
            return new(
                DouyinDiscoveryStatus.Failed,
                null,
                [],
                latestProgress,
                "douyin.discovery.failed");
        }
    }

    private static IDouyinQuickCandidateSource CreateQuickSources(string? trustedSignatureThumbprint) =>
        new WindowsDouyinQuickSources(new DouyinCandidateValidator(trustedSignatureThumbprint));

    private static IDouyinFullDiskScanner CreateFullScanner(string? trustedSignatureThumbprint) =>
        new FileSystemDouyinScanner(
            new FixedDriveCatalog(),
            new DouyinCandidateValidator(trustedSignatureThumbprint));
}
