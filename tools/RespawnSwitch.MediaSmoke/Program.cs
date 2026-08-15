using System.Text.Json;
using System.Text.Json.Serialization;
using RespawnSwitch.Application.Media;
using RespawnSwitch.Windows.Media;

namespace RespawnSwitch.MediaSmoke;

public enum MediaSmokeCommand { Invalid, Help, List, Probe }

public sealed record MediaSmokeOptions(
    MediaSmokeCommand Command,
    string? Aumid,
    string? Fingerprint,
    bool IsValid);

public static class MediaSmokeCli
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<int> Main(string[] args)
    {
        var options = Parse(args);
        if (!options.IsValid)
        {
            PrintHelp(Console.Error);
            return 6;
        }

        if (options.Command == MediaSmokeCommand.Help)
        {
            PrintHelp(Console.Out);
            return 0;
        }

        try
        {
            var gateway = new WinRtGsmtcGateway();
            if (options.Command == MediaSmokeCommand.List)
            {
                var sessions = await gateway.EnumerateAsync(CancellationToken.None);
                WriteJson(new
                {
                    controller = "GSMTC",
                    matchCount = sessions.Count,
                    sessions = sessions.Select(session => new
                    {
                        sourceAppUserModelId = session.SourceAppUserModelId,
                        diagnosticFingerprint = session.DiagnosticFingerprint,
                        state = session.PlaybackState,
                        session.CanPlay,
                        session.CanPause
                    })
                });
                return 0;
            }

            var profile = new GsmtcMediaProfile(options.Aumid!, options.Fingerprint!);
            var sessionsBefore = await gateway.EnumerateAsync(CancellationToken.None);
            var matchCount = sessionsBefore.Count(session =>
                string.Equals(session.SourceAppUserModelId, profile.SourceAppUserModelId, StringComparison.Ordinal) &&
                string.Equals(session.DiagnosticFingerprint, profile.DiagnosticFingerprint, StringComparison.Ordinal));
            var controller = new GsmtcDouyinMediaController(profile);
            var probe = await controller.ProbeAsync(CancellationToken.None);
            WriteJson(new
            {
                controller = controller.Name,
                matchCount,
                state = probe.State,
                isUsable = probe.IsUsable,
                probe.FailureKind,
                probe.FailureCode
            });
            return ExitCode(probe.FailureKind, probe.IsUsable);
        }
        catch (Exception exception)
        {
            var failure = GsmtcFailureMapper.Map(exception, CancellationToken.None);
            WriteJson(new { controller = "GSMTC", failureKind = failure.Kind, failureCode = failure.Code });
            return ExitCode(failure.Kind, verified: false);
        }
    }

    public static MediaSmokeOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 1 && args[0] is "--help" or "-h" or "help")
        {
            return new(MediaSmokeCommand.Help, null, null, true);
        }

        if (args.Count == 1 && string.Equals(args[0], "list", StringComparison.Ordinal))
        {
            return new(MediaSmokeCommand.List, null, null, true);
        }

        if (args.Count != 5 || !TryCommand(args[0], out var command))
        {
            return new(MediaSmokeCommand.Invalid, null, null, false);
        }

        string? aumid = null;
        string? fingerprint = null;
        for (var index = 1; index < args.Count; index += 2)
        {
            if (string.Equals(args[index], "--aumid", StringComparison.Ordinal))
            {
                aumid = args[index + 1];
            }
            else if (string.Equals(args[index], "--fingerprint", StringComparison.Ordinal))
            {
                fingerprint = args[index + 1];
            }
            else
            {
                return new(MediaSmokeCommand.Invalid, null, null, false);
            }
        }

        var valid = !string.IsNullOrWhiteSpace(aumid) && !string.IsNullOrWhiteSpace(fingerprint);
        return valid
            ? new(command, aumid, fingerprint, true)
            : new(MediaSmokeCommand.Invalid, null, null, false);
    }

    private static bool TryCommand(string value, out MediaSmokeCommand command)
    {
        command = value switch
        {
            "probe" => MediaSmokeCommand.Probe,
            _ => MediaSmokeCommand.Invalid
        };
        return command != MediaSmokeCommand.Invalid;
    }

    private static int ExitCode(MediaFailureKind kind, bool verified) => kind switch
    {
        MediaFailureKind.None when verified => 0,
        MediaFailureKind.NoMatch => 2,
        MediaFailureKind.AmbiguousMatch => 3,
        MediaFailureKind.PermissionDenied or MediaFailureKind.Unsupported => 4,
        _ => 5
    };

    private static void PrintHelp(TextWriter writer) => writer.WriteLine(
        "RespawnSwitch.MediaSmoke list | probe --aumid AUMID --fingerprint SHA256_HEX");

    private static void WriteJson<T>(T value) =>
        Console.WriteLine(JsonSerializer.Serialize(value, JsonOptions));
}
