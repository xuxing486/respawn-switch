namespace RespawnSwitch.Application.Douyin;

public enum DouyinDiscoverySource
{
    SavedPath,
    RunningProcess,
    Registry,
    StartMenu,
    FullDisk
}

public enum DouyinDiscoveryStatus
{
    NotStarted,
    Scanning,
    Found,
    NotFound,
    Ambiguous,
    Cancelled,
    Failed
}

public enum DouyinDiscoveryMode
{
    Auto,
    Manual,
    WebOnly
}

public sealed record DouyinCandidate(
    string NormalizedPath,
    DouyinDiscoverySource Source,
    bool IsRunning,
    bool HasTrustedSignature,
    string? SignatureThumbprint,
    Version FileVersion,
    DateTimeOffset LastWriteTimeUtc,
    string ProductName,
    string FileDescription);

public sealed record DouyinScanProgress(
    string? CurrentDrive,
    string? CurrentDirectory,
    long DirectoriesScanned,
    long DirectoriesSkipped,
    int CandidatesFound)
{
    public static DouyinScanProgress Empty { get; } = new(null, null, 0, 0, 0);
}

public sealed record DouyinDiscoveryResult(
    DouyinDiscoveryStatus Status,
    DouyinCandidate? Selected,
    IReadOnlyList<DouyinCandidate> Candidates,
    DouyinScanProgress Progress,
    string Code);

public interface IDouyinInstallationDetector
{
    Task<DouyinDiscoveryResult> DetectAsync(
        string? savedPath,
        IProgress<DouyinScanProgress>? progress,
        CancellationToken cancellationToken);
}
