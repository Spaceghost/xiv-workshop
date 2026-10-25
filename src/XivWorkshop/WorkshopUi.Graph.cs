using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dalamud.Bindings.ImGui;

namespace XivWorkshop;

internal sealed partial class WorkshopUi
{
    private string newType = "event";
    private string? selectedNode;
    private string? linkFrom;
    private GraphNode? draft;
    private string draftValue = "0";
    private string draftCode = "";
    private bool draftDirty;
    private long draftRevision = -1;
    private string graphDragGroup = "";
    private static readonly string[] NodeTypes = ["event", "set", "toggle", "animate", "script"];
    private static readonly string[] Properties = ["x", "y", "z", "width", "height", "depth", "rotation", "color", "visible", "label"];
    private static readonly string[] NumericProperties = ["x", "y", "z", "width", "height", "depth", "rotation"];

    private static GraphNode Clone(GraphNode node) =>
        JsonSerializer.Deserialize<GraphNode>(JsonSerializer.Serialize(node, Backend.Json), Backend.Json)!;

    private static bool Choice(string label, ref string value, IEnumerable<string> choices)
    {
        var changed = false;
        if (!ImGui.BeginCombo(label, value)) return false;
        try
        {
            foreach (var item in choices)
                if (ImGui.Selectable(item, item == value)) { value = item; changed = true; }
        }
        finally { ImGui.EndCombo(); }
        return changed;
    }

    private void DrawGraph()
    {
        if (backend.Scene.Objects.Count == 0)
        { ImGui.TextWrapped("Create an object in the Objects tab first."); return; }
        ImGui.SetNextItemWidth(145);
        Choice("New node", ref newType, NodeTypes);
        ImGui.SameLine();
        if (ImGui.Button("Add node"))
        {
            var target = backend.Scene.Objects.Find(o => o.Id == backend.State.Selected) ?? backend.Scene.Objects[0];
            var n = backend.Scene.Nodes.Count;
            Send(new { op = "node", node = new GraphNode
            {
                Type = newType, Object = target.Id, Event = "click", Property = "x",
                Value = JsonValue.Create(target.X), Code = "-- Change any object through the same API as visual nodes.\nlog(\"Hello from \" .. event)",
                X = 35 + n % 3 * 260, Y = 35 + n / 3 * 125,
            } });
            if (Error is null)
            {
                selectedNode = backend.Scene.Nodes[^1].Id;
                draft = null;
            }
        }
        ImGui.SameLine();
        if (linkFrom is not null && ImGui.Button("Cancel wire")) linkFrom = null;
        ImGui.TextWrapped(linkFrom is null
            ? "Connect an output dot to an input dot. Click a node to edit it; drag its body to move it."
            : $"Wire from {linkFrom}: click the receiving node's input dot.");
        DrawGraphCanvas();
        DrawNodeInspector();
    }

    private void DrawGraphCanvas()
    {
        const float width = 218, height = 86;
        var nodes = backend.Scene.Nodes;
        ImGui.BeginChild("graph-canvas", new Vector2(0, 295), true, ImGuiWindowFlags.HorizontalScrollbar);
        try
        {
            var origin = ImGui.GetCursorScreenPos();
            var extent = new Vector2(Math.Max(1000, nodes.Count == 0 ? 0 : nodes.Max(n => n.X) + width + 60),
                Math.Max(500, nodes.Count == 0 ? 0 : nodes.Max(n => n.Y) + height + 60));
            // Reserve scrolling space without claiming mouse input before pins.
            ImGui.Dummy(extent);
            var draw = ImGui.GetWindowDrawList();
            var pinColor = ImGui.ColorConvertFloat4ToU32(new Vector4(.42f, .8f, 1, 1));
            foreach (var link in backend.Scene.Links)
            {
                var from = nodes.Find(n => n.Id == link.From);
                var to = nodes.Find(n => n.Id == link.To);
                if (from is null || to is null) continue;
                var a = origin + new Vector2(from.X + width - 9, from.Y + 55);
                var b = origin + new Vector2(to.X + 9, to.Y + 55);
                draw.AddBezierCubic(a, a + new Vector2(90, 0), b - new Vector2(90, 0), b, pinColor, 2);
            }
            if (linkFrom is not null && nodes.Find(n => n.Id == linkFrom) is { } pending)
            {
                var a = origin + new Vector2(pending.X + width - 9, pending.Y + 55);
                var b = ImGui.GetMousePos();
                draw.AddBezierCubic(a, a + new Vector2(90, 0), b - new Vector2(90, 0), b, pinColor, 2);
            }
            foreach (var node in nodes)
            {
                var p = origin + new Vector2(node.X, node.Y);
                draw.AddRectFilled(p, p + new Vector2(width, height), 0xf02e2420, 7);
                draw.AddRect(p, p + new Vector2(width, height), selectedNode == node.Id ? pinColor : 0xff70594c, 7);
                var title = node.Type == "event" ? "On " + node.Event : node.Type == "script" ? "Lua script" : node.Type + " " + node.Property;
                draw.AddText(p + new Vector2(22, 10), 0xfff4e9e2, title);
                draw.AddText(p + new Vector2(22, 36), 0xffd0c4b0, node.Object);
                draw.AddText(p + new Vector2(22, 61), 0xffa08d7e, node.Id);
                ImGui.PushID(node.Id);
                try
                {
                    ImGui.SetCursorScreenPos(p + new Vector2(20, 0));
                    ImGui.InvisibleButton("node-body", new Vector2(width - 40, height));
                    if (ImGui.IsItemClicked()) { selectedNode = node.Id; draft = null; }
                    if (ImGui.IsItemActivated()) graphDragGroup = "node-drag:" + ++groupSerial;
                    if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                    {
                        var delta = ImGui.GetIO().MouseDelta;
                        if (delta != Vector2.Zero)
                        {
                            var moved = Clone(node);
                            moved.X = Math.Clamp(moved.X + delta.X, 0, 10000);
                            moved.Y = Math.Clamp(moved.Y + delta.Y, 0, 10000);
                            Send(new { op = "node", node = moved, group = graphDragGroup });
                        }
                    }
                    if (node.Type != "event")
                    {
                        ImGui.SetCursorScreenPos(p + new Vector2(0, 46));
                        ImGui.InvisibleButton("input", new Vector2(18, 18));
                        draw.AddCircleFilled(p + new Vector2(9, 55), 6, pinColor);
                        if (ImGui.IsItemClicked() && linkFrom is not null)
                        {
                            Send(new { op = "link", from = linkFrom, to = node.Id });
                            if (Error is null) linkFrom = null;
                        }
                    }
                    ImGui.SetCursorScreenPos(p + new Vector2(width - 18, 46));
                    ImGui.InvisibleButton("output", new Vector2(18, 18));
                    draw.AddCircleFilled(p + new Vector2(width - 9, 55), 6, pinColor);
                    if (ImGui.IsItemClicked()) linkFrom = node.Id;
                }
                finally { ImGui.PopID(); }
            }
        }
        finally { ImGui.EndChild(); }
    }

