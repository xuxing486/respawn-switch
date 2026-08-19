// Author: Stress Monster
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Input;
using RespawnSwitch.App;

namespace RespawnSwitch.Desktop.IntegrationTests.Ui;

[Collection("Desktop window tests")]
public sealed class MainWindowPresentationTests
{
    [Fact]
    public void Main_window_uses_simple_mature_words_and_keeps_technical_details_hidden()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            MainWindow? window = null;
            try
            {
                if (System.Windows.Application.Current is null)
                {
                    var app = new RespawnSwitch.App.App();
                    app.InitializeComponent();
                }
                window = new MainWindow();

                Assert.True(window.AllowsTransparency);
                Assert.Equal(WindowStyle.None, window.WindowStyle);
                Assert.False(window.ShowInTaskbar);
                Assert.InRange(window.Width, 300, 380);
                Assert.InRange(window.Height, 380, 480);
                Assert.Equal("阵亡看抖音，复活回游戏", Text(window, "FriendlyTitleText"));
                Assert.Equal("英雄联盟", Text(window, "GameStatusTitleText"));
                Assert.Equal("抖音", Text(window, "DouyinStatusTitleText"));
                Assert.Equal(Visibility.Collapsed, Assert.IsType<Grid>(window.FindName("TechnicalDetailsPanel")).Visibility);
                var target = Assert.IsType<ComboBox>(window.FindName("DouyinTargetCombo"));
                var choices = target.Items.Cast<ComboBoxItem>().Select(x => x.Content?.ToString()).ToArray();
                Assert.Equal(["自动选择", "抖音客户端", "抖音网页"], choices);
                var expectedSurface = Color.FromRgb(0x24, 0x21, 0x38);
                Assert.Equal(expectedSurface, Assert.IsType<SolidColorBrush>(target.Background).Color);
                target.ApplyTemplate();
                var toggle = Assert.IsType<ToggleButton>(target.Template.FindName("DropDownToggle", target));
                toggle.ApplyTemplate();
                var surface = Assert.IsType<Border>(toggle.Template.FindName("ComboSurface", toggle));
                Assert.Equal(expectedSurface, Assert.IsType<SolidColorBrush>(surface.Background).Color);
                Assert.NotNull(Assert.IsType<Image>(window.FindName("CatgirlMascotImage")).Source);
                var petPanel = Assert.IsType<Border>(window.FindName("PetPanel"));
                Assert.Equal(Visibility.Collapsed, petPanel.Visibility);
                window.ShowPetPanel();
                Assert.Equal(Visibility.Visible, petPanel.Visibility);
                foreach (var zone in new[] { "HeadTouchZone", "HandTouchZone", "TailTouchZone" })
                    Assert.IsType<Border>(window.FindName(zone));
                var head = Assert.IsType<Border>(window.FindName("HeadTouchZone"));
                head.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                    { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
                Assert.Equal(Visibility.Visible, Assert.IsType<Border>(window.FindName("ReactionBubble")).Visibility);
                Assert.Contains("舒服", Text(window, "ReactionText"));
            }
            catch (Exception exception) { failure = exception; }
            finally
            {
                window?.Close();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        Assert.Null(failure);
    }

    private static string Text(MainWindow window, string name) => Assert.IsType<TextBlock>(window.FindName(name)).Text;
}
