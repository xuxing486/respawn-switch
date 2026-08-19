// Author: Stress Monster
using RespawnSwitch.Application.Pet;
using RespawnSwitch.Application.Windows;

namespace RespawnSwitch.Application.Tests.Pet;

public sealed class PetDockGeometryTests
{
    [Theory]
    [InlineData(4, 200, 104, 300, PetDockEdge.Left, 0, 200, 100, 300)]
    [InlineData(896, 200, 996, 300, PetDockEdge.Right, 900, 200, 1000, 300)]
    [InlineData(400, 3, 500, 103, PetDockEdge.Top, 400, 0, 500, 100)]
    [InlineData(400, 696, 500, 796, PetDockEdge.Bottom, 400, 700, 500, 800)]
    public void Snap_places_window_on_nearest_edge(
        int left, int top, int right, int bottom, PetDockEdge edge,
        int expectedLeft, int expectedTop, int expectedRight, int expectedBottom)
    {
        var result = PetDockGeometry.Snap(
            new PixelRect(0, 0, 1000, 800), new PixelRect(left, top, right, bottom), 12);

        Assert.Equal(edge, result.Edge);
        Assert.Equal(new PixelRect(expectedLeft, expectedTop, expectedRight, expectedBottom), result.Bounds);
    }

    [Fact]
    public void Snap_supports_a_monitor_with_negative_origin()
    {
        var result = PetDockGeometry.Snap(
            new PixelRect(-1920, 40, 0, 1080), new PixelRect(-1917, 150, -1817, 250), 10);

        Assert.Equal(PetDockEdge.Left, result.Edge);
        Assert.Equal(new PixelRect(-1920, 150, -1820, 250), result.Bounds);
    }

    [Fact]
    public void PlacePeek_leaves_only_the_requested_visible_strip()
    {
        var result = PetDockGeometry.PlacePeek(
            new PixelRect(0, 0, 1000, 800), new PixelRect(900, 200, 1000, 300), PetDockEdge.Right, 22);

        Assert.Equal(new PixelRect(978, 200, 1078, 300), result);
    }
}
