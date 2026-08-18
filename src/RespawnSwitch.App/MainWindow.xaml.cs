using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using RespawnSwitch.App.Overlay;
using RespawnSwitch.App.Browser;
using RespawnSwitch.Application.Douyin;
using RespawnSwitch.Application.Windows;
using RespawnSwitch.Windows.DouyinDiscovery;
using RespawnSwitch.Windows.Identity;
using RespawnSwitch.Windows.Media;
using RespawnSwitch.Windows.Windows;

namespace RespawnSwitch.App;

public partial class MainWindow : Window
{
    private readonly RespawnOverlayWindow overlay = new();
    private readonly DouyinDiscoveryController discovery = new(new WindowsDouyinInstallationDetector());
    private readonly LeagueClientPresenceProbe leagueClient = new(new NativeWindowSnapshotSource(), new ToolhelpProcessSnapshot());
    private readonly DispatcherTimer readinessTimer;
    private readonly BrowserBridgeState browserState = new();
    private BrowserBridgeServer? browserServer;
    private RespawnCoordinator? coordinator;
    private AppSettings settings = AppSettings.Default;
    private bool initialized;

    public MainWindow()
    {
        InitializeComponent();
        readinessTimer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, async (_, _) => await RefreshReadinessAsync(), Dispatcher);
        discovery.Changed += (_, _) => _ = Dispatcher.InvokeAsync(RefreshReadinessAsync);
        Loaded += OnLoadedAsync;
        Closed += OnClosedAsync;
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        settings = await AppSettingsStore.LoadAsync();
        DouyinTargetCombo.SelectedIndex = settings.DiscoveryMode switch { DouyinDiscoveryMode.Manual => 1, DouyinDiscoveryMode.WebOnly => 2, _ => 0 };
        initialized = true;
        browserServer = new BrowserBridgeServer(browserState, Path.Combine(AppSettingsStore.DirectoryPath, "browser-status.json"));
        try { browserServer.Start(); }
        catch (Exception ex) { SetIssue($"浏览器连接问题 · 本地桥接启动失败：{ex.GetType().Name}", true); }
        await discovery.StartAsync(settings.PreferredDouyinPath);
        StartMonitoring();
        readinessTimer.Start();
        await RefreshReadinessAsync();
    }

    private async void OnClosedAsync(object? sender, EventArgs e)
    {
        readinessTimer.Stop();
        if (coordinator is not null) await coordinator.DisposeAsync();
        await discovery.DisposeAsync();
        if (browserServer is not null) await browserServer.DisposeAsync();
        overlay.EndCycle();
    }

    private void StartMonitoring()
    {
        coordinator ??= new RespawnCoordinator(overlay, settings, SetRuntimeStatus, discovery, browserState: browserState);
        coordinator.Start();
        AddEvent("League · 正在等待对局数据");
    }

    private async Task RefreshReadinessAsync()
    {
        var league = leagueClient.Probe();
        LeagueClientStateText.Text = league.IsReady ? "已检测 · 可以赛前准备" : "未检测到客户端";
        DouyinMediaDiscovery[] mediaMatches;
        try { mediaMatches = (await DouyinGsmTcDiscovery.DiscoverAsync(CancellationToken.None)).ToArray(); }
        catch { mediaMatches = []; }
        var desktop = discovery.CurrentResult.Status == DouyinDiscoveryStatus.Found;
        var desktopMedia = mediaMatches.Length == 1;
        var web = ReadBrowserReady();
        var mode = settings.DiscoveryMode;
        var targetReady = mode switch { DouyinDiscoveryMode.Manual => desktop && desktopMedia, DouyinDiscoveryMode.WebOnly => web, _ => (desktop && desktopMedia) || web };

        DouyinStateText.Text = mode switch
        {
            DouyinDiscoveryMode.WebOnly => web ? "Chrome / Edge 抖音网页已连接" : "网页未连接",
            DouyinDiscoveryMode.Manual => desktop ? "桌面客户端已检测" : "请提前打开桌面抖音",
            _ when desktop && desktopMedia => "自动选择 · 桌面客户端",
            _ when web => "自动选择 · 抖音网页版",
            _ => "未找到可用抖音目标"
        };
        MediaStateText.Text = desktopMedia ? "GSMTC Play / Pause 已验证" : web ? "浏览器扩展 Play / Pause 已连接" : "播放控制未就绪";

        if (!league.IsReady) { SetOverall("等待 League 客户端", false); SetIssue("League 问题 · 请启动并登录 League 客户端。", true); }
        else if (!targetReady)
        {
            SetOverall("部分功能未就绪", false);
            SetIssue(mode == DouyinDiscoveryMode.WebOnly ? "抖音网页问题 · 请安装扩展并打开唯一的 douyin.com 视频标签页。" : "抖音问题 · 请提前打开视频；桌面端需要唯一 GSMTC 会话。", true);
        }
        else { SetOverall("赛前准备完成 · 等待进入对局", true); SetIssue("League、抖音和本地倒计时均已准备。进入对局后会自动连接游戏数据。", false); }
    }

    private static bool ReadBrowserReady()
    {
        var path = Path.Combine(AppSettingsStore.DirectoryPath, "browser-status.json");
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            return root.TryGetProperty("ready", out var ready) && ready.GetBoolean() && root.TryGetProperty("updatedAtUtc", out var updated) && DateTimeOffset.UtcNow - updated.GetDateTimeOffset() < TimeSpan.FromSeconds(10);
        }
        catch { return false; }
    }

    private async void Retest_Click(object sender, RoutedEventArgs e) { SetOverall("正在重新检测", false); await discovery.RescanAsync(settings.PreferredDouyinPath); await RefreshReadinessAsync(); }

    private async void DouyinTarget_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!initialized || DouyinTargetCombo.SelectedItem is not ComboBoxItem item || !Enum.TryParse<DouyinDiscoveryMode>(item.Tag?.ToString(), out var mode)) return;
        settings = settings with { DiscoveryMode = mode, OpenWebFallback = mode != DouyinDiscoveryMode.Manual };
        await AppSettingsStore.SaveAsync(settings); coordinator?.UpdateSettings(settings); await RefreshReadinessAsync();
    }

    private async void Stop_Click(object sender, RoutedEventArgs e) { if (coordinator is not null) { await coordinator.DisposeAsync(); coordinator = null; } SetOverall("已停止", false); AddEvent("系统 · 已停止监控"); }

    private void TestOverlay_Click(object sender, RoutedEventArgs e)
    {
        var area = SystemParameters.WorkArea;
        var target = new GameWindowTarget(new(new(1), Environment.ProcessId, "preview"), new((int)area.Left, (int)area.Top, (int)area.Right, (int)area.Bottom), "preview", "preview", true);
        var sample = new RespawnSwitch.Core.Game.GameSample(0, 0, "preview", true, 12, 12, 0, "preview", RespawnSwitch.Core.Game.SchemaSource.PlayerList, "preview", "阿狸", 8, 3, 11);
        overlay.BeginCycle(target, sample, RespawnSwitch.Core.Clock.LocalRespawnCountdown.Create(TimeProvider.System, 12));
        AddEvent("悬浮层 · 正在预览阿狸 8/3/11");
    }

    private void SetRuntimeStatus(string text) => Dispatcher.Invoke(() =>
    {
        AddEvent(text);
        if (text.Contains("League 对局数据已连接", StringComparison.Ordinal) || text.Contains("League 对局数据已恢复", StringComparison.Ordinal))
        {
            var dead = text.Contains("当前阵亡", StringComparison.Ordinal);
            LeagueGameDataStateText.Text = dead ? "已连接 · 当前阵亡" : "已连接 · 当前存活";
            SetOverall(dead ? "对局监控中 · 当前阵亡" : "对局监控中", true);
            SetIssue("League 对局数据读取正常。", false);
        }
        else if (text.Contains("League 数据连接问题", StringComparison.Ordinal))
        {
            LeagueGameDataStateText.Text = "连接不稳定";
            SetOverall("对局连接不稳定", false);
            SetIssue(text, true);
        }
        else if (text.Contains("已检测到死亡", StringComparison.Ordinal)) { LeagueGameDataStateText.Text = "已连接 · 当前阵亡"; SetOverall("对局监控中 · 当前阵亡", true); }
        else if (text.Contains("复活", StringComparison.Ordinal)) { LeagueGameDataStateText.Text = "已连接 · 当前存活"; SetOverall("对局监控中", true); }
        else if (text.Contains("问题", StringComparison.Ordinal) || text.Contains("未连接", StringComparison.Ordinal)) SetIssue(text, true);
    });

    private void SetOverall(string text, bool ready) { OverallStatusText.Text = text; OverallStatusLight.Fill = (System.Windows.Media.Brush)FindResource(ready ? "SuccessBrush" : "WarningBrush"); OverallStatusPill.Background = new SolidColorBrush(ready ? System.Windows.Media.Color.FromRgb(20, 54, 47) : System.Windows.Media.Color.FromRgb(44, 40, 30)); }
    private void SetIssue(string text, bool warning) { IssueText.Text = text; IssuePanel.Background = new SolidColorBrush(warning ? System.Windows.Media.Color.FromRgb(36, 27, 32) : System.Windows.Media.Color.FromRgb(20, 45, 39)); IssuePanel.BorderBrush = new SolidColorBrush(warning ? System.Windows.Media.Color.FromRgb(85, 50, 59) : System.Windows.Media.Color.FromRgb(36, 91, 73)); }
    private void AddEvent(string text) { EventLog.Items.Insert(0, $"{DateTime.Now:HH:mm:ss}  {text}"); while (EventLog.Items.Count > 60) EventLog.Items.RemoveAt(EventLog.Items.Count - 1); }
}
