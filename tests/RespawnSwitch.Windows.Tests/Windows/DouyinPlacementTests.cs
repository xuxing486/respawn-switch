using RespawnSwitch.Application.Windows;
using RespawnSwitch.Windows.Windows;

namespace RespawnSwitch.Windows.Tests.Windows;

public sealed class DouyinPlacementTests
{
    [Fact]
    public void PlaceOnRight_keeps_the_window_on_the_same_monitor_work_area()
    {
        var result = DouyinWindowPlacement.PlaceOnRight(new PixelRect(0, 0, 1920, 1080), new PixelRect(100, 100, 700, 900));

        Assert.Equal(new PixelRect(1320, 0, 1920, 800), result);
    }
}
