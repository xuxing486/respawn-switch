using System.Configuration;
using System.Windows;

namespace RespawnSwitch.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private async void OnStartup(object sender, StartupEventArgs e)
    {
        if (e.Args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                System.IO.Directory.CreateDirectory(AppSettingsStore.DirectoryPath);
                var probe = System.IO.Path.Combine(AppSettingsStore.DirectoryPath, $".self-test-{Guid.NewGuid():N}.tmp");
                await System.IO.File.WriteAllTextAsync(probe, "ok");
                System.IO.File.Delete(probe);
                _ = new RespawnSwitch.App.Overlay.RespawnOverlayWindow();
                _ = new RespawnSwitch.Core.Respawn.RespawnStateMachine(new RespawnSwitch.Core.Respawn.RespawnStateMachineOptions(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2), 2.0, TimeProvider.System.TimestampFrequency));
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { ok = true, settings = AppSettingsStore.DirectoryPath }));
                Shutdown(0);
            }
            catch (Exception ex) { Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { ok = false, error = ex.Message })); Shutdown(1); }
            return;
        }
        MainWindow = new MainWindow(); MainWindow.Show();
    }
}
