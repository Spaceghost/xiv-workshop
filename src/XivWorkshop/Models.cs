using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace XivWorkshop;

public sealed class SceneObject
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "panel";
    public string Label { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Width { get; set; } = 180;
    public float Height { get; set; } = 70;
    public float Depth { get; set; } = 1;
    public float Rotation { get; set; }
    public float[] Color { get; set; } = [.42f, .72f, 1, 1];
    public bool Visible { get; set; } = true;
    public uint Territory { get; set; }
    [JsonIgnore] public bool IsWorld => Kind is "orb" or "cube";
}

public sealed class GraphNode
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "event";
    public string Object { get; set; } = "";
    public string? Event { get; set; }
    public string? Property { get; set; }
    public JsonNode? Value { get; set; }
    public float Duration { get; set; } = 1;
    public string? Code { get; set; }
    public float X { get; set; } = 30;
    public float Y { get; set; } = 30;
}

public sealed class Wire
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
}

public sealed class Scene
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "Untitled workshop";
    public List<SceneObject> Objects { get; set; } = [];
    public List<GraphNode> Nodes { get; set; } = [];
    public List<Wire> Links { get; set; } = [];
}

public sealed class Snapshot
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public Scene? Scene { get; set; }
    public List<SceneObject> Objects { get; set; } = [];
    public bool Playing { get; set; }
    public double Time { get; set; }
    public string Selected { get; set; } = "";
    public long Revision { get; set; }
    public bool Dirty { get; set; }
    public bool CanUndo { get; set; }
    public bool CanRedo { get; set; }
    public List<string> Logs { get; set; } = [];
}
