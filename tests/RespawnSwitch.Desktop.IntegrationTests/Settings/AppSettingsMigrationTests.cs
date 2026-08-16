using RespawnSwitch.Application.Douyin;
using RespawnSwitch.App;

namespace RespawnSwitch.Desktop.IntegrationTests.Settings;

public sealed class AppSettingsMigrationTests
{
    [Fact]
    public void Deserialize_Version01Json_MigratesDouyinPathAndKeepsIdentity()
    {
        const string json = """
        {
          "DouyinPath": "D:\\douyin\\douyin.exe",
          "DouyinWindowClass": "Chrome_WidgetWin_1",
          "SourceAppUserModelId": "douyin.aumid",
          "DiagnosticFingerprint": "fingerprint"
        }
        """;

        var settings = AppSettingsStore.Deserialize(json);

        Assert.Equal(@"D:\douyin\douyin.exe", settings.PreferredDouyinPath);
        Assert.True(settings.AutoDetectDouyin);
        Assert.True(settings.OpenWebFallback);
        Assert.Equal(DouyinDiscoveryMode.Auto, settings.DiscoveryMode);
        Assert.Equal("Chrome_WidgetWin_1", settings.DouyinWindowClass);
        Assert.Equal("douyin.aumid", settings.SourceAppUserModelId);
        Assert.Equal("fingerprint", settings.DiagnosticFingerprint);
    }

    [Fact]
    public void Deserialize_NewJson_PreservesExplicitFalseSettings()
    {
        const string json = """
        {
          "PreferredDouyinPath": "C:\\Douyin\\douyin.exe",
          "AutoDetectDouyin": false,
          "OpenWebFallback": false,
          "DiscoveryMode": 1
        }
        """;

        var settings = AppSettingsStore.Deserialize(json);

        Assert.False(settings.AutoDetectDouyin);
        Assert.False(settings.OpenWebFallback);
        Assert.Equal(DouyinDiscoveryMode.Manual, settings.DiscoveryMode);
    }
}
