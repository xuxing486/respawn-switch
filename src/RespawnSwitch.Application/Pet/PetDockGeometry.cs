// Author: Stress Monster
using RespawnSwitch.Application.Windows;

namespace RespawnSwitch.Application.Pet;

public sealed record PetDockResult(PixelRect Bounds, PetDockEdge? Edge, int Offset);

public static class PetDockGeometry
{
    public static PetDockResult Snap(PixelRect workArea, PixelRect window, int threshold)
    {
        var distances = new (PetDockEdge Edge, int Distance)[]
        {
            (PetDockEdge.Left, Math.Abs(window.Left - workArea.Left)),
            (PetDockEdge.Right, Math.Abs(workArea.Right - window.Right)),
            (PetDockEdge.Top, Math.Abs(window.Top - workArea.Top)),
            (PetDockEdge.Bottom, Math.Abs(workArea.Bottom - window.Bottom))
        };
        var nearest = distances.OrderBy(x => x.Distance).First();
        if (nearest.Distance > Math.Max(0, threshold))
            return new(Clamp(workArea, window), null, 0);

        var width = window.Width;
        var height = window.Height;
        var left = Math.Clamp(window.Left, workArea.Left, Math.Max(workArea.Left, workArea.Right - width));
        var top = Math.Clamp(window.Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - height));
        switch (nearest.Edge)
        {
            case PetDockEdge.Left: left = workArea.Left; break;
            case PetDockEdge.Right: left = workArea.Right - width; break;
            case PetDockEdge.Top: top = workArea.Top; break;
            case PetDockEdge.Bottom: top = workArea.Bottom - height; break;
        }
        var offset = nearest.Edge is PetDockEdge.Left or PetDockEdge.Right
            ? top - workArea.Top
            : left - workArea.Left;
        return new(new PixelRect(left, top, left + width, top + height), nearest.Edge, offset);
    }

    public static PixelRect PlacePeek(PixelRect workArea, PixelRect window, PetDockEdge edge, int visibleStrip)
    {
        var strip = Math.Clamp(visibleStrip, 1, edge is PetDockEdge.Left or PetDockEdge.Right ? window.Width : window.Height);
        return edge switch
        {
            PetDockEdge.Left => new(workArea.Left - window.Width + strip, window.Top, workArea.Left + strip, window.Bottom),
            PetDockEdge.Right => new(workArea.Right - strip, window.Top, workArea.Right - strip + window.Width, window.Bottom),
            PetDockEdge.Top => new(window.Left, workArea.Top - window.Height + strip, window.Right, workArea.Top + strip),
            _ => new(window.Left, workArea.Bottom - strip, window.Right, workArea.Bottom - strip + window.Height)
        };
    }

    private static PixelRect Clamp(PixelRect area, PixelRect window)
    {
        var left = Math.Clamp(window.Left, area.Left, Math.Max(area.Left, area.Right - window.Width));
        var top = Math.Clamp(window.Top, area.Top, Math.Max(area.Top, area.Bottom - window.Height));
        return new(left, top, left + window.Width, top + window.Height);
    }
}
