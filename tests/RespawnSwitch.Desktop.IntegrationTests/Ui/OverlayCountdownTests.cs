using System.Windows.Controls;
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

    private static GameSample Sample() => new(1, 0, "Player#NA1", true, 12.2, 12.2, 20, "CLASSIC", SchemaSource.PlayerList, "t", "阿狸", 8, 3, 11);
    private static GameWindowTarget Target() => new(new(new(1), 1, "League"), new(0, 0, 1920, 1080), "League of Legends.exe", "t", true);

    private sealed class MutableTimeProvider : TimeProvider
    {
        private long ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => ticks;
        public void Advance(TimeSpan value) => ticks += value.Ticks;
    }
}
