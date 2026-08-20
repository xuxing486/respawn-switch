// Author: Stress Monster
namespace RespawnSwitch.Application.Pet;

public sealed record PetDockPose(
    double Width,
    double Height,
    double TranslateX,
    double TranslateY,
    double Rotation,
    double Scale,
    bool ShowGrip,
    TimeSpan SnapDuration);

public static class PetDockPresentation
{
    private static readonly TimeSpan SmoothSnap = TimeSpan.FromMilliseconds(220);

    public static PetDockPose For(PetDockEdge edge) => edge switch
    {
        PetDockEdge.Top => new(154, 103, 0, -2, 0, 0.94, false, SmoothSnap),
        PetDockEdge.Bottom => new(164, 112, 0, -28, 0, 0.90, true, SmoothSnap),
        PetDockEdge.Left => new(100, 158, -24, -10, 4, 0.94, false, SmoothSnap),
        PetDockEdge.Right => new(100, 158, 24, -10, -4, 0.94, false, SmoothSnap),
        _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, null)
    };
}
