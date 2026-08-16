using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using RespawnSwitch.App.Overlay;
using RespawnSwitch.Application.Douyin;
using RespawnSwitch.Application.Windows;
using RespawnSwitch.Windows.DouyinDiscovery;
using RespawnSwitch.Windows.Media;

namespace RespawnSwitch.App;

public partial class MainWindow : Window
{
    private readonly RespawnOverlayWindow overlay = new();
    private readonly DouyinDiscoveryController discovery = new(new WindowsDouyinInstallationDetector());
    private RespawnCoordinator? coordinator;
    private AppSettings settings = AppSettings.Default;
    private bool initialized;

    public MainWindow()
    {
        InitializeComponent();
        discovery.Changed += Discovery_Changed;
        Loaded += OnLoadedAsync;
        Closed += OnClosedAsync;
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        settings = await AppSettingsStore.LoadAsync();
        AutoDetectToggle.IsChecked = settings.AutoDetectDouyin;
        WebFallbackToggle.IsChecked = settings.OpenWebFallback;
        DiscoveryModeCombo.SelectedIndex = settings.DiscoveryMode switch
        {
            DouyinDiscoveryMode.Manual => 1,
            DouyinDiscoveryMode.WebOnly => 2,
            _ => 0
        };
        DouyinPathText.Text = settings.PreferredDouyinPath ?? "尚未确认";
        initialized = true;

        if (settings.AutoDetectDouyin && settings.DiscoveryMode != DouyinDiscoveryMode.WebOnly)
        {
            await discovery.StartAsync(settings.PreferredDouyinPath);
        }
        else
        {
            UpdateDiscoveryUi(discovery.CurrentResult);
        }

        await CalibrateAsync();
        StartMonitoring();
    }

    private async void OnClosedAsync(object? sender, EventArgs e)
    {
        discovery.Changed -= Discovery_Changed;
        if (coordinator is not null)
        {
            await coordinator.DisposeAsync();
        }

        await discovery.DisposeAsync();
        overlay.Hide();
    }

    private async Task CalibrateAsync()
    {
        try
        {
            var matches = await DouyinGsmTcDiscovery.DiscoverAsync(CancellationToken.None);
            IdentityText.Text = matches.Count == 1 ? "已发现抖音媒体会话" : "等待抖音媒体会话";
        }
        catch (Exception)
        {
            IdentityText.Text = "媒体发现暂不可用";
        }
    }

    private void StartMonitoring()
    {
        coordinator ??= new RespawnCoordinator(overlay, settings, SetStatus, discovery);
        coordinator.Start();
        SetStatus("正在监控 League Live Client…");
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        await SaveSettingsAsync();
        StartMonitoring();
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (coordinator is not null)
        {
            await coordinator.DisposeAsync();
            coordinator = null;
        }

        SetStatus("已停止监控");
    }

    private async void Rescan_Click(object sender, RoutedEventArgs e)
    {
        SetStatus("正在扫描所有本机固定磁盘…");
        await discovery.RescanAsync(settings.PreferredDouyinPath);
    }

    private async void CancelScan_Click(object sender, RoutedEventArgs e)
    {
        await discovery.CancelAsync();
        SetStatus("已取消抖音扫描");
    }

    private async void BrowseDouyin_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择抖音客户端 douyin.exe",
            Filter = "抖音客户端 (douyin.exe)|douyin.exe",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var validator = new DouyinCandidateValidator(settings.LastValidatedSignatureThumbprint);
        var candidate = await validator.ValidateAsync(
            dialog.FileName,
            DouyinDiscoverySource.SavedPath,
            isRunning: false,
            CancellationToken.None);
        if (candidate is null)
        {
            SetStatus("所选文件未通过抖音身份验证");
            return;
        }

