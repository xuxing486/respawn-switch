using System.IO;

namespace RespawnSwitch.TestWindowHost;

public sealed record HostOptions(string Mode, string ReadyFile, string? RecreateEvent, string? HungEvent)
{
    private static readonly HashSet<string> SupportedModes = new(StringComparer.OrdinalIgnoreCase) { "normal", "hidden", "minimized", "maximized", "topmost", "recreate", "topmost-peer", "focus-target", "hung-uia" };

    public static HostOptions Parse(IReadOnlyList<string> arguments)
    {
        string? mode = null, readyFile = null, recreateEvent = null, hungEvent = null;
        for (var index = 0; index < arguments.Count; index += 2)
        {
            if (index + 1 >= arguments.Count) throw new ArgumentException("Every TestWindowHost option requires a value.");
            switch (arguments[index])
            {
                case "--mode": mode = arguments[index + 1]; break;
                case "--ready-file": readyFile = arguments[index + 1]; break;
                case "--recreate-event": recreateEvent = arguments[index + 1]; break;
                case "--hung-event": hungEvent = arguments[index + 1]; break;
                default: throw new ArgumentException($"Unknown TestWindowHost option: {arguments[index]}");
            }
        }
        if (mode is null || !SupportedModes.Contains(mode)) throw new ArgumentException("A supported --mode is required.");
        if (string.IsNullOrWhiteSpace(readyFile)) throw new ArgumentException("--ready-file is required.");
        return new HostOptions(mode, Path.GetFullPath(readyFile), recreateEvent, hungEvent);
    }
}
