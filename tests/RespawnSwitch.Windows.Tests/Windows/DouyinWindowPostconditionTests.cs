using RespawnSwitch.Application.Windows;
using RespawnSwitch.Windows.Identity;
using RespawnSwitch.Windows.Windows;

namespace RespawnSwitch.Windows.Tests.Windows;

public sealed class DouyinWindowPostconditionTests
{
    [Fact]
    public void Attached_requires_visible_restored_topmost_window_at_requested_bounds()
    {
        var desired = new PixelRect(100, 0, 900, 900);

        Assert.False(DouyinWindowPostcondition.IsAttached(Window(visible: true, minimized: false, topmost: false, desired), desired));
        Assert.False(DouyinWindowPostcondition.IsAttached(Window(visible: true, minimized: true, topmost: true, desired), desired));
        Assert.False(DouyinWindowPostcondition.IsAttached(Window(visible: false, minimized: false, topmost: true, desired), desired));
        Assert.True(DouyinWindowPostcondition.IsAttached(Window(visible: true, minimized: false, topmost: true, desired), desired));
        Assert.True(DouyinWindowPostcondition.IsAttached(Window(visible: true, minimized: false, topmost: false, desired, foreground: true), desired));
    }

    [Fact]
    public void Attached_allows_small_DWM_frame_differences()
    {
        var desired = new PixelRect(100, 0, 900, 900);
        var actual = new PixelRect(98, 0, 902, 902);

        Assert.True(DouyinWindowPostcondition.IsAttached(Window(true, false, true, actual), desired));
    }

    private static NativeWindowSnapshot Window(bool visible, bool minimized, bool topmost, PixelRect bounds, bool foreground = false) => new(
        new WindowIdentity(new NativeWindowHandle(1), 7, "DouyinMain"), true, visible, false,
        bounds, bounds, bounds, new PixelRect(0, 0, 1920, 1080), 0,
        topmost ? 0x00000008 : 0, minimized, default, foreground);
}
