using RespawnSwitch.Application.Douyin;

namespace RespawnSwitch.Windows.DouyinDiscovery;

public sealed class FileSystemDouyinScanner(
    IFixedDriveCatalog fixedDriveCatalog,
    IDouyinCandidateValidator validator)
{
    private const int ProgressDirectoryInterval = 128;

    public async Task<IReadOnlyList<DouyinCandidate>> ScanAsync(
        IProgress<DouyinScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var candidates = new List<DouyinCandidate>();
        var seenCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long scanned = 0;
        long skipped = 0;
        string? currentDrive = null;
        string? currentDirectory = null;

        foreach (var root in fixedDriveCatalog.GetFixedDriveRoots())
        {
            cancellationToken.ThrowIfCancellationRequested();
            currentDrive = Path.GetPathRoot(root) ?? root;
            var pending = new Stack<string>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                currentDirectory = pending.Pop();
                scanned++;

                IEnumerable<string> entries;
                try
                {
                    entries = Directory.EnumerateFileSystemEntries(currentDirectory).ToArray();
                }
                catch (Exception exception) when (IsRecoverable(exception))
                {
                    skipped++;
                    Report(progress, currentDrive, currentDirectory, scanned, skipped, candidates.Count);
                    continue;
                }

                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    FileAttributes attributes;
                    try
                    {
                        attributes = File.GetAttributes(entry);
                    }
                    catch (Exception exception) when (IsRecoverable(exception))
                    {
                        skipped++;
                        continue;
                    }

                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        if (ShouldTraverse(attributes))
                        {
                            pending.Push(entry);
                        }
                        else
                        {
                            skipped++;
                        }

                        continue;
                    }

                    if (!string.Equals(Path.GetFileName(entry), "douyin.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var normalized = Path.GetFullPath(entry);
                    if (!seenCandidates.Add(normalized))
                    {
                        continue;
                    }

                    var candidate = await validator.ValidateAsync(
                        normalized,
                        DouyinDiscoverySource.FullDisk,
                        isRunning: false,
                        cancellationToken).ConfigureAwait(false);
                    if (candidate is not null)
                    {
                        candidates.Add(candidate);
                        Report(progress, currentDrive, currentDirectory, scanned, skipped, candidates.Count);
                    }
                }

                if (scanned % ProgressDirectoryInterval == 0)
                {
                    Report(progress, currentDrive, currentDirectory, scanned, skipped, candidates.Count);
                }
            }
        }

        Report(progress, currentDrive, currentDirectory, scanned, skipped, candidates.Count);
        return candidates
            .OrderBy(candidate => candidate.NormalizedPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static bool ShouldTraverse(FileAttributes attributes) =>
        (attributes & FileAttributes.Directory) != 0 &&
        (attributes & FileAttributes.ReparsePoint) == 0;

    private static bool IsRecoverable(Exception exception) =>
        exception is UnauthorizedAccessException or IOException or PathTooLongException or DirectoryNotFoundException or FileNotFoundException;

    private static void Report(
        IProgress<DouyinScanProgress>? progress,
        string? drive,
        string? directory,
        long scanned,
        long skipped,
        int found) =>
        progress?.Report(new(drive, directory, scanned, skipped, found));
}
