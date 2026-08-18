// Author: Stress Monster
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
        catch { SetIssue("抖音网页连接没有启动，请重新打开本程序。", true); }
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
        LeagueClientStateText.Text = league.IsReady ? "游戏已打开" : "请先打开英雄联盟";
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
            DouyinDiscoveryMode.WebOnly => web ? "抖音网页已打开" : "请先打开抖音网页",
            DouyinDiscoveryMode.Manual => desktop ? "抖音客户端已打开" : "请先打开抖音客户端",
            _ when desktop && desktopMedia => "已找到抖音客户端",
            _ when web => "已找到抖音网页",
            _ => "请先打开一个抖音视频"
        };
        MediaStateText.Text = desktopMedia || web ? "可以自动播放和暂停" : "等待视频准备好";

        if (!league.IsReady) { SetOverall("等待游戏", false); SetIssue("请启动并登录英雄联盟。", true); }
        else if (!targetReady)
        {
            SetOverall("等待抖音", false);
            SetIssue(mode == DouyinDiscoveryMode.WebOnly ? "请打开一个抖音视频网页。" : "请打开抖音并播放一次视频。", true);
        }
        else { SetOverall("准备就绪", true); SetIssue("阵亡时自动切到抖音，复活后自动返回游戏。", false); }
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

    private async void Retest_Click(object sender, RoutedEventArgs e) { SetOverall("正在重新检查", false); await discovery.RescanAsync(settings.PreferredDouyinPath); await RefreshReadinessAsync(); }

    private async void DouyinTarget_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!initialized || DouyinTargetCombo.SelectedItem is not ComboBoxItem item || !Enum.TryParse<DouyinDiscoveryMode>(item.Tag?.ToString(), out var mode)) return;
        settings = settings with { DiscoveryMode = mode, OpenWebFallback = mode != DouyinDiscoveryMode.Manual };
        await AppSettingsStore.SaveAsync(settings); coordinator?.UpdateSettings(settings); await RefreshReadinessAsync();
    }

    private async void Stop_Click(object sender, RoutedEventArgs e) { if (coordinator is not null) { await coordinator.DisposeAsync(); coordinator = null; } SetOverall("已暂停", false); SetIssue("自动切换已经暂停。", true); AddEvent("系统 · 已停止监控"); }

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
            LeagueGameDataStateText.Text = dead ? "正在对局 · 当前阵亡" : "正在对局 · 当前存活";
            SetOverall(dead ? "当前阵亡" : "正在对局", true);
            SetIssue(dead ? "已切换到抖音，悬浮窗会显示复活时间。" : "游戏连接正常。", false);
        }
        else if (text.Contains("League 数据连接问题", StringComparison.Ordinal))
        {
            LeagueGameDataStateText.Text = "正在重新连接";
            SetOverall("正在重新连接游戏", false);
            SetIssue("游戏数据暂时没有回应，正在自动重试。", true);
        }
        else if (text.Contains("已检测到死亡", StringComparison.Ordinal)) { LeagueGameDataStateText.Text = "正在对局 · 当前阵亡"; SetOverall("当前阵亡", true); }
        else if (text.Contains("复活", StringComparison.Ordinal)) { LeagueGameDataStateText.Text = "正在对局 · 当前存活"; SetOverall("正在对局", true); }
        else if (text.Contains("问题", StringComparison.Ordinal) || text.Contains("未连接", StringComparison.Ordinal)) SetIssue(FriendlyIssue(text), true);
    });

    private static string FriendlyIssue(string text)
    {
        if (text.Contains("媒体", StringComparison.OrdinalIgnoreCase) || text.Contains("播放", StringComparison.Ordinal))
            return "抖音没有开始播放，请先手动播放一次视频。";
        if (text.Contains("抖音", StringComparison.Ordinal) || text.Contains("窗口", StringComparison.Ordinal))
            return "抖音没有成功显示到前面，请让游戏、抖音和本程序使用相同权限启动。";
        if (text.Contains("League", StringComparison.OrdinalIgnoreCase) || text.Contains("游戏", StringComparison.Ordinal))
            return "游戏数据暂时没有回应，正在自动重试。";
        return "自动切换遇到问题，请点击重新检查。";
    }

    private void SetOverall(string text, bool ready) { OverallStatusText.Text = text; OverallStatusLight.Fill = (System.Windows.Media.Brush)FindResource(ready ? "SuccessBrush" : "WarningBrush"); OverallStatusPill.Background = new SolidColorBrush(ready ? System.Windows.Media.Color.FromRgb(20, 54, 47) : System.Windows.Media.Color.FromRgb(44, 40, 30)); }
    private void SetIssue(string text, bool warning) { IssueText.Text = text; IssuePanel.Background = new SolidColorBrush(warning ? System.Windows.Media.Color.FromRgb(36, 27, 32) : System.Windows.Media.Color.FromRgb(20, 45, 39)); IssuePanel.BorderBrush = new SolidColorBrush(warning ? System.Windows.Media.Color.FromRgb(85, 50, 59) : System.Windows.Media.Color.FromRgb(36, 91, 73)); }
    private void AddEvent(string text) { EventLog.Items.Insert(0, $"{DateTime.Now:HH:mm:ss}  {text}"); while (EventLog.Items.Count > 60) EventLog.Items.RemoveAt(EventLog.Items.Count - 1); }
}
