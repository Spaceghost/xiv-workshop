using System.Numerics;

namespace XivWorkshop;

public interface IWorkshopWorld
{
    uint Territory { get; }
    Vector3? PlayerPosition { get; }
    bool WorldToScreen(Vector3 world, out Vector2 screen);
    bool ScreenToWorld(Vector2 screen, out Vector3 world);
}
