// Author: Stress Monster
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Media.Animation;
using RespawnSwitch.Application.Pet;
using RespawnSwitch.App.Overlay;
using RespawnSwitch.App.Browser;
using RespawnSwitch.Application.Douyin;
using RespawnSwitch.Application.Windows;
using RespawnSwitch.Windows.DouyinDiscovery;
using RespawnSwitch.Windows.Identity;
using RespawnSwitch.Windows.Media;
using RespawnSwitch.Windows.Windows;
using WpfButton = System.Windows.Controls.Button;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfPanel = System.Windows.Controls.Panel;

namespace RespawnSwitch.App;

public partial class MainWindow : Window
{
    private readonly RespawnOverlayWindow overlay = new();
    private readonly DouyinDiscoveryController discovery = new(new WindowsDouyinInstallationDetector());
    private readonly LeagueClientPresenceProbe leagueClient = new(new NativeWindowSnapshotSource(), new ToolhelpProcessSnapshot());
    private readonly DispatcherTimer readinessTimer;
    private readonly DispatcherTimer panelOpenTimer;
    private readonly DispatcherTimer panelCloseTimer;
    private readonly DispatcherTimer reactionTimer;
    private readonly BrowserBridgeState browserState = new();
    private BrowserBridgeServer? browserServer;
    private RespawnCoordinator? coordinator;
    private AppSettings settings = AppSettings.Default;
    private bool initialized;
    private bool panelLocked;
    private bool isPeeked;
    private bool isExpanded;
    private System.Windows.Forms.NotifyIcon? trayIcon;

    public MainWindow()
    {
        InitializeComponent();
        readinessTimer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, async (_, _) => await RefreshReadinessAsync(), Dispatcher);
        panelOpenTimer = NewOneShotTimer(TimeSpan.FromMilliseconds(320), () => { if (IsMouseOver) ShowPetPanel(); });
        panelCloseTimer = NewOneShotTimer(TimeSpan.FromMilliseconds(850), () => { if (!IsMouseOver && !panelLocked) HidePetPanel(peek: true); });
        reactionTimer = NewOneShotTimer(TimeSpan.FromSeconds(2.6), () =>
        {
            ReactionBubble.Visibility = Visibility.Collapsed;
            if (panelLocked) ShowPetPanel(); else HidePetPanel(peek: true);
        });
        discovery.Changed += (_, _) => _ = Dispatcher.InvokeAsync(RefreshReadinessAsync);
        Loaded += OnLoadedAsync;
        Closed += OnClosedAsync;
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        settings = await AppSettingsStore.LoadAsync();
        ApplyPetPlacement();
        Topmost = settings.PetPinned;
        PinButton.Content = Topmost ? "已置顶" : "置顶";
        CreateTrayIcon();
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
        panelOpenTimer.Stop(); panelCloseTimer.Stop(); reactionTimer.Stop();
        trayIcon?.Dispose();
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

    public void ShowPetPanel()
    {
        SetExpanded(true);
        RestoreFromPeek();
        ApplyPetPlacement();
        ReactionBubble.Visibility = Visibility.Collapsed;
        PetPanel.Visibility = Visibility.Visible;
        WpfPanel.SetZIndex(PetPanel, 20);
    }

    private void HidePetPanel(bool peek)
    {
        PetPanel.Visibility = Visibility.Collapsed;
        SetExpanded(false);
        ApplyPetPlacement();
        if (peek && !settings.PetPinned) PeekAtEdge();
    }

    private void ShowPetPanel_Click(object sender, RoutedEventArgs e)
    {
        panelLocked = PetPanel.Visibility != Visibility.Visible;
        if (panelLocked) ShowPetPanel(); else HidePetPanel(peek: true);
        e.Handled = true;
    }

