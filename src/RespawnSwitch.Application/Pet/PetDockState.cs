// Author: Stress Monster
namespace RespawnSwitch.Application.Pet;

public enum PetDockEdge { Left, Right, Top, Bottom }

public sealed record PetDockState(PetDockEdge Edge, int Offset, bool IsPinned, double Scale);
