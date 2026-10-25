using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace XivWorkshop;

internal sealed partial class WorkshopUi
{
    private string objectDragGroup = "";

    public void DrawObjects()
    {
        foreach (var o in backend.State.Objects)
        {
            if (!o.Visible && backend.State.Playing) continue;
            if (o.IsWorld) DrawWorldObject(o);
            else DrawScreenObject(o);
        }
        if (placing is not null)
        {
            if (!backend.Scene.Objects.Any(o => o.Id == placing) || backend.State.Playing)
            { placing = null; return; }
            var mouse = ImGui.GetMousePos();
            ImGui.GetForegroundDrawList().AddText(mouse + new Vector2(16, 18), 0xffffffff, "Click ground to place · Escape cancels");
            if (ImGui.IsKeyPressed(ImGuiKey.Escape)) placing = null;
            else if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsWindowHovered(ImGuiHoveredFlags.AnyWindow))
            {
                if (world.ScreenToWorld(mouse, out var p))
                {
                    var id = placing;
                    Try(() => backend.Edit(id, new() { ["x"] = p.X, ["y"] = p.Y + .5f,
                        ["z"] = p.Z, ["territory"] = world.Territory }));
                    if (Error is null) placing = null;
                }
                else Error = "No ground at that point. Try a closer surface.";
            }
        }
    }

    private static Vector4 Color(SceneObject o)
    {
        var c = o.Color;
        return new Vector4(c[0], c[1], c[2], o.Visible ? c[3] : Math.Min(.25f, c[3]));
    }

    private void DrawScreenObject(SceneObject o)
    {
        var pos = ImGui.GetMainViewport().WorkPos + new Vector2(o.X, o.Y);
        var size = new Vector2(Math.Max(24, o.Width), Math.Max(24, o.Height));
        ImGui.SetNextWindowPos(pos);
        ImGui.SetNextWindowSize(size);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, Vector2.One);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);
        try
        {
            var name = "##workshop-object-" + o.Id;
            var shown = ImGui.Begin(name, FlagsForObject(name) | ImGuiWindowFlags.NoBackground);
            try
            {
                if (!shown) return;
                var draw = ImGui.GetWindowDrawList();
                var tint = ImGui.ColorConvertFloat4ToU32(Color(o));
                if (o.Kind != "label") draw.AddRectFilled(pos, pos + size, tint, 7);
                var selected = backend.State.Selected == o.Id && !backend.State.Playing;
                if (selected) draw.AddRect(pos, pos + size, 0xffffffff, 7, ImDrawFlags.None, 2);
                var textSize = ImGui.CalcTextSize(o.Label);
                var textPos = pos + (size - textSize) / 2;
                draw.PushClipRect(pos, pos + size, true);
                draw.AddText(textPos, o.Kind == "label" ? tint : 0xffffffff, o.Label);
                draw.PopClipRect();
                var pressed = ImGui.InvisibleButton("object-hit", size);
                var clicked = ImGui.IsItemClicked();
                var activated = ImGui.IsItemActivated();
                var active = ImGui.IsItemActive();
                if (backend.State.Playing)
                {
                    if (pressed) Send(new { op = "click", id = o.Id });
                }
                else
                {
                    if (clicked) Send(new { op = "select", id = o.Id });
                    if (activated) objectDragGroup = "object-drag:" + ++groupSerial;
                    if (active && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                    {
                        var delta = ImGui.GetIO().MouseDelta;
                        if (delta != Vector2.Zero)
                            Try(() => backend.Edit(o.Id, new() { ["x"] = o.X + delta.X, ["y"] = o.Y + delta.Y }, objectDragGroup));
                    }
                }
            }
            finally { ImGui.End(); }
        }
        finally { ImGui.PopStyleColor(); ImGui.PopStyleVar(2); }
    }

    private void DrawWorldObject(SceneObject o)
    {
        if (world.PlayerPosition is null || o.Territory != world.Territory) return;
        var position = new Vector3(o.X, o.Y, o.Z);
        if (!world.WorldToScreen(position, out var center)) return;
        var view = ImGui.GetMainViewport();
        if (center.X < view.Pos.X || center.Y < view.Pos.Y ||
            center.X > view.Pos.X + view.Size.X || center.Y > view.Pos.Y + view.Size.Y) return;
        var color = ImGui.ColorConvertFloat4ToU32(Color(o));
        var draw = ImGui.GetBackgroundDrawList();
        if (o.Kind == "orb")
        {
            // Screen radius follows world scale rather than a fixed marker size.
            var radius = 12f;
            if (world.WorldToScreen(position + Vector3.UnitY * o.Height / 2, out var edge))
                radius = Math.Clamp(Vector2.Distance(center, edge), 3, 800);
            var rgba = Color(o); rgba.W *= .28f;
            draw.AddCircleFilled(center, radius, ImGui.ColorConvertFloat4ToU32(rgba));
            draw.AddCircle(center, radius, color, 48, 2);
        }
        else
        {
            var corners = new Vector2[8]; var visible = new bool[8];
            for (var i = 0; i < 8; i++) visible[i] = world.WorldToScreen(backend.BoxVertex(o, i), out corners[i]);
            for (var i = 0; i < 8; i++)
                foreach (var bit in new[] { 1, 2, 4 })
                {
                    var j = i ^ bit;
                    if (j > i && visible[i] && visible[j]) draw.AddLine(corners[i], corners[j], color, 2);
                }
        }
        if (backend.State.Selected == o.Id && !backend.State.Playing)
        {
            draw.AddCircle(center, 18, 0xffffffff, 24, 2);
            draw.AddText(center + new Vector2(22, -8), 0xffffffff, o.Label);
        }
        // Only a small explicit handle intercepts input; the shape's full bounds do not.
        ImGui.SetNextWindowPos(center - new Vector2(14));
        ImGui.SetNextWindowSize(new Vector2(28));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, Vector2.One);
        try
        {
            var name = "##workshop-world-" + o.Id;
            var shown = ImGui.Begin(name, FlagsForObject(name) | ImGuiWindowFlags.NoBackground);
            try
            {
                if (!shown) return;
                if (ImGui.InvisibleButton("world-hit", new Vector2(28))) Send(new { op = "click", id = o.Id });
                ImGui.GetWindowDrawList().AddCircleFilled(center, 4, color);
            }
            finally { ImGui.End(); }
        }
        finally { ImGui.PopStyleVar(2); }
    }
}
