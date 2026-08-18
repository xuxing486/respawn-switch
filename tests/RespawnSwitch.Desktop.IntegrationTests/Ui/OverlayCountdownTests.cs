using System.Windows.Controls;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using RespawnSwitch.App.Overlay;
using RespawnSwitch.Application.Windows;
using RespawnSwitch.Core.Clock;
using RespawnSwitch.Core.Game;

namespace RespawnSwitch.Desktop.IntegrationTests.Ui;

public sealed class OverlayCountdownTests
{
    [Fact]
    public void BeginCycle_RendersHeroKdaAndLocallyDecreasingTime()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var time = new MutableTimeProvider();
                var countdown = LocalRespawnCountdown.Create(time, 12.2);
                var overlay = new RespawnOverlayWindow();
                overlay.BeginCycle(Target(), Sample(), countdown);

                Assert.Equal("阿狸", ((TextBlock)overlay.FindName("ChampionText")).Text);
                Assert.Equal("8 / 3 / 11", ((TextBlock)overlay.FindName("KdaText")).Text);
                Assert.Equal("13", ((TextBlock)overlay.FindName("CountdownText")).Text);

                time.Advance(TimeSpan.FromMilliseconds(300));
                overlay.RefreshCountdown();
                Assert.Equal("12", ((TextBlock)overlay.FindName("CountdownText")).Text);
                overlay.EndCycle();
            }
            catch (Exception exception) { failure = exception; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        Assert.Null(failure);
    }

    [Fact]
    public void RefreshCountdown_Reasserts_overlay_above_a_later_topmost_window()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            RespawnOverlayWindow? overlay = null;
            Window? peer = null;
            try
            {
                overlay = new RespawnOverlayWindow();
                overlay.BeginCycle(Target(), Sample(), LocalRespawnCountdown.Create(TimeProvider.System, 12));
                peer = new Window { Width = 480, Height = 320, Topmost = true, ShowInTaskbar = false };
                peer.Show();
                var overlayHandle = new WindowInteropHelper(overlay).Handle;
                var peerHandle = new WindowInteropHelper(peer).Handle;
                Assert.True(SetWindowPos(peerHandle, new nint(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010));
                Assert.True(IsAbove(peerHandle, overlayHandle));

                overlay.RefreshCountdown();

                Assert.True(IsAbove(overlayHandle, peerHandle));
            }
            catch (Exception exception) { failure = exception; }
            finally
            {
                peer?.Close();
                overlay?.Close();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        Assert.Null(failure);
    }

    private static bool IsAbove(nint first, nint second)
    {
        for (var current = GetWindow(first, 0); current != 0; current = GetWindow(current, 2))
        {
            if (current == first) return true;
            if (current == second) return false;
        }
        return false;
    }

    private static GameSample Sample() => new(1, 0, "Player#NA1", true, 12.2, 12.2, 20, "CLASSIC", SchemaSource.PlayerList, "t", "阿狸", 8, 3, 11);
    private static GameWindowTarget Target() => new(new(new(1), 1, "League"), new(0, 0, 1920, 1080), "League of Legends.exe", "t", true);

    private sealed class MutableTimeProvider : TimeProvider
    {
        private long ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => ticks;
        public void Advance(TimeSpan value) => ticks += value.Ticks;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern nint GetWindow(nint hWnd, uint command);
}
