using RespawnSwitch.Application.Douyin;
using RespawnSwitch.Windows.DouyinDiscovery;

namespace RespawnSwitch.Windows.Tests.DouyinDiscovery;

public sealed class FileSystemDouyinScannerTests
{
    [Fact]
    public async Task ScanAsync_FindsDouyinBelowNonstandardNestedDirectory()
    {
        using var tree = new TemporaryTree();
        var executable = tree.CreateFile(Path.Combine("unusual", "deep", "client", "douyin.exe"));
        var progress = new List<DouyinScanProgress>();
        var scanner = new FileSystemDouyinScanner(
            new FixedRoots([tree.Root]),
            new AcceptingValidator());

        var results = await scanner.ScanAsync(new InlineProgress<DouyinScanProgress>(progress.Add), CancellationToken.None);

        var candidate = Assert.Single(results);
        Assert.Equal(Path.GetFullPath(executable), candidate.NormalizedPath);
        Assert.Contains(progress, item => item.CandidatesFound == 1);
    }

    [Fact]
    public async Task ScanAsync_VisitsEveryProvidedFixedRootAndMatchesExactFileName()
    {
        using var first = new TemporaryTree();
        using var second = new TemporaryTree();
        _ = first.CreateFile("not-douyin.exe");
        _ = first.CreateFile("douyin.exe.backup");
        var firstExecutable = first.CreateFile("DOUYIN.EXE");
        var secondExecutable = second.CreateFile(Path.Combine("apps", "douyin.exe"));
        var scanner = new FileSystemDouyinScanner(
            new FixedRoots([first.Root, second.Root]),
            new AcceptingValidator());

        var results = await scanner.ScanAsync(null, CancellationToken.None);

        Assert.Equal(
            new[] { Path.GetFullPath(firstExecutable), Path.GetFullPath(secondExecutable) }
                .Order(StringComparer.OrdinalIgnoreCase),
            results.Select(candidate => candidate.NormalizedPath).Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ScanAsync_MissingRootDoesNotPreventOtherRoots()
    {
        using var tree = new TemporaryTree();
        var executable = tree.CreateFile("douyin.exe");
        var missing = Path.Combine(tree.Root, "removed-root");
        var scanner = new FileSystemDouyinScanner(
            new FixedRoots([missing, tree.Root]),
            new AcceptingValidator());

        var results = await scanner.ScanAsync(null, CancellationToken.None);

        Assert.Equal(Path.GetFullPath(executable), Assert.Single(results).NormalizedPath);
    }

    [Fact]
    public async Task ScanAsync_CancelledToken_ThrowsCancellation()
    {
        using var tree = new TemporaryTree();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var scanner = new FileSystemDouyinScanner(
            new FixedRoots([tree.Root]),
            new AcceptingValidator());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => scanner.ScanAsync(null, cancellation.Token));
    }

    [Theory]
    [InlineData(FileAttributes.Directory, true)]
    [InlineData(FileAttributes.Directory | FileAttributes.ReparsePoint, false)]
    [InlineData(FileAttributes.Normal, false)]
    public void ShouldTraverse_RejectsReparsePointsAndNonDirectories(
        FileAttributes attributes,
        bool expected)
    {
        Assert.Equal(expected, FileSystemDouyinScanner.ShouldTraverse(attributes));
    }

    [Theory]
    [InlineData(DriveType.Fixed, true, true)]
    [InlineData(DriveType.Fixed, false, false)]
    [InlineData(DriveType.Network, true, false)]
    [InlineData(DriveType.Removable, true, false)]
    public void FixedDriveCatalog_IsEligible_OnlyForReadyFixedDrives(
        DriveType type,
        bool isReady,
        bool expected)
    {
        Assert.Equal(expected, FixedDriveCatalog.IsEligible(type, isReady));
    }

    private sealed class FixedRoots(IReadOnlyList<string> roots) : IFixedDriveCatalog
    {
        public IReadOnlyList<string> GetFixedDriveRoots() => roots;
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class AcceptingValidator : IDouyinCandidateValidator
    {
        public ValueTask<DouyinCandidate?> ValidateAsync(
            string path,
            DouyinDiscoverySource source,
            bool isRunning,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<DouyinCandidate?>(new(
                Path.GetFullPath(path),
                source,
                isRunning,
                HasTrustedSignature: true,
                SignatureThumbprint: "TEST",
                new Version(1, 0),
                DateTimeOffset.UnixEpoch,
                "Douyin",
                "Douyin"));
        }
    }

    private sealed class TemporaryTree : IDisposable
    {
        public TemporaryTree() => Root = Directory.CreateTempSubdirectory("respawnswitch-scan-").FullName;

        public string Root { get; }

        public string CreateFile(string relativePath)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, [0x4D, 0x5A]);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
