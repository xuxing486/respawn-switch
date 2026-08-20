// Author: Stress Monster
using RespawnSwitch.Application.Windows;

namespace RespawnSwitch.Application.Pet;

public sealed record PetEdgeDragResult(PixelRect Bounds, PetDockEdge? Edge, PetSpriteKind Sprite, bool Mirror);

public static class PetEdgeDragGeometry
{
    private const int FreeWidth = 155;
    private const int FreeHeight = 205;

    public static PetEdgeDragResult Update(
        PixelRect workArea,
        int pointerX,
        int pointerY,
        PetDockEdge? currentEdge,
        int enterDistance,
        int exitDistance,
        int grabOffsetX = -1,
        int grabOffsetY = -1)
    {
        var edge = currentEdge;
        if (edge is { } docked && HasLeftEdge(workArea, pointerX, pointerY, docked, exitDistance))
            edge = null;
        if (edge is null)
            edge = FindEntryEdge(workArea, pointerX, pointerY, enterDistance);

        if (edge is { } target)
        {
            var pose = PetDockPresentation.For(target);
            var width = (int)pose.Width;
            var height = (int)pose.Height;
            var dockGrabX = grabOffsetX < 0 ? width / 2 : Math.Clamp(grabOffsetX, 0, width);
            var dockGrabY = grabOffsetY < 0 ? height / 2 : Math.Clamp(grabOffsetY, 0, height);
            var left = target switch
            {
                PetDockEdge.Left => workArea.Left,
                PetDockEdge.Right => workArea.Right - width,
                _ => Math.Clamp(pointerX - dockGrabX, workArea.Left, workArea.Right - width)
            };
            var top = target switch
            {
                PetDockEdge.Top => workArea.Top,
                PetDockEdge.Bottom => workArea.Bottom - height,
                _ => Math.Clamp(pointerY - dockGrabY, workArea.Top, workArea.Bottom - height)
            };
            return new(new PixelRect(left, top, left + width, top + height), target, pose.Sprite, pose.Mirror);
        }

        var freeGrabX = grabOffsetX < 0 ? FreeWidth / 2 : Math.Clamp(grabOffsetX, 0, FreeWidth);
        var freeGrabY = grabOffsetY < 0 ? FreeHeight / 2 : Math.Clamp(grabOffsetY, 0, FreeHeight);
        var freeLeft = Math.Clamp(pointerX - freeGrabX, workArea.Left, workArea.Right - FreeWidth);
        var freeTop = Math.Clamp(pointerY - freeGrabY, workArea.Top, workArea.Bottom - FreeHeight);
        return new(new PixelRect(freeLeft, freeTop, freeLeft + FreeWidth, freeTop + FreeHeight), null, PetSpriteKind.Free, false);
    }

    private static bool HasLeftEdge(PixelRect area, int x, int y, PetDockEdge edge, int exitDistance) => edge switch
    {
        PetDockEdge.Top => y - area.Top > exitDistance,
        PetDockEdge.Bottom => area.Bottom - y > exitDistance,
        PetDockEdge.Left => x - area.Left > exitDistance,
        PetDockEdge.Right => area.Right - x > exitDistance,
        _ => true
    };

    private static PetDockEdge? FindEntryEdge(PixelRect area, int x, int y, int enterDistance)
    {
        var nearest = new (PetDockEdge Edge, int Distance)[]
        {
            (PetDockEdge.Left, Math.Abs(x - area.Left)),
            (PetDockEdge.Right, Math.Abs(area.Right - x)),
            (PetDockEdge.Top, Math.Abs(y - area.Top)),
            (PetDockEdge.Bottom, Math.Abs(area.Bottom - y))
        }.OrderBy(candidate => candidate.Distance).First();
        return nearest.Distance <= Math.Max(0, enterDistance) ? nearest.Edge : null;
    }
}
