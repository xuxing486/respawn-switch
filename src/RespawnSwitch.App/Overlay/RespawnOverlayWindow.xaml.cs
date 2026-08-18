using System.Windows;
using System.Windows.Interop;
using RespawnSwitch.Application.Windows;
using RespawnSwitch.Core.Clock;
using RespawnSwitch.Core.Game;
using System.Windows.Threading;

namespace RespawnSwitch.App.Overlay;

public partial class RespawnOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const nint WsExTransparent = 0x00000020;
    private const nint WsExNoActivate = 0x08000000;
    private const nint HwndTopmost = -1;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private readonly DispatcherTimer timer;
    private LocalRespawnCountdown? countdown;

    public RespawnOverlayWindow()
    {
        InitializeComponent();
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Render, (_, _) => RefreshCountdown(), Dispatcher);
        timer.Stop();
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var style = GetWindowLongPtr(hwnd, GwlExStyle);
            _ = SetWindowLongPtr(hwnd, GwlExStyle, style | WsExTransparent | WsExNoActivate);
        };
    }

    public void ShowCountdown(GameWindowTarget target, int seconds)
    {
        var sample = new GameSample(0, 0, "preview", true, seconds, seconds, 0, "preview", SchemaSource.PlayerList, "preview", "倒计时预览");
        BeginCycle(target, sample, LocalRespawnCountdown.Create(TimeProvider.System, Math.Max(0, seconds)));
    }

    public void BeginCycle(GameWindowTarget target, GameSample sample, LocalRespawnCountdown localCountdown)
    {
        countdown = localCountdown;
        ChampionText.Text = string.IsNullOrWhiteSpace(sample.ChampionName) ? "未知英雄" : sample.ChampionName;
        KdaText.Text = $"{sample.Kills} / {sample.Deaths} / {sample.Assists}";
        OverlayStatusText.Text = "本地复活计时";
        RefreshCountdown();
        Left = target.Bounds.Left + Math.Max(0, (target.Bounds.Width - ActualWidth) / 2);
        Top = target.Bounds.Top + 32;
        if (!IsVisible) Show();
        Left = target.Bounds.Left + Math.Max(0, (target.Bounds.Width - ActualWidth) / 2);
        Top = target.Bounds.Top + 32;
        timer.Start();
    }

    public void RefreshCountdown()
    {
        if (countdown is null) return;
        var frame = countdown.Snapshot();
        CountdownText.Text = frame.DisplaySeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        OverlayStatusText.Text = frame.AwaitingRespawnConfirmation ? "正在确认复活" : "本地复活计时";
        if (IsVisible)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            _ = SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoActivate);
        }
    }

    public void MarkConnectionUnstable() => OverlayStatusText.Text = "连接不稳定";

    public void EndCycle()
    {
        timer.Stop();
        countdown = null;
        base.Hide();
    }

    public new void Hide() => EndCycle();

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint hWnd, int index);

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint hWnd, int index, nint value);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
}
