using System.Text.Json;
using System.IO;

namespace RespawnSwitch.App;

public sealed record AppSettings(string DouyinPath, string? DouyinWindowClass, string? SourceAppUserModelId, string? DiagnosticFingerprint)
{
    public static AppSettings Default { get; } = new(@"D:\douyin\douyin.exe", null, null, null);
}

public static class AppSettingsStore
{
    public static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RespawnSwitch");
    public static string PathValue => Path.Combine(DirectoryPath, "settings.json");
    public static async Task<AppSettings> LoadAsync()
    {
        try { if (!File.Exists(PathValue)) return AppSettings.Default; await using var file = File.OpenRead(PathValue); return await JsonSerializer.DeserializeAsync<AppSettings>(file).ConfigureAwait(false) ?? AppSettings.Default; }
        catch { return AppSettings.Default; }
    }
    public static async Task SaveAsync(AppSettings settings)
    {
        Directory.CreateDirectory(DirectoryPath); var temporary = PathValue + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
        File.Move(temporary, PathValue, true);
    }
}
