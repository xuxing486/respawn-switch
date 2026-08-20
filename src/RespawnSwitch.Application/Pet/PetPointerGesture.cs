// Author: Stress Monster
namespace RespawnSwitch.Application.Pet;

public static class PetPointerGesture
{
    public static bool HasMoved(int startX, int startY, int currentX, int currentY, int threshold)
    {
        var minimum = Math.Max(1, threshold);
        var deltaX = currentX - startX;
        var deltaY = currentY - startY;
        return deltaX * deltaX + deltaY * deltaY >= minimum * minimum;
    }
}
