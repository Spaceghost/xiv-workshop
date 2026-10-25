using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using XivWorkshop;

var root = Path.GetFullPath(args[0]);
// Dalamud's binding explicitly loads cimgui.dll beside its managed assembly.
var imguiPath = Path.Combine(AppContext.BaseDirectory, "cimgui.dll");
File.Copy(Path.Combine(root, "build/ui-test/cimgui.so"), imguiPath, overwrite: true);
var cimgui = NativeLibrary.Load(imguiPath);
NativeLibrary.SetDllImportResolver(typeof(ImGui).Assembly, (name, _, _) => name == "cimgui" ? cimgui : 0);
NativeLibrary.SetDllImportResolver(typeof(Bridge).Assembly, (name, _, _) => name == "cimgui" ? cimgui : 0);
var context = ImGui.CreateContext();
var io = ImGui.GetIO();
io.DisplaySize = new Vector2(1600, 1000);
io.DeltaTime = 1f / 60;
unsafe
{
    io.IniFilename = null;
    io.Fonts.AddFontDefault();
    byte* pixels = null;
    int width = 0, height = 0;
    io.Fonts.GetTexDataAsRGBA32(0, &pixels, &width, &height);
}
using var backend = new Backend(Path.Combine(root, "build/native/libxiv_workshop.so"),
    Path.Combine(root, "build/plugin/lua/workshop.lua"));
var example = Path.Combine(root, "examples/starter.json");
var temp = Path.Combine(root, "build/ui-test/scene.json");
var ui = new WorkshopUi(backend, new PreviewWorld(), temp, example);
ui.LoadExample();

void Frame()
{
    Bridge.ws_ui_begin();
    ImGui.NewFrame();
    backend.Send(new { op = "tick", dt = 1.0 / 60 });
    ui.DrawObjects();
    ui.Draw();
    ImGui.Render();
    if (ui.Error is not null) throw new Exception(ui.Error);
}
void Check(bool passed, string message)
{
    if (!passed) throw new Exception(message);
    Console.WriteLine("PASS " + message);
}
Vector2 Find(string label, string window = "", int occurrence = 0)
{
    if (!Bridge.ws_ui_item(label, window, occurrence, out var x, out var y))
    {
        Bridge.ws_ui_items();
        throw new Exception("UI item not found: " + label + " / " + window);
    }
    return new(x, y);
}
void ClickAt(Vector2 p)
{
    io.AddMousePosEvent(p.X, p.Y); Frame();
    io.AddMouseButtonEvent(0, true); Frame();
    io.AddMouseButtonEvent(0, false); Frame(); Frame();
}
void Click(string label, string window = "", int occurrence = 0) => ClickAt(Find(label, window, occurrence));
void Capture(string name) => Bridge.ws_ui_capture(Path.Combine(root, "build/ui-test/" + name + ".ppm"));

try
{
    Frame(); Frame(); Frame();
    Check(ImGui.GetDrawData().TotalVtxCount > 0, "real ImGui draws the editor, interfaces, and projected shapes");
    Capture("objects");
    Click("object-hit", "workshop-object-button1");
    Check(backend.State.Selected == "button1", "clicking a rendered object selects its shared scene ID");
    var before = backend.Scene.Objects.Single(o => o.Id == "button1").X;
    var body = Find("object-hit", "workshop-object-button1");
    io.AddMousePosEvent(body.X, body.Y); Frame();
    io.AddMouseButtonEvent(0, true); Frame();
    io.AddMousePosEvent(body.X + 24, body.Y + 12); Frame();
    io.AddMousePosEvent(body.X + 40, body.Y + 16); Frame();
    io.AddMouseButtonEvent(0, false); Frame();
    Check(backend.Scene.Objects.Single(o => o.Id == "button1").X > before, "dragging the actual interface edits its authored position");
    Click("Undo");
    Check(backend.Scene.Objects.Single(o => o.Id == "button1").X == before, "one Undo restores the entire interface drag");
    Click("Play");
    Check(backend.State.Playing, "Play toolbar starts the runtime");
    Click("object-hit", "workshop-object-button1");
    Check(backend.State.Objects.Single(o => o.Id == "orb1").Color[0] == 1, "a real button click runs visual wiring and Lua");
    Click("Stop");
    Check(!backend.State.Playing && backend.State.Objects.Single(o => o.Id == "orb1").Color[0] != 1, "Stop restores authored properties");
    Click("Behaviors"); Frame();
    Capture("graph");
    backend.Send(new { op = "unlink", from = "click", to = "color" }); Frame();
    Click("output", "graph-canvas", 0);
    Click("input", "graph-canvas", 0);
    Check(backend.Scene.Links.Any(l => l.From == "click" && l.To == "color"), "clicking output and input dots creates a real graph wire");
    Click("node-body", "graph-canvas", 2);
    Capture("script");
    Click("##script-source");
    io.AddKeyEvent(ImGuiKey.ModCtrl, true); io.AddKeyEvent(ImGuiKey.A, true); Frame();
    io.AddKeyEvent(ImGuiKey.A, false); io.AddKeyEvent(ImGuiKey.ModCtrl, false); Frame();
    foreach (var ch in "log(\"typed in the editor\")") io.AddInputCharacter(ch);
    Frame();
    Click("Apply changes");
    Check(backend.Scene.Nodes.Single(n => n.Id == "lift").Code == "log(\"typed in the editor\")", "typing and applying Lua updates the saved graph node");
    Click("Save");
    Check(File.Exists(temp) && !backend.State.Dirty, "Save toolbar atomically writes the shared scene");
    Capture("script-edited");
    Click("Console");
    Capture("console");
}
finally { ImGui.DestroyContext(context); }

internal sealed class PreviewWorld : IWorkshopWorld
{
    public uint Territory => 0;
    public Vector3? PlayerPosition => Vector3.Zero;
    public bool WorldToScreen(Vector3 p, out Vector2 screen)
    { screen = new(180 + p.X * 55, 580 - p.Y * 55 + p.Z * 18); return true; }
    public bool ScreenToWorld(Vector2 screen, out Vector3 p)
    { p = new((screen.X - 180) / 55, 0, (screen.Y - 580) / 18); return true; }
}

internal static class Bridge
{
    [DllImport("cimgui")] internal static extern void ws_ui_begin();
    [DllImport("cimgui")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool ws_ui_item([MarshalAs(UnmanagedType.LPUTF8Str)] string label,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string window, int occurrence, out float x, out float y);
    [DllImport("cimgui")] internal static extern void ws_ui_items();
    [DllImport("cimgui")] internal static extern void ws_ui_capture([MarshalAs(UnmanagedType.LPUTF8Str)] string path);
}
