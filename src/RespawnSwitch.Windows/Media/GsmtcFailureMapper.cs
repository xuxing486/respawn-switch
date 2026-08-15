using RespawnSwitch.Application.Media;

namespace RespawnSwitch.Windows.Media;

internal static class GsmtcFailureMapper
{
    private const int AccessDeniedHResult = unchecked((int)0x80070005);
    private const int ServiceDoesNotExistHResult = unchecked((int)0x80070424);

    internal static (MediaFailureKind Kind, string Code) Map(
        Exception exception,
        CancellationToken cancellationToken) =>
        exception switch
        {
            OperationCanceledException when cancellationToken.IsCancellationRequested =>
                (MediaFailureKind.Cancelled, "cancelled"),
            UnauthorizedAccessException =>
                (MediaFailureKind.PermissionDenied, "gsmtc-permission-denied"),
            _ when exception.HResult == AccessDeniedHResult =>
                (MediaFailureKind.PermissionDenied, $"gsmtc-permission-denied-{exception.HResult:x8}"),
            PlatformNotSupportedException or NotSupportedException =>
                (MediaFailureKind.Unsupported, "gsmtc-unsupported"),
            _ when exception.HResult == ServiceDoesNotExistHResult =>
                (MediaFailureKind.Unsupported, $"gsmtc-service-unavailable-{exception.HResult:x8}"),
            TimeoutException =>
                (MediaFailureKind.TimedOut, "gsmtc-timed-out"),
            _ => (MediaFailureKind.Unexpected, $"gsmtc-unexpected-{exception.HResult:x8}")
        };
}
