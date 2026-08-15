using RespawnSwitch.Application.Windows;
using RespawnSwitch.Windows.Windows;

namespace RespawnSwitch.Windows.Tests.Windows;

public sealed class DouyinWindowCalibrationTests
{
    [Fact]
    public void SelectUniqueWindowClass_ReturnsClassOnlyForOneVisibleTopLevelExactPath()
    {
        var result = DouyinWindowCalibration.SelectUniqueDouyinWindowClass(
            [new DouyinWindowCandidate(@"D:\douyin\douyin.exe", "DouyinMain", true, true, false, new NativeWindowHandle(1))],
            @"D:\douyin\douyin.exe");

        Assert.Equal("DouyinMain", result);
    }

    [Fact]
    public void SelectUniqueWindowClass_ReturnsNullWhenPathHasMultipleWindows()
    {
        var result = DouyinWindowCalibration.SelectUniqueDouyinWindowClass(
            [new(@"D:\douyin\douyin.exe", "A", true, true, false, new NativeWindowHandle(1)), new(@"D:\douyin\douyin.exe", "B", true, true, false, new NativeWindowHandle(2))],
            @"D:\douyin\douyin.exe");

        Assert.Null(result);
    }
}
