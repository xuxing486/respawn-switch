// Author: Stress Monster
using RespawnSwitch.Application.Pet;
using RespawnSwitch.Application.Windows;

namespace RespawnSwitch.Application.Tests.Pet;

public sealed class PetDockGeometryTests
{
    [Theory]
    [InlineData(PetDockEdge.Top, PetSpriteKind.Top, false, 170, 108)]
    [InlineData(PetDockEdge.Bottom, PetSpriteKind.Bottom, false, 180, 125)]
    [InlineData(PetDockEdge.Left, PetSpriteKind.Side, false, 92, 175)]
    [InlineData(PetDockEdge.Right, PetSpriteKind.Side, true, 92, 175)]
    public void DockPresentation_uses_three_assets_and_mirrors_only_the_right_side(
        PetDockEdge edge, PetSpriteKind sprite, bool mirror, double width, double height)
    {
        var pose = PetDockPresentation.For(edge);

        Assert.Equal(sprite, pose.Sprite);
        Assert.Equal(mirror, pose.Mirror);
        Assert.Equal(width, pose.Width);
        Assert.Equal(height, pose.Height);
        Assert.Equal(TimeSpan.Zero, pose.SnapDuration);
    }

    [Fact]
    public void Edge_drag_slides_along_the_top_without_leaving_the_edge()
    {
        var result = PetEdgeDragGeometry.Update(
            new PixelRect(0, 0, 1000, 800), pointerX: 620, pointerY: 12,
            PetDockEdge.Top, enterDistance: 24, exitDistance: 48);

        Assert.Equal(PetDockEdge.Top, result.Edge);
        Assert.Equal(new PixelRect(535, 0, 705, 108), result.Bounds);
    }

    [Fact]
    public void Edge_drag_returns_to_free_chibi_after_pointer_leaves_the_edge()
    {
        var result = PetEdgeDragGeometry.Update(
            new PixelRect(0, 0, 1000, 800), pointerX: 620, pointerY: 90,
            PetDockEdge.Top, enterDistance: 24, exitDistance: 48);

        Assert.Null(result.Edge);
        Assert.Equal(PetSpriteKind.Free, result.Sprite);
        Assert.Equal(new PixelRect(543, 0, 698, 205), result.Bounds);
    }

    [Fact]
    public void Free_drag_switches_to_side_asset_at_the_left_edge()
    {
        var result = PetEdgeDragGeometry.Update(
            new PixelRect(0, 0, 1000, 800), pointerX: 10, pointerY: 430,
            currentEdge: null, enterDistance: 24, exitDistance: 48);

        Assert.Equal(PetDockEdge.Left, result.Edge);
        Assert.Equal(PetSpriteKind.Side, result.Sprite);
        Assert.False(result.Mirror);
        Assert.Equal(new PixelRect(0, 343, 92, 518), result.Bounds);
    }

    [Fact]
    public void Docked_drag_keeps_the_pointer_grab_point_instead_of_jumping_to_center()
    {
        var result = PetEdgeDragGeometry.Update(
            new PixelRect(0, 0, 1000, 800), pointerX: 620, pointerY: 12,
            PetDockEdge.Top, enterDistance: 24, exitDistance: 48,
            grabOffsetX: 20, grabOffsetY: 40);

        Assert.Equal(new PixelRect(600, 0, 770, 108), result.Bounds);
    }

    [Theory]
    [InlineData(100, 100, 103, 104, false)]
    [InlineData(100, 100, 106, 100, true)]
    public void Pointer_gesture_does_not_turn_a_tap_into_a_drag(
        int startX, int startY, int currentX, int currentY, bool expected)
    {
        Assert.Equal(expected, PetPointerGesture.HasMoved(startX, startY, currentX, currentY, threshold: 6));
    }

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