    private void DrawNodeInspector()
    {
        var node = backend.Scene.Nodes.Find(n => n.Id == selectedNode);
        if (node is null) { ImGui.TextWrapped("Select a node to edit its event, action, or Lua script."); return; }
        if (draft is null || draft.Id != node.Id || (!draftDirty && draftRevision != backend.State.Revision))
        {
            draft = Clone(node); draftCode = node.Code ?? "";
            draftValue = node.Value?.ToJsonString() ?? "0";
            draftDirty = false; draftRevision = backend.State.Revision;
        }
        ImGui.TextUnformatted($"{node.Id} · {node.Type}" + (draftDirty ? " · unapplied changes" : ""));
        var target = draft.Object;
        if (Choice("Object", ref target, backend.Scene.Objects.Select(o => o.Id)))
        { draft.Object = target; draftDirty = true; }
        if (node.Type == "event")
        {
            var evt = draft.Event ?? "click";
            if (Choice("Event", ref evt, ["click", "start", "tick"])) { draft.Event = evt; draftDirty = true; }
        }
        if (node.Type is "set" or "animate")
        {
            var property = draft.Property ?? "x";
            if (Choice("Property", ref property, node.Type == "animate" ? NumericProperties : Properties))
            {
                draft.Property = property; draftDirty = true;
                draftValue = property switch { "color" => "[1,0.4,0.7,1]", "visible" => "true", "label" => "\"Hello\"", _ => "1" };
            }
            if (property == "color")
            {
                try
                {
                    var rgba = JsonSerializer.Deserialize<float[]>(draftValue)!;
                    var color = new Vector4(rgba[0], rgba[1], rgba[2], rgba[3]);
                    if (ImGui.ColorEdit4("Value", ref color))
                    { draftValue = JsonSerializer.Serialize(new[] { color.X, color.Y, color.Z, color.W }); draftDirty = true; }
                }
                catch (Exception) { if (ImGui.InputText("RGBA value", ref draftValue, 256)) draftDirty = true; }
            }
            else if (ImGui.InputText("Value (JSON)", ref draftValue, 512)) draftDirty = true;
            if (node.Type == "animate")
            {
                var duration = draft.Duration;
                if (ImGui.DragFloat("Seconds", ref duration, .02f, .01f, 120)) { draft.Duration = duration; draftDirty = true; }
            }
        }
        if (node.Type == "script")
        {
            if (ImGui.InputTextMultiline("##script-source", ref draftCode, 16384, new Vector2(-1, 165))) draftDirty = true;
            ImGui.TextWrapped("self:get / set / animate · scene:get(id) · event · time · dt · math · log(message)");
        }
        if (ImGui.Button("Apply changes"))
        {
            Try(() =>
            {
                var apply = Clone(draft);
                // Keep a dragged node's current position when applying a text draft.
                apply.X = node.X; apply.Y = node.Y;
                if (node.Type is "set" or "animate") apply.Value = JsonNode.Parse(draftValue);
                if (node.Type == "script") apply.Code = draftCode;
                backend.Send(new { op = "node", node = apply });
                draftDirty = false; draftRevision = -1;
            });
        }
        ImGui.SameLine();
        if (ImGui.Button("Delete node"))
        { Send(new { op = "delete_node", id = node.Id }); if (Error is null) { selectedNode = null; draft = null; } }
        var links = backend.Scene.Links.Where(l => l.From == node.Id || l.To == node.Id).ToArray();
        foreach (var link in links)
        {
            ImGui.PushID(link.From + "/" + link.To);
            try
            {
                if (ImGui.SmallButton("Remove wire")) Send(new { op = "unlink", from = link.From, to = link.To });
                ImGui.SameLine(); ImGui.TextUnformatted(link.From + " → " + link.To);
            }
            finally { ImGui.PopID(); }
        }
    }
}