        await SelectCandidateAsync(candidate);
        await discovery.RescanAsync(candidate.NormalizedPath);
    }

    private async void CandidateList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!initialized || CandidateList.SelectedItem is not DouyinCandidate candidate)
        {
            return;
        }

        await SelectCandidateAsync(candidate);
    }

    private async Task SelectCandidateAsync(DouyinCandidate candidate)
    {
        settings = settings with
        {
            PreferredDouyinPath = candidate.NormalizedPath,
            LastValidatedSignatureThumbprint = candidate.SignatureThumbprint,
            DiscoveryMode = DouyinDiscoveryMode.Manual
        };
        DouyinPathText.Text = candidate.NormalizedPath;
        DiscoveryModeCombo.SelectedIndex = 1;
        await AppSettingsStore.SaveAsync(settings);
        coordinator?.UpdateSettings(settings);
        SetStatus("已保存经过验证的抖音客户端路径");
    }

    private async void SettingToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!initialized)
        {
            return;
        }

        settings = settings with
        {
            AutoDetectDouyin = AutoDetectToggle.IsChecked == true,
            OpenWebFallback = WebFallbackToggle.IsChecked == true
        };
        await AppSettingsStore.SaveAsync(settings);
        coordinator?.UpdateSettings(settings);
    }

    private async void DiscoveryMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!initialized || DiscoveryModeCombo.SelectedItem is not ComboBoxItem item ||
            !Enum.TryParse<DouyinDiscoveryMode>(item.Tag?.ToString(), out var mode))
        {
            return;
        }

        settings = settings with { DiscoveryMode = mode };
        await AppSettingsStore.SaveAsync(settings);
        coordinator?.UpdateSettings(settings);
        if (mode == DouyinDiscoveryMode.WebOnly)
        {
            await discovery.CancelAsync();
            SetStatus("已切换为仅使用抖音网页版");
        }
    }

    private Task SaveSettingsAsync()
    {
        settings = settings with
        {
            AutoDetectDouyin = AutoDetectToggle.IsChecked == true,
            OpenWebFallback = WebFallbackToggle.IsChecked == true
        };
        coordinator?.UpdateSettings(settings);
        return AppSettingsStore.SaveAsync(settings);
    }

    private void Discovery_Changed(object? sender, DouyinDiscoveryResult result) =>
        Dispatcher.InvokeAsync(() => UpdateDiscoveryUi(result));

    private void UpdateDiscoveryUi(DouyinDiscoveryResult result)
    {
        DiscoveryProgressBar.IsIndeterminate = result.Status == DouyinDiscoveryStatus.Scanning;
        DiscoveryProgressBar.Value = result.Status == DouyinDiscoveryStatus.Found ? 100 : 0;
        CancelScanButton.IsEnabled = result.Status == DouyinDiscoveryStatus.Scanning;
        CandidateList.ItemsSource = result.Candidates;
        var progress = result.Progress;
        DiscoveryProgressText.Text = result.Status == DouyinDiscoveryStatus.Scanning
            ? $"{progress.CurrentDrive ?? "本机"} · 已检查 {progress.DirectoriesScanned:N0} 个文件夹 · 找到 {progress.CandidatesFound} 个候选"
            : $"已检查 {progress.DirectoriesScanned:N0} 个文件夹 · 跳过 {progress.DirectoriesSkipped:N0} 个受保护目录";

        switch (result.Status)
        {
            case DouyinDiscoveryStatus.Found when result.Selected is not null:
                DouyinStateText.Text = "已确认客户端";
                DouyinPathSummary.Text = result.Selected.NormalizedPath;
                DouyinPathText.Text = result.Selected.NormalizedPath;
                _ = PersistDiscoveredCandidateAsync(result.Selected);
                break;
            case DouyinDiscoveryStatus.Scanning:
                DouyinStateText.Text = "正在扫描整台电脑";
                DouyinPathSummary.Text = progress.CurrentDirectory ?? "先检查快速来源";
                break;
            case DouyinDiscoveryStatus.Ambiguous:
                DouyinStateText.Text = "发现多个同级候选";
                DouyinPathSummary.Text = "请从列表中手动选择";
                break;
            case DouyinDiscoveryStatus.NotFound:
                DouyinStateText.Text = settings.OpenWebFallback ? "将使用网页版" : "未找到客户端";
                DouyinPathSummary.Text = "https://www.douyin.com/";
                break;
            case DouyinDiscoveryStatus.Cancelled:
                DouyinStateText.Text = "扫描已取消";
                DouyinPathSummary.Text = "可随时重新扫描";
                break;
            case DouyinDiscoveryStatus.Failed:
                DouyinStateText.Text = "扫描遇到错误";
                DouyinPathSummary.Text = "可重试或使用网页版";
                break;
            default:
                DouyinStateText.Text = "等待扫描";
                DouyinPathSummary.Text = "尚未开始";
                break;
        }
    }

    private async Task PersistDiscoveredCandidateAsync(DouyinCandidate candidate)
    {
        if (string.Equals(settings.PreferredDouyinPath, candidate.NormalizedPath, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(settings.LastValidatedSignatureThumbprint, candidate.SignatureThumbprint, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        settings = settings with
        {
            PreferredDouyinPath = candidate.NormalizedPath,
            LastValidatedSignatureThumbprint = candidate.SignatureThumbprint
        };
        await AppSettingsStore.SaveAsync(settings);
        coordinator?.UpdateSettings(settings);
    }

    private void TestOverlay_Click(object sender, RoutedEventArgs e)
    {
        var area = SystemParameters.WorkArea;
        var bounds = new PixelRect((int)area.Left, (int)area.Top, (int)area.Right, (int)area.Bottom);
        var preview = new GameWindowTarget(
            new WindowIdentity(new NativeWindowHandle(1), Environment.ProcessId, "preview"),
            bounds,
            "preview",
            "preview",
            IsBorderless: true);
        overlay.ShowCountdown(preview, 12);
        SetStatus("正在预览悬浮倒计时");
    }

    private void SetStatus(string text)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = text;
            EventLog.Items.Insert(0, $"{DateTime.Now:HH:mm:ss}  {text}");
            while (EventLog.Items.Count > 80)
            {
                EventLog.Items.RemoveAt(EventLog.Items.Count - 1);
            }

            var warning = text.Contains("未找到", StringComparison.Ordinal) ||
                          text.Contains("无法", StringComparison.Ordinal) ||
                          text.Contains("取消", StringComparison.Ordinal);
            StatusLight.Fill = (System.Windows.Media.Brush)FindResource(warning ? "WarningBrush" : "SuccessBrush");
            MonitoringStatusPill.Background = new SolidColorBrush(warning
                ? System.Windows.Media.Color.FromRgb(70, 51, 23)
                : System.Windows.Media.Color.FromRgb(23, 61, 50));
        });
    }
}
