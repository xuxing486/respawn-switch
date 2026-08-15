using System.Windows;
using System.Windows.Interop;
using RespawnSwitch.Application.Windows;

namespace RespawnSwitch.App.Overlay;

public partial class RespawnOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const nint WsExTransparent = 0x00000020;
    private const nint WsExNoActivate = 0x08000000;

    public RespawnOverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var style = GetWindowLongPtr(hwnd, GwlExStyle);
            _ = SetWindowLongPtr(hwnd, GwlExStyle, style | WsExTransparent | WsExNoActivate);
        };
    }

    public void ShowCountdown(GameWindowTarget target, int seconds)
    {
        CountdownText.Text = $"复活 {Math.Max(0, seconds)}";
        Left = target.Bounds.Left + Math.Max(0, (target.Bounds.Width - ActualWidth) / 2);
        Top = target.Bounds.Top + 32;
        if (!IsVisible) Show();
        Left = target.Bounds.Left + Math.Max(0, (target.Bounds.Width - ActualWidth) / 2);
        Top = target.Bounds.Top + 32;
    }

    public new void Hide() => base.Hide();

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint hWnd, int index);

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint hWnd, int index, nint value);
}