    private void ClosePanel_Click(object sender, RoutedEventArgs e) { panelLocked = false; HidePetPanel(peek: true); e.Handled = true; }
    private async void Pin_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost; settings = settings with { PetPinned = Topmost };
        PinButton.Content = Topmost ? "已置顶" : "置顶";
        await AppSettingsStore.SaveAsync(settings); e.Handled = true;
    }

    private void PetSurface_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        panelCloseTimer.Stop(); RestoreFromPeek();
    }

    private void PetSurface_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        panelOpenTimer.Stop(); if (!panelLocked) panelCloseTimer.Start();
    }

    private void PetSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) { panelLocked = true; ShowPetPanel(); e.Handled = true; return; }
        if (IsInteractiveSource(e.OriginalSource as DependencyObject)) return;
        try { DragMove(); DockToNearestEdge(); } catch (InvalidOperationException) { }
    }

    private void HeadTouchZone_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => React("摸头", "嗯…这里很舒服，再摸一下嘛 ♥", 1.045, e);
    private void TailTouchZone_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => React("尾巴", "呀！尾巴很敏感的…别突然碰啦。", 0.955, e);
    private void HandTouchZone_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => React("击掌", "啪！今天这局也交给我吧。", 1.075, e);

    private void React(string action, string text, double scale, MouseButtonEventArgs e)
    {
        panelOpenTimer.Stop(); panelCloseTimer.Stop(); PetPanel.Visibility = Visibility.Collapsed;
        ReactionText.Text = text; ReactionBubble.Visibility = Visibility.Visible; WpfPanel.SetZIndex(ReactionBubble, 30);
        reactionTimer.Stop(); reactionTimer.Start();
        var easing = new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut };
        PetScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, scale, TimeSpan.FromMilliseconds(170)) { AutoReverse = true, EasingFunction = easing });
        PetScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, scale, TimeSpan.FromMilliseconds(170)) { AutoReverse = true, EasingFunction = easing });
        ChibiScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, scale, TimeSpan.FromMilliseconds(170)) { AutoReverse = true, EasingFunction = easing });
        ChibiScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, scale, TimeSpan.FromMilliseconds(170)) { AutoReverse = true, EasingFunction = easing });
        AddEvent($"桌宠 · {action}"); e.Handled = true;
    }

    private DispatcherTimer NewOneShotTimer(TimeSpan interval, Action action)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = interval };
        timer.Tick += (_, _) => { timer.Stop(); action(); };
        return timer;
    }

    private static bool IsInteractiveSource(DependencyObject? source)
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is WpfButton or WpfComboBox || current is FrameworkElement { Name: "HeadTouchZone" or "TailTouchZone" or "HandTouchZone" or "ChibiHeadTouchZone" or "ChibiTailTouchZone" or "ChibiHandTouchZone" }) return true;
        return false;
    }

    private void ApplyPetPlacement()
    {
        var area = SystemParameters.WorkArea;
        var width = Width * settings.PetScale; var height = Height * settings.PetScale;
        PetScaleTransform.ScaleX = settings.PetScale; PetScaleTransform.ScaleY = settings.PetScale;
        ChibiScaleTransform.ScaleX = settings.PetScale; ChibiScaleTransform.ScaleY = settings.PetScale;
        switch (settings.PetEdge)
        {
            case PetDockEdge.Left: Left = area.Left; Top = Math.Clamp(area.Top + settings.PetOffset, area.Top, area.Bottom - height); break;
            case PetDockEdge.Top: Top = area.Top; Left = Math.Clamp(area.Left + settings.PetOffset, area.Left, area.Right - width); break;
            case PetDockEdge.Bottom: Top = area.Bottom - height; Left = Math.Clamp(area.Left + settings.PetOffset, area.Left, area.Right - width); break;
            default: Left = area.Right - width; Top = Math.Clamp(area.Top + settings.PetOffset, area.Top, area.Bottom - height); break;
        }
    }

    private async void DockToNearestEdge()
    {
        var area = SystemParameters.WorkArea;
        var result = PetDockGeometry.Snap(
            new((int)area.Left, (int)area.Top, (int)area.Right, (int)area.Bottom),
            new((int)Left, (int)Top, (int)(Left + ActualWidth), (int)(Top + ActualHeight)), 28);
        Left = result.Bounds.Left; Top = result.Bounds.Top;
        if (result.Edge is { } edge)
        {
            settings = settings with { PetEdge = edge, PetOffset = result.Offset };
            await AppSettingsStore.SaveAsync(settings);
        }
    }

    private void PeekAtEdge()
    {
        if (isPeeked) return;
        var area = SystemParameters.WorkArea; const double strip = 46;
        if (settings.PetEdge == PetDockEdge.Right) Left = area.Right - strip;
        else if (settings.PetEdge == PetDockEdge.Left) Left = area.Left - Width + strip;
        isPeeked = true;
    }

    private void RestoreFromPeek() { if (!isPeeked) return; isPeeked = false; ApplyPetPlacement(); }

    private void SetExpanded(bool expanded)
    {
        if (isExpanded == expanded) return;
        isExpanded = expanded;
        Width = expanded ? 350 : 155;
        Height = expanded ? 460 : 205;
        AdultPetCharacter.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        ChibiPetCharacter.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
        StatusBead.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
    }

    private void CreateTrayIcon()
    {
        trayIcon = new System.Windows.Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Application, Text = "RespawnSwitch · Stress Monster", Visible = true };
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("显示桌宠", null, (_, _) => Dispatcher.Invoke(() => { Show(); Activate(); panelLocked = true; ShowPetPanel(); }));
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(Close));
        trayIcon.ContextMenuStrip = menu;
        trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(() => { panelLocked = true; ShowPetPanel(); });
    }

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

    private void SetOverall(string text, bool ready) { OverallStatusText.Text = text; PetStatusText.Text = text; OverallStatusLight.Fill = (System.Windows.Media.Brush)FindResource(ready ? "SuccessBrush" : "WarningBrush"); OverallStatusPill.Background = new SolidColorBrush(ready ? System.Windows.Media.Color.FromRgb(20, 54, 47) : System.Windows.Media.Color.FromRgb(44, 40, 30)); }
    private void SetIssue(string text, bool warning) { IssueText.Text = text; IssuePanel.Background = new SolidColorBrush(warning ? System.Windows.Media.Color.FromRgb(36, 27, 32) : System.Windows.Media.Color.FromRgb(20, 45, 39)); IssuePanel.BorderBrush = new SolidColorBrush(warning ? System.Windows.Media.Color.FromRgb(85, 50, 59) : System.Windows.Media.Color.FromRgb(36, 91, 73)); }
    private void AddEvent(string text) { EventLog.Items.Insert(0, $"{DateTime.Now:HH:mm:ss}  {text}"); while (EventLog.Items.Count > 60) EventLog.Items.RemoveAt(EventLog.Items.Count - 1); }
}
