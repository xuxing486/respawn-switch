// Author: Stress Monster
namespace RespawnSwitch.Application.Pet;

public enum PetSpriteKind { Free, Top, Bottom, Side }

public sealed record PetDockPose(PetSpriteKind Sprite, bool Mirror, double Width, double Height, TimeSpan SnapDuration);

public static class PetDockPresentation
{
    public static PetDockPose For(PetDockEdge edge) => edge switch
    {
        PetDockEdge.Top => new(PetSpriteKind.Top, false, 170, 120, TimeSpan.Zero),
        PetDockEdge.Bottom => new(PetSpriteKind.Bottom, false, 180, 125, TimeSpan.Zero),
        PetDockEdge.Left => new(PetSpriteKind.Side, false, 120, 175, TimeSpan.Zero),
        PetDockEdge.Right => new(PetSpriteKind.Side, true, 120, 175, TimeSpan.Zero),
        _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, null)
    };
}
