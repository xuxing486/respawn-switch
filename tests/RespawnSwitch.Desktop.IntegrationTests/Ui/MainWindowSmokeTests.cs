using System.Windows.Controls;
using System.Windows.Threading;
using RespawnSwitch.App;

namespace RespawnSwitch.Desktop.IntegrationTests.Ui;

public sealed class MainWindowSmokeTests
{
    [Fact]
    public void MainWindow_ContainsCompactPrematchReadinessAndDiagnostics()
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
                Assert.Equal(
                    System.Windows.ShutdownMode.OnMainWindowClose,
                    System.Windows.Application.Current!.ShutdownMode);
                var window = new MainWindow();
                foreach (var name in new[]
                {
                    "OverallStatusText", "LeagueStatusCard", "LeagueClientStateText", "LeagueGameDataStateText",
                    "DouyinStatusCard", "DouyinTargetCombo", "DouyinStateText", "MediaStateText",
                    "AutomationStatusCard", "AutomationStateText", "IssuePanel", "IssueText",
                    "RetestButton", "TestOverlayButton", "EventLog"
                })
                {
                    Assert.NotNull(window.FindName(name));
                }

                Assert.IsType<ComboBox>(window.FindName("DouyinTargetCombo"));
                Assert.IsType<ListBox>(window.FindName("EventLog"));
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
