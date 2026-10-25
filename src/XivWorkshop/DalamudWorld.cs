using System.Numerics;
using Dalamud.Plugin.Services;

namespace XivWorkshop;

internal sealed class DalamudWorld(IGameGui gameGui, IClientState client, IObjectTable objects) : IWorkshopWorld
{
    public uint Territory => client.TerritoryType;
    public Vector3? PlayerPosition => objects.LocalPlayer?.Position;
    public bool WorldToScreen(Vector3 world, out Vector2 screen) => gameGui.WorldToScreen(world, out screen);
    public bool ScreenToWorld(Vector2 screen, out Vector3 world) => gameGui.ScreenToWorld(screen, out world);
}
