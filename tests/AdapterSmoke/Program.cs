using System.Diagnostics;
using System.Text.Json;
using XivWorkshop;

var root = Path.GetFullPath(args[0]);
var library = Path.Combine(root, OperatingSystem.IsWindows() ? "build/plugin/xiv_workshop.dll" : "build/native/libxiv_workshop.so");
using var runtime = new Backend(library,
    Path.Combine(root, "build/plugin/lua/workshop.lua"));
void Check(bool passed, string message)
{
    if (!passed) throw new Exception(message);
    Console.WriteLine("PASS " + message);
}

SceneStore.Load(runtime, Path.Combine(root, "examples/starter.json"));
Check(runtime.Scene.Objects.Count == 5 && runtime.Scene.Nodes.Count == 5, "managed host loads example through the native VM");
runtime.Send(new { op = "play", enabled = true });
runtime.Send(new { op = "click", id = "button1" });
Check(runtime.State.Objects.Single(o => o.Id == "orb1").Color[0] == 1, "managed input reaches visual nodes");
runtime.Send(new { op = "tick", dt = .1 });
Check(runtime.State.Objects.Single(o => o.Id == "orb1").Y > 1, "managed tick reaches the Lua script animation");
var timer = Stopwatch.StartNew();
for (var i = 0; i < 600; i++) runtime.Send(new { op = "tick", dt = .016 });
timer.Stop();
Console.WriteLine($"600 actual ABI ticks: {timer.ElapsedMilliseconds}ms total");
Check(runtime.Scene.Objects.Single(o => o.Id == "orb1").Y == 1, "runtime animation leaves authored scene intact");
var temp = Path.Combine(Path.GetTempPath(), "xiv-workshop-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    var path = Path.Combine(temp, "雪-scene.json");
    runtime.Edit("orb1", new() { ["label"] = "Orb 雪" });
    Check(runtime.State.Dirty, "authored changes mark the scene dirty");
    SceneStore.Save(runtime, path);
    Check(!runtime.State.Dirty && File.Exists(path), "atomic save acknowledges the written revision with a Unicode path");
    runtime.Edit("orb1", new() { ["label"] = "modified" });
    SceneStore.Load(runtime, path);
    Check(runtime.Scene.Objects.Single(o => o.Id == "orb1").Label == "Orb 雪", "managed persistence round trip preserves Unicode");
    File.WriteAllText(path, "{\"version\":9}");
    try { SceneStore.Load(runtime, path); throw new Exception("Invalid scene accepted"); }
    catch (InvalidOperationException) { }
    Check(runtime.Scene.Objects.Count == 5, "invalid reload preserves the existing scene");
    var cube = runtime.Scene.Objects.Single(o => o.Id == "cube1");
    var vertex = runtime.BoxVertex(cube, 7);
    Check(Math.Abs(vertex.X - (cube.X + .5f)) < .001f, "managed renderer calls compiled Nelua geometry");
}
finally { Directory.Delete(temp, recursive: true); }

using (var concurrent = new Backend(library, Path.Combine(root, "build/plugin/lua/workshop.lua")))
{
    Parallel.For(0, 32, _ => concurrent.Send(new { op = "add", kind = "label" }));
    Check(concurrent.Scene.Objects.Count == 32, "concurrent host calls preserve every authored edit");
    concurrent.Dispose();
    try { concurrent.Send(new { op = "snapshot" }); throw new Exception("Disposed VM accepted a call"); }
    catch (ObjectDisposedException) { }
    Check(true, "disposed native handles reject further calls");
}
for (var i = 0; i < 8; i++)
{
    using var reload = new Backend(library, Path.Combine(root, "build/plugin/lua/workshop.lua"));
    if (reload.Scene.Objects.Count != 0) throw new Exception("Reload retained an old scene");
}
Check(true, "repeated native load and unload creates clean independent VMs");
