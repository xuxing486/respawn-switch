// Author: Stress Monster
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Reflection;
using RespawnSwitch.Application.Pet;
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
                Assert.InRange(window.Width, 140, 180);
                Assert.InRange(window.Height, 180, 230);
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
                Assert.Equal(Visibility.Visible, Assert.IsType<Grid>(window.FindName("ChibiPetCharacter")).Visibility);
                Assert.Equal(Visibility.Collapsed, Assert.IsType<Grid>(window.FindName("AdultPetCharacter")).Visibility);
                window.ShowPetPanel();
                Assert.Equal(Visibility.Visible, petPanel.Visibility);
                Assert.InRange(window.Width, 400, 420);
                Assert.InRange(window.Height, 360, 400);
                Assert.Equal(Visibility.Collapsed, Assert.IsType<Grid>(window.FindName("ChibiPetCharacter")).Visibility);
                var adult = Assert.IsType<Grid>(window.FindName("AdultPetCharacter"));
                Assert.Equal(Visibility.Visible, adult.Visibility);
                Assert.Equal(HorizontalAlignment.Left, adult.HorizontalAlignment);
                Assert.Equal(HorizontalAlignment.Right, petPanel.HorizontalAlignment);
                Assert.True(adult.Width + petPanel.Width <= window.Width);
                foreach (var zone in new[] { "HeadTouchZone", "HandTouchZone", "TailTouchZone" })
                    Assert.IsType<Border>(window.FindName(zone));
                var head = Assert.IsType<Border>(window.FindName("HeadTouchZone"));
                head.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                    { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
                Assert.Equal(Visibility.Visible, Assert.IsType<Border>(window.FindName("ReactionBubble")).Visibility);
                Assert.Contains("只有你", Text(window, "ReactionText"));
                Assert.Equal(Visibility.Visible, Assert.IsType<Grid>(window.FindName("AdultPetCharacter")).Visibility);
                Assert.Equal(Visibility.Collapsed, petPanel.Visibility);
                foreach (var imageName in new[] { "TopDockImage", "BottomDockImage", "SideDockImage" })
                    Assert.True(Assert.IsType<Image>(window.FindName(imageName)).Source is not null, imageName);
                foreach (var zoneName in new[] { "DockHeadTouchZone", "DockPawTouchZone", "DockTailTouchZone" })
                {
                    var zone = Assert.IsType<Border>(window.FindName(zoneName));
                    var method = typeof(MainWindow).GetMethod("IsInteractiveSource", BindingFlags.NonPublic | BindingFlags.Static);
                    Assert.NotNull(method);
                    Assert.False(Assert.IsType<bool>(method.Invoke(null, [zone])));
                }
                AssertDockAssetTouchesEdge(Assert.IsType<Image>(window.FindName("TopDockImage")), PetDockPresentation.For(PetDockEdge.Top), PetDockEdge.Top);
                AssertDockAssetTouchesEdge(Assert.IsType<Image>(window.FindName("BottomDockImage")), PetDockPresentation.For(PetDockEdge.Bottom), PetDockEdge.Bottom);
                AssertDockAssetTouchesEdge(Assert.IsType<Image>(window.FindName("SideDockImage")), PetDockPresentation.For(PetDockEdge.Left), PetDockEdge.Left);
                window.Width = PetDockPresentation.For(PetDockEdge.Left).Width;
                var react = typeof(MainWindow).GetMethod("React", BindingFlags.Instance | BindingFlags.NonPublic, null,
                    [typeof(string), typeof(string), typeof(double), typeof(bool)], null);
                Assert.NotNull(react);
                react.Invoke(window, ["贴边交互", "贴在这里也能摸到我，嘿嘿 ♥", 1.045, false]);
                var reaction = Assert.IsType<Border>(window.FindName("ReactionBubble"));
                Assert.True(reaction.Width <= window.Width - 4, "The dock reaction bubble must fit inside the narrow side window.");
                var showDock = typeof(MainWindow).GetMethod("ShowDockSprite", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(showDock);
                var dockScale = Assert.IsType<ScaleTransform>(window.FindName("DockScaleTransform"));
                showDock.Invoke(window, [PetDockEdge.Left]); Assert.Equal(0, dockScale.CenterX);
                showDock.Invoke(window, [PetDockEdge.Right]); Assert.Equal(PetDockPresentation.For(PetDockEdge.Right).Width, dockScale.CenterX);
                showDock.Invoke(window, [PetDockEdge.Top]); Assert.Equal(0, dockScale.CenterY);
                showDock.Invoke(window, [PetDockEdge.Bottom]); Assert.Equal(PetDockPresentation.For(PetDockEdge.Bottom).Height, dockScale.CenterY);
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

    private static void AssertDockAssetTouchesEdge(Image image, PetDockPose pose, PetDockEdge edge)
    {
        var source = Assert.IsAssignableFrom<BitmapSource>(image.Source);
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        var minX = converted.PixelWidth;
        var minY = converted.PixelHeight;
        var maxX = -1;
        var maxY = -1;
        for (var y = 0; y < converted.PixelHeight; y++)
        for (var x = 0; x < converted.PixelWidth; x++)
        {
            if (pixels[y * stride + x * 4 + 3] <= 8) continue;
            minX = Math.Min(minX, x); minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
        }
        var scale = Math.Min(pose.Width / converted.PixelWidth, pose.Height / converted.PixelHeight);
        var horizontalInset = (pose.Width - converted.PixelWidth * scale) / 2;
        var verticalInset = (pose.Height - converted.PixelHeight * scale) / 2;
        var gap = edge switch
        {
            PetDockEdge.Left => horizontalInset + minX * scale,
            PetDockEdge.Right => horizontalInset + (converted.PixelWidth - 1 - maxX) * scale,
            PetDockEdge.Top => verticalInset + minY * scale,
            PetDockEdge.Bottom => verticalInset + (converted.PixelHeight - 1 - maxY) * scale,
            _ => double.MaxValue
        };
        Assert.True(gap <= 1, $"{edge} sprite leaves a {gap:F1}px visual gap from the screen edge.");
    }
}
