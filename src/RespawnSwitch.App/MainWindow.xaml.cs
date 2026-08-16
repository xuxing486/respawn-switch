using System.Text;
using System.Windows;
using System.Windows.Media;
using RespawnSwitch.App.Overlay;
using RespawnSwitch.Application.Windows;
using RespawnSwitch.Windows.Media;
using RespawnSwitch.Windows.DouyinDiscovery;

namespace RespawnSwitch.App;

public partial class MainWindow : Window
{
    private readonly RespawnOverlayWindow overlay = new(); private RespawnCoordinator? coordinator; private AppSettings settings = AppSettings.Default;
    private readonly DouyinDiscoveryController discovery = new(new WindowsDouyinInstallationDetector());
    public MainWindow() { InitializeComponent(); Loaded += async (_, _) => { settings = await AppSettingsStore.LoadAsync(); DouyinPathText.Text = settings.PreferredDouyinPath ?? string.Empty; await discovery.StartAsync(settings.PreferredDouyinPath); await CalibrateAsync(); Start(); }; Closed += async (_, _) => { if (coordinator is not null) await coordinator.DisposeAsync(); await discovery.DisposeAsync(); overlay.Hide(); }; }
    private async Task CalibrateAsync() { try { var matches = await DouyinGsmTcDiscovery.DiscoverAsync(CancellationToken.None); IdentityText.Text = matches.Count == 1 ? $"已发现媒体身份：{matches[0].SourceAppUserModelId}" : "未发现唯一抖音媒体身份"; } catch { IdentityText.Text = "媒体身份发现不可用"; } }
    private void Start() { coordinator ??= new RespawnCoordinator(overlay, settings with { PreferredDouyinPath = DouyinPathText.Text.Trim() }, SetStatus, discovery); coordinator.Start(); }
    private async void Start_Click(object sender, RoutedEventArgs e) { settings = settings with { PreferredDouyinPath = DouyinPathText.Text.Trim() }; await AppSettingsStore.SaveAsync(settings); Start(); }
    private async void Stop_Click(object sender, RoutedEventArgs e) { if (coordinator is not null) { await coordinator.DisposeAsync(); coordinator = null; } SetStatus("已停止监控"); }
    private void TestOverlay_Click(object sender, RoutedEventArgs e)
    {
        var area = SystemParameters.WorkArea;
        var bounds = new PixelRect(
            (int)area.Left,
            (int)area.Top,
            (int)area.Right,
            (int)area.Bottom);
        var preview = new GameWindowTarget(
            new WindowIdentity(new NativeWindowHandle(1), Environment.ProcessId, "preview"),
            bounds,
            "preview",
            "preview",
            IsBorderless: true);
        overlay.ShowCountdown(preview, 12);
    }
    private void SetStatus(string text) => Dispatcher.Invoke(() => { StatusText.Text = text; LogText.Text = $"{DateTime.Now:HH:mm:ss} {text}{Environment.NewLine}{LogText.Text}"; StatusLight.Fill = text.Contains("安全跳过") ? System.Windows.Media.Brushes.Orange : System.Windows.Media.Brushes.ForestGreen; });
}
