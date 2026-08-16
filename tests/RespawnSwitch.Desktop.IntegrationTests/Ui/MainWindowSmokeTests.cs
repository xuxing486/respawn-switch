using System.Windows.Controls;
using System.Windows.Threading;
using RespawnSwitch.App;

namespace RespawnSwitch.Desktop.IntegrationTests.Ui;

public sealed class MainWindowSmokeTests
{
    [Fact]
    public void MainWindow_ContainsDiscoverySettingsStatusAndLogControls()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                if (System.Windows.Application.Current is null)
                {
                    var application = new RespawnSwitch.App.App();
                    application.InitializeComponent();
                }
                var window = new MainWindow();
                foreach (var name in new[]
                {
                    "MonitoringStatusPill", "LeagueStatusCard", "DouyinStatusCard", "MediaStatusCard",
                    "AutoDetectToggle", "WebFallbackToggle", "DiscoveryProgressBar", "CandidateList",
                    "RescanButton", "CancelScanButton", "EventLog"
                })
                {
                    Assert.NotNull(window.FindName(name));
                }

                Assert.IsType<ProgressBar>(window.FindName("DiscoveryProgressBar"));
                Assert.IsType<ListBox>(window.FindName("CandidateList"));
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        Assert.Null(failure);
    }
}
