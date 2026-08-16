using System.Text.Json;
using System.IO;
using System.Text.Json.Serialization;
using RespawnSwitch.Application.Douyin;

namespace RespawnSwitch.App;

public sealed record AppSettings(
    string? PreferredDouyinPath,
    bool AutoDetectDouyin,
    bool OpenWebFallback,
    DouyinDiscoveryMode DiscoveryMode,
    string? LastValidatedSignatureThumbprint,
    string? DouyinWindowClass,
    string? SourceAppUserModelId,
    string? DiagnosticFingerprint)
{
    public static AppSettings Default { get; } = new(
        PreferredDouyinPath: null,
        AutoDetectDouyin: true,
        OpenWebFallback: true,
        DiscoveryMode: DouyinDiscoveryMode.Auto,
        LastValidatedSignatureThumbprint: null,
        DouyinWindowClass: null,
        SourceAppUserModelId: null,
        DiagnosticFingerprint: null);

    [JsonIgnore]
    public string DouyinPath => PreferredDouyinPath ?? @"D:\douyin\douyin.exe";
}

public static class AppSettingsStore
{
    public static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RespawnSwitch");
    public static string PathValue => Path.Combine(DirectoryPath, "settings.json");
    public static async Task<AppSettings> LoadAsync()
    {
        try { return !File.Exists(PathValue) ? AppSettings.Default : Deserialize(await File.ReadAllTextAsync(PathValue).ConfigureAwait(false)); }
        catch { return AppSettings.Default; }
    }

    public static AppSettings Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return AppSettings.Default;
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new AppSettings(
            PreferredDouyinPath: ReadString(root, "PreferredDouyinPath") ?? ReadString(root, "DouyinPath"),
            AutoDetectDouyin: ReadBoolean(root, "AutoDetectDouyin", fallback: true),
            OpenWebFallback: ReadBoolean(root, "OpenWebFallback", fallback: true),
            DiscoveryMode: ReadMode(root),
            LastValidatedSignatureThumbprint: ReadString(root, "LastValidatedSignatureThumbprint"),
            DouyinWindowClass: ReadString(root, "DouyinWindowClass"),
            SourceAppUserModelId: ReadString(root, "SourceAppUserModelId"),
            DiagnosticFingerprint: ReadString(root, "DiagnosticFingerprint"));
    }
    public static async Task SaveAsync(AppSettings settings)
    {
        Directory.CreateDirectory(DirectoryPath); var temporary = PathValue + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
        File.Move(temporary, PathValue, true);
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool ReadBoolean(JsonElement root, string name, bool fallback) =>
        root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : fallback;

    private static DouyinDiscoveryMode ReadMode(JsonElement root)
    {
        if (!root.TryGetProperty("DiscoveryMode", out var value))
        {
            return DouyinDiscoveryMode.Auto;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric) &&
            Enum.IsDefined(typeof(DouyinDiscoveryMode), numeric))
        {
            return (DouyinDiscoveryMode)numeric;
        }

        return value.ValueKind == JsonValueKind.String &&
               Enum.TryParse<DouyinDiscoveryMode>(value.GetString(), ignoreCase: true, out var parsed)
            ? parsed
            : DouyinDiscoveryMode.Auto;
    }
}
