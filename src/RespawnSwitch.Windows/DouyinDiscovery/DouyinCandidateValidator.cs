using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using RespawnSwitch.Application.Douyin;

namespace RespawnSwitch.Windows.DouyinDiscovery;

internal sealed record DouyinFileMetadata(
    bool Exists,
    string ProductName,
    string FileDescription,
    Version? FileVersion,
    DateTimeOffset LastWriteTimeUtc,
    string? SignatureThumbprint,
    bool HasTrustedSignature);

internal interface IDouyinFileMetadataReader
{
    DouyinFileMetadata? Read(string path);
}

public sealed class DouyinCandidateValidator : IDouyinCandidateValidator
{
    private readonly IDouyinFileMetadataReader metadataReader;
    private readonly string? trustedSignatureThumbprint;

    public DouyinCandidateValidator(string? trustedSignatureThumbprint = null)
        : this(new DouyinFileMetadataReader(), trustedSignatureThumbprint)
    {
    }

    internal DouyinCandidateValidator(
        IDouyinFileMetadataReader metadataReader,
        string? trustedSignatureThumbprint = null)
    {
        this.metadataReader = metadataReader ?? throw new ArgumentNullException(nameof(metadataReader));
        this.trustedSignatureThumbprint = NormalizeThumbprint(trustedSignatureThumbprint);
    }

    public ValueTask<DouyinCandidate?> ValidateAsync(
        string path,
        DouyinDiscoverySource source,
        bool isRunning,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(path) ||
            !string.Equals(Path.GetFileName(path), "douyin.exe", StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult<DouyinCandidate?>(null);
        }

        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return ValueTask.FromResult<DouyinCandidate?>(null);
        }

        var metadata = metadataReader.Read(normalizedPath);
        if (metadata is not { Exists: true, FileVersion: not null })
        {
            return ValueTask.FromResult<DouyinCandidate?>(null);
        }

        var identifiesDouyin = ContainsDouyin(metadata.ProductName) || ContainsDouyin(metadata.FileDescription);
        var thumbprintMatches = trustedSignatureThumbprint is not null &&
            string.Equals(
                NormalizeThumbprint(metadata.SignatureThumbprint),
                trustedSignatureThumbprint,
                StringComparison.Ordinal);
        if (!identifiesDouyin && !(metadata.HasTrustedSignature && thumbprintMatches))
        {
            return ValueTask.FromResult<DouyinCandidate?>(null);
        }

        return ValueTask.FromResult<DouyinCandidate?>(new DouyinCandidate(
            normalizedPath,
            source,
            isRunning,
            metadata.HasTrustedSignature,
            NormalizeThumbprint(metadata.SignatureThumbprint),
            metadata.FileVersion,
            metadata.LastWriteTimeUtc,
            metadata.ProductName,
            metadata.FileDescription));
    }

    private static bool ContainsDouyin(string value) =>
        value.Contains("douyin", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("抖音", StringComparison.Ordinal);

    private static string? NormalizeThumbprint(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

    private sealed class DouyinFileMetadataReader : IDouyinFileMetadataReader
    {
        public DouyinFileMetadata? Read(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                var versionInfo = FileVersionInfo.GetVersionInfo(path);
                var version = ParseVersion(versionInfo.FileVersion) ?? ParseVersion(versionInfo.ProductVersion);
                var (thumbprint, trusted) = ReadSignature(path);
                return new(
                    Exists: true,
                    ProductName: versionInfo.ProductName ?? string.Empty,
                    FileDescription: versionInfo.FileDescription ?? string.Empty,
                    FileVersion: version,
                    LastWriteTimeUtc: new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero),
                    SignatureThumbprint: thumbprint,
                    HasTrustedSignature: trusted);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException or ArgumentException)
            {
                return null;
            }
        }

        private static Version? ParseVersion(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var numeric = new string(value.TakeWhile(character => char.IsDigit(character) || character == '.').ToArray());
            return Version.TryParse(numeric.TrimEnd('.'), out var version) ? version : null;
        }

        private static (string? Thumbprint, bool Trusted) ReadSignature(string path)
        {
            try
            {
                using var embedded = X509Certificate.CreateFromSignedFile(path);
                using var certificate = new X509Certificate2(embedded);
                var now = DateTimeOffset.UtcNow;
                if (now < certificate.NotBefore || now > certificate.NotAfter)
                {
                    return (certificate.Thumbprint, false);
                }

                using var chain = new X509Chain();
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
                chain.ChainPolicy.ApplicationPolicy.Add(new System.Security.Cryptography.Oid("1.3.6.1.5.5.7.3.3"));
                return (certificate.Thumbprint, chain.Build(certificate));
            }
            catch (CryptographicException)
            {
                return (null, false);
            }
        }
    }
}
