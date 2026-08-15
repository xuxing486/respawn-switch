using RespawnSwitch.Application.Windows;

namespace RespawnSwitch.Windows.Windows;

public static class DouyinWindowPlacement
{
    public static PixelRect PlaceOnRight(PixelRect workArea, PixelRect currentBounds)
    {
        var width = Math.Min(Math.Max(1, currentBounds.Width), workArea.Width);
        var height = Math.Min(Math.Max(1, currentBounds.Height), workArea.Height);
        return new PixelRect(workArea.Right - width, workArea.Top, workArea.Right, workArea.Top + height);
    }
}
