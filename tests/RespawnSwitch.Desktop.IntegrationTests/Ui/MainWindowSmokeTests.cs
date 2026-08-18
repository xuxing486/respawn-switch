// Author: Stress Monster
using System.Windows.Controls;
using System.Windows.Threading;
using System.Reflection;
using RespawnSwitch.App;
using RespawnSwitch.App.Overlay;
using RespawnSwitch.Core.Game;
using RespawnSwitch.Core.Respawn;
using RespawnSwitch.Windows.DouyinDiscovery;

namespace RespawnSwitch.Desktop.IntegrationTests.Ui;

public sealed class MainWindowSmokeTests
{
    [Fact]
    public void MainWindow_ContainsCompactPrematchReadinessAndFriendlyRuntimeStatus()
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
                    "OverallStatusText", "LeagueClientStateText", "LeagueGameDataStateText",
                    "DouyinTargetCombo", "DouyinStateText", "MediaStateText", "TechnicalDetailsPanel",
                    "AutomationStateText", "IssuePanel", "IssueText", "RetestButton", "TestOverlayButton", "EventLog"
                })
                {
                    Assert.NotNull(window.FindName(name));
                }

                Assert.IsType<ComboBox>(window.FindName("DouyinTargetCombo"));
                Assert.IsType<ListBox>(window.FindName("EventLog"));

                var setStatus = typeof(MainWindow).GetMethod("SetRuntimeStatus", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var discovery = new DouyinDiscoveryController(new WindowsDouyinInstallationDetector());
                var coordinator = new RespawnCoordinator(
                    new RespawnOverlayWindow(),
                    AppSettings.Default,
                    text => setStatus.Invoke(window, [text]),
                    discovery);
                var handle = typeof(RespawnCoordinator).GetMethod("HandleAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var handled = (Task)handle.Invoke(coordinator, [new LifeStateSynchronized(LifeState.Alive, 1L)])!;
                Assert.True(handled.IsCompletedSuccessfully);

                Assert.Equal("正在对局 · 当前存活", ((TextBlock)window.FindName("LeagueGameDataStateText")).Text);
                Assert.Equal("正在对局", ((TextBlock)window.FindName("OverallStatusText")).Text);

                handled = (Task)handle.Invoke(coordinator, [new ConnectionBecameStale(2L)])!;
                Assert.True(handled.IsCompletedSuccessfully);
                Assert.Equal("正在重新连接", ((TextBlock)window.FindName("LeagueGameDataStateText")).Text);

                handled = (Task)handle.Invoke(coordinator, [new ConnectionRestored(3L)])!;
                Assert.True(handled.IsCompletedSuccessfully);
                Assert.Equal("正在对局 · 当前存活", ((TextBlock)window.FindName("LeagueGameDataStateText")).Text);
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
