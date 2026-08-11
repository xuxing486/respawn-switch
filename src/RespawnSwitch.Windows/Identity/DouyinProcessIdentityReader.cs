using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using RespawnSwitch.Application.Windows;

namespace RespawnSwitch.Windows.Identity;

public interface IDouyinProcessIdentityReader
{
    ValueTask<ProcessIdentity?> TryReadAsync(int processId, CancellationToken cancellationToken);
}

public sealed class DouyinProcessIdentityReader : IDouyinProcessIdentityReader
{
    public ValueTask<ProcessIdentity?> TryReadAsync(int processId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var process = Process.GetProcessById(processId);
            var path = NormalizePath(process.MainModule?.FileName);
            if (path is null)
            {
                return ValueTask.FromResult<ProcessIdentity?>(null);
            }

            using var certificate = X509Certificate.CreateFromSignedFile(path);
            using var x509 = new X509Certificate2(certificate);
            return ValueTask.FromResult<ProcessIdentity?>(new ProcessIdentity(
                processId,
                new DateTimeOffset(process.StartTime.ToUniversalTime()),
                path,
                x509.Subject,
                x509.Thumbprint ?? string.Empty));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or CryptographicException)
        {
            return ValueTask.FromResult<ProcessIdentity?>(null);
        }
    }

    public static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
    }
}
