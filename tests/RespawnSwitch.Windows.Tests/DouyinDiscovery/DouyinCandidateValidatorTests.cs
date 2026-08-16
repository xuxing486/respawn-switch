using RespawnSwitch.Application.Douyin;
using RespawnSwitch.Windows.DouyinDiscovery;

namespace RespawnSwitch.Windows.Tests.DouyinDiscovery;

public sealed class DouyinCandidateValidatorTests
{
    [Fact]
    public async Task ValidateAsync_MissingFile_IsRejected()
    {
        var validator = new DouyinCandidateValidator(new MetadataReader(null));

        var result = await validator.ValidateAsync(
            @"C:\missing\douyin.exe",
            DouyinDiscoverySource.FullDisk,
            isRunning: false,
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateAsync_WrongFileName_IsRejected()
    {
        var validator = new DouyinCandidateValidator(new MetadataReader(Metadata(productName: "Douyin")));

        var result = await validator.ValidateAsync(
            @"C:\Apps\video.exe",
            DouyinDiscoverySource.FullDisk,
            isRunning: false,
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateAsync_UnrelatedUnsignedProduct_IsRejected()
    {
        var validator = new DouyinCandidateValidator(new MetadataReader(Metadata(productName: "Other Player", trusted: false)));

        var result = await validator.ValidateAsync(
            @"C:\Apps\douyin.exe",
            DouyinDiscoverySource.FullDisk,
            isRunning: false,
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateAsync_DouyinProduct_ReturnsNormalizedCandidate()
    {
        var validator = new DouyinCandidateValidator(new MetadataReader(Metadata(productName: "抖音桌面客户端", version: new Version(8, 1, 2, 3))));

        var result = await validator.ValidateAsync(
            @"C:\Apps\..\Apps\douyin.exe",
            DouyinDiscoverySource.Registry,
            isRunning: true,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(Path.GetFullPath(@"C:\Apps\douyin.exe"), result.NormalizedPath);
        Assert.Equal(new Version(8, 1, 2, 3), result.FileVersion);
        Assert.True(result.IsRunning);
        Assert.True(result.HasTrustedSignature);
    }

    [Fact]
    public async Task ValidateAsync_SavedTrustedThumbprint_AllowsKnownSignedIdentity()
    {
        var validator = new DouyinCandidateValidator(
            new MetadataReader(Metadata(productName: "Desktop Client", thumbprint: "KNOWN", trusted: true)),
            trustedSignatureThumbprint: "KNOWN");

        var result = await validator.ValidateAsync(
            @"D:\Portable\douyin.exe",
            DouyinDiscoverySource.SavedPath,
            isRunning: false,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("KNOWN", result.SignatureThumbprint);
    }

    private static DouyinFileMetadata Metadata(
        string productName,
        Version? version = null,
        string? thumbprint = "ABC",
        bool trusted = true) =>
        new(
            Exists: true,
            ProductName: productName,
            FileDescription: productName,
            FileVersion: version ?? new Version(1, 0),
            LastWriteTimeUtc: DateTimeOffset.Parse("2026-08-15T00:00:00Z"),
            SignatureThumbprint: thumbprint,
            HasTrustedSignature: trusted);

    private sealed class MetadataReader(DouyinFileMetadata? metadata) : IDouyinFileMetadataReader
    {
        public DouyinFileMetadata? Read(string path) => metadata;
    }
}
