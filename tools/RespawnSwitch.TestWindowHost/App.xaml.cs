using System.Configuration;
using System.Windows;

namespace RespawnSwitch.TestWindowHost;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new TestWindow(HostOptions.Parse(e.Args));
        MainWindow = window;
        window.Show();
    }
}
