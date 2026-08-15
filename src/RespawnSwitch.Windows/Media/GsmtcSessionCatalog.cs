using RespawnSwitch.Application.Media;

namespace RespawnSwitch.Windows.Media;

internal static class GsmtcSessionCatalog
{
    internal static GsmtcSelectionResult Select(
        IReadOnlyList<GsmtcSessionDescriptor> sessions,
        GsmtcMediaProfile profile)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(profile);

        var matches = sessions
            .Where(session =>
                string.Equals(session.SourceAppUserModelId, profile.SourceAppUserModelId, StringComparison.Ordinal) &&
                string.Equals(session.DiagnosticFingerprint, profile.DiagnosticFingerprint, StringComparison.Ordinal))
            .Take(2)
            .ToArray();

        return matches.Length switch
        {
            0 => new(null, MediaFailureKind.NoMatch, "gsmtc-no-exact-match"),
            1 => new(matches[0], MediaFailureKind.None, string.Empty),
            _ => new(null, MediaFailureKind.AmbiguousMatch, "gsmtc-ambiguous-exact-match")
        };
    }
}

internal sealed record GsmtcSelectionResult(
    GsmtcSessionDescriptor? SelectedSession,
    MediaFailureKind FailureKind,
    string FailureCode);
