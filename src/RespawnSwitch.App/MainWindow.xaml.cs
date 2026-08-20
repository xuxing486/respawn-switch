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
    private const double CompactWidth = 155;
    private const double CompactHeight = 205;
    private const double ExpandedWidth = 420;
    private const double ExpandedHeight = 390;
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

    private void ChibiHeadTouchZone_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => React("摸头", "嗯…这里很舒服，再摸一下嘛 ♥", 1.045, false, e);
    private void ChibiTailTouchZone_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => React("尾巴", "呀！尾巴很敏感的…别突然碰啦。", 0.955, false, e);
    private void ChibiHandTouchZone_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => React("击掌", "啪！今天这局也交给我吧。", 1.075, false, e);

    private void AdultHeadTouchZone_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => React("轻抚", "嗯…只有你能这样温柔地碰我。", 1.035, true, e);
    private void AdultTailTouchZone_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => React("尾巴", "尾巴很敏感…再逗我，我可要记住了。", 0.97, true, e);
    private void AdultHandTouchZone_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => React("牵手", "手给你了，这局结束前不许松开。", 1.055, true, e);

    private void React(string action, string text, double scale, bool adult, MouseButtonEventArgs e)
    {
        panelOpenTimer.Stop(); panelCloseTimer.Stop(); PetPanel.Visibility = Visibility.Collapsed;
        ReactionBubble.Width = adult ? 185 : 145;
        ReactionBubble.HorizontalAlignment = adult ? System.Windows.HorizontalAlignment.Left : System.Windows.HorizontalAlignment.Center;
        ReactionBubble.VerticalAlignment = adult ? System.Windows.VerticalAlignment.Bottom : System.Windows.VerticalAlignment.Top;
        ReactionBubble.Margin = adult ? new Thickness(5, 0, 0, 15) : new Thickness(0, 2, 0, 0);
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
        ClearWindowMotion();
        var area = SystemParameters.WorkArea;
        var width = Width; var height = Height;
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
        if (result.Edge is { } edge)
        {
            settings = settings with { PetEdge = edge, PetOffset = result.Offset };
            await AppSettingsStore.SaveAsync(settings);
            ApplyDockPresentation(edge, animate: true);
        }
        else
        {
            RestoreFreeCompact(result.Bounds.Left, result.Bounds.Top, animate: true);
        }
    }

    private void PeekAtEdge()
    {
        if (isPeeked) return;
        ApplyDockPresentation(settings.PetEdge, animate: true);
    }

    private void RestoreFromPeek()
    {
        if (!isPeeked) return;
        isPeeked = false;
        DockGrip.Visibility = Visibility.Collapsed;
        StatusBead.Visibility = Visibility.Visible;
        ResetChibiPose();
        var target = AnchoredBounds(CompactWidth, CompactHeight, settings.PetEdge, settings.PetOffset);
        AnimateWindowTo(target.Left, target.Top, target.Width, target.Height, TimeSpan.FromMilliseconds(180));
    }

    internal void ApplyDockPresentation(PetDockEdge edge, bool animate)
    {
        var pose = PetDockPresentation.For(edge);
        isPeeked = true;
        ChibiPoseScale.ScaleX = pose.Scale;
        ChibiPoseScale.ScaleY = pose.Scale;
        ChibiPoseRotate.Angle = pose.Rotation;
        ChibiDockTranslate.X = pose.TranslateX;
        ChibiDockTranslate.Y = pose.TranslateY;
        DockGrip.Visibility = pose.ShowGrip ? Visibility.Visible : Visibility.Collapsed;
        StatusBead.Visibility = Visibility.Collapsed;
        var target = AnchoredBounds(pose.Width, pose.Height, edge, settings.PetOffset);
        if (animate) AnimateWindowTo(target.Left, target.Top, target.Width, target.Height, pose.SnapDuration);
        else SetWindowBounds(target.Left, target.Top, target.Width, target.Height);
    }

    private void RestoreFreeCompact(double left, double top, bool animate)
    {
        isPeeked = false;
        DockGrip.Visibility = Visibility.Collapsed;
        StatusBead.Visibility = Visibility.Visible;
        ResetChibiPose();
        var area = SystemParameters.WorkArea;
        left = Math.Clamp(left, area.Left, Math.Max(area.Left, area.Right - CompactWidth));
        top = Math.Clamp(top, area.Top, Math.Max(area.Top, area.Bottom - CompactHeight));
        if (animate) AnimateWindowTo(left, top, CompactWidth, CompactHeight, TimeSpan.FromMilliseconds(180));
        else SetWindowBounds(left, top, CompactWidth, CompactHeight);
    }

    private static Rect AnchoredBounds(double width, double height, PetDockEdge edge, double offset)
    {
        var area = SystemParameters.WorkArea;
        var left = edge is PetDockEdge.Left or PetDockEdge.Right
            ? (edge == PetDockEdge.Left ? area.Left : area.Right - width)
            : Math.Clamp(area.Left + offset, area.Left, Math.Max(area.Left, area.Right - width));
        var top = edge is PetDockEdge.Top or PetDockEdge.Bottom
            ? (edge == PetDockEdge.Top ? area.Top : area.Bottom - height)
            : Math.Clamp(area.Top + offset, area.Top, Math.Max(area.Top, area.Bottom - height));
        return new Rect(left, top, width, height);
    }

    private void ResetChibiPose()
    {
        ChibiPoseScale.ScaleX = ChibiPoseScale.ScaleY = 1;
        ChibiPoseRotate.Angle = 0;
        ChibiDockTranslate.X = ChibiDockTranslate.Y = 0;
    }

    private void AnimateWindowTo(double left, double top, double width, double height, TimeSpan duration)
    {
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        AnimateWindowProperty(LeftProperty, left, duration, easing);
        AnimateWindowProperty(TopProperty, top, duration, easing);
        AnimateWindowProperty(WidthProperty, width, duration, easing);
        AnimateWindowProperty(HeightProperty, height, duration, easing);
    }

    private void AnimateWindowProperty(DependencyProperty property, double target, TimeSpan duration, IEasingFunction easing)
    {
        BeginAnimation(property, null);
        var animation = new DoubleAnimation((double)GetValue(property), target, duration)
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.Stop
        };
        animation.Completed += (_, _) =>
        {
            BeginAnimation(property, null);
            SetValue(property, target);
        };
        BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private void ClearWindowMotion()
    {
        foreach (var property in new[] { LeftProperty, TopProperty, WidthProperty, HeightProperty })
        {
            var value = (double)GetValue(property);
            BeginAnimation(property, null);
            SetValue(property, value);
        }
    }

    private void SetWindowBounds(double left, double top, double width, double height)
    {
        ClearWindowMotion();
        Left = left; Top = top; Width = width; Height = height;
    }

    private void SetExpanded(bool expanded)
    {
        if (isExpanded == expanded) return;
        isExpanded = expanded;
        isPeeked = false;
        ClearWindowMotion();
        Width = expanded ? ExpandedWidth : CompactWidth;
        Height = expanded ? ExpandedHeight : CompactHeight;
        ResetChibiPose();
        DockGrip.Visibility = Visibility.Collapsed;
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
