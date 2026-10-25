using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace XivWorkshop;

internal sealed partial class WorkshopUi(Backend backend, IWorkshopWorld world, string scenePath, string examplePath)
{
    public bool Open = true;
    public string? Error;
    private string? notice;
    private string? placing;
    private long groupSerial;
    private string fieldKey = "";
    private string fieldGroup = "";
    private readonly HashSet<string> knownObjectWindows = [];
    private bool focusEditor;
    private const ImGuiWindowFlags ObjectFlags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings |
        ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
        ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBringToFrontOnFocus;

    public void Try(Action action)
    {
        try { action(); Error = null; }
        catch (Exception ex) { Error = ex.Message; }
    }

    public void Save() => Try(() => { SceneStore.Save(backend, scenePath); notice = "Scene saved."; });
    public void Reload() => Try(() => { SceneStore.Load(backend, scenePath); notice = "Scene loaded. Undo restores the previous scene."; });
    public void LoadExample() => Try(() => { SceneStore.Load(backend, examplePath); notice = "Example loaded. Place the orb near you, then press Play and click the button."; });

    private void Send(object command) => Try(() => backend.Send(command));

    private string Group(string key, bool changed)
    {
        if (ImGui.IsItemActivated() || (changed && key != fieldKey))
        {
            fieldKey = key; fieldGroup = key + ":" + ++groupSerial;
        }
        return fieldGroup;
    }

    public void Draw()
    {
        if (!Open) return;
        var viewport = ImGui.GetMainViewport();
        var size = new Vector2(Math.Min(1040, Math.Max(480, viewport.WorkSize.X - 40)),
            Math.Min(740, Math.Max(380, viewport.WorkSize.Y - 40)));
        var offset = new Vector2(Math.Min(380, Math.Max(20, viewport.WorkSize.X - size.X - 20)),
            Math.Min(110, Math.Max(20, viewport.WorkSize.Y - size.Y - 20)));
        ImGui.SetNextWindowSize(size, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(viewport.WorkPos + offset, ImGuiCond.FirstUseEver);
        if (focusEditor) { ImGui.SetNextWindowFocus(); focusEditor = false; }
        if (ImGui.Begin("XIV Workshop###XivWorkshop", ref Open))
        {
            try { DrawEditor(); }
            finally { ImGui.End(); }
        }
        else ImGui.End();
    }

    private ImGuiWindowFlags FlagsForObject(string window)
    {
        // ImGui inserts a *new* NoBringToFront window at the back. Create in
        // document order first, then preserve that order during normal input.
        if (!knownObjectWindows.Add(window)) return ObjectFlags;
        focusEditor = true;
        return ObjectFlags & ~ImGuiWindowFlags.NoBringToFrontOnFocus;
    }

    private void DrawEditor()
    {
        var state = backend.State;
        if (ImGui.Button(state.Playing ? "Stop" : "Play"))
            Send(new { op = "play", enabled = !state.Playing });
        ImGui.SameLine();
        ImGui.BeginDisabled(!state.CanUndo);
        if (ImGui.Button("Undo")) Send(new { op = "undo" });
        ImGui.EndDisabled(); ImGui.SameLine();
        ImGui.BeginDisabled(!state.CanRedo);
        if (ImGui.Button("Redo")) Send(new { op = "redo" });
        ImGui.EndDisabled(); ImGui.SameLine();
        if (ImGui.Button("Save")) Save();
        ImGui.SameLine(); if (ImGui.Button("Reload")) Reload();
        ImGui.SameLine(); if (ImGui.Button("Load example")) LoadExample();
        ImGui.SameLine();
        ImGui.TextUnformatted(backend.State.Playing ? $"Playing {backend.State.Time:0.0}s" : "Editing");
        ImGui.TextUnformatted(backend.Scene.Name + (backend.State.Dirty ? " *" : ""));
        if (Error is not null) ImGui.TextWrapped("Error: " + Error);
        else if (notice is not null) ImGui.TextWrapped(notice);
        if (ImGui.BeginTabBar("workshop-tabs"))
        {
            try
            {
                if (ImGui.BeginTabItem("Objects"))
                { try { DrawInspector(); } finally { ImGui.EndTabItem(); } }
                if (ImGui.BeginTabItem("Behaviors"))
                { try { DrawGraph(); } finally { ImGui.EndTabItem(); } }
                if (ImGui.BeginTabItem("Console"))
                { try { DrawConsole(); } finally { ImGui.EndTabItem(); } }
            }
            finally { ImGui.EndTabBar(); }
        }
    }

    private void DrawInspector()
    {
        foreach (var kind in new[] { "panel", "button", "label", "orb", "cube" })
        {
            if (kind != "panel") ImGui.SameLine();
            if (ImGui.Button("+ " + kind))
            {
                var patch = new Dictionary<string, object>();
                if (kind is "orb" or "cube" && world.PlayerPosition is { } position)
                {
                    patch["x"] = position.X + 1; patch["y"] = position.Y + 1;
                    patch["z"] = position.Z; patch["territory"] = world.Territory;
                }
                Send(new { op = "add", kind, patch });
            }
        }
        ImGui.TextWrapped("Edit: click and drag your interface objects. Play: clicks run their behaviors. Stop restores authored values.");
        var available = ImGui.GetContentRegionAvail();
        ImGui.BeginChild("object-list", new Vector2(205, Math.Max(120, available.Y)), true);
        try
        {
            foreach (var o in backend.Scene.Objects)
                if (ImGui.Selectable($"{(string.IsNullOrWhiteSpace(o.Label) ? o.Kind : o.Label)}##{o.Id}", backend.State.Selected == o.Id))
                    Send(new { op = "select", id = o.Id });
        }
        finally { ImGui.EndChild(); }
        ImGui.SameLine();
        ImGui.BeginChild("inspector", new Vector2(0, Math.Max(120, available.Y)), true);
        try
        {
            var o = backend.Scene.Objects.Find(o => o.Id == backend.State.Selected);
            if (o is null) { ImGui.TextWrapped("Select an object, or create one with the buttons above."); return; }
            ImGui.TextUnformatted(o.Kind + " · " + o.Id);
            var label = o.Label;
            var changed = ImGui.InputText("Label", ref label, 256);
            var group = Group(o.Id + ":label", changed);
            if (changed) Try(() => backend.Edit(o.Id, new() { ["label"] = label }, group));
            var color = new Vector4(o.Color[0], o.Color[1], o.Color[2], o.Color[3]);
            changed = ImGui.ColorEdit4("Color", ref color); group = Group(o.Id + ":color", changed);
            if (changed) Try(() => backend.Edit(o.Id, new() { ["color"] = new[] { color.X, color.Y, color.Z, color.W } }, group));
            var visible = o.Visible;
            if (ImGui.Checkbox("Visible", ref visible)) Try(() => backend.Edit(o.Id, new() { ["visible"] = visible }));
            FloatField(o, "X", "x", o.X, o.IsWorld ? .02f : 1);
            FloatField(o, "Y", "y", o.Y, o.IsWorld ? .02f : 1);
            if (o.IsWorld) FloatField(o, "Z", "z", o.Z, .02f);
            FloatField(o, "Width", "width", o.Width, o.IsWorld ? .02f : 1, .01f, 10000);
            FloatField(o, "Height", "height", o.Height, o.IsWorld ? .02f : 1, .01f, 10000);
            if (o.IsWorld)
            {
                FloatField(o, "Depth", "depth", o.Depth, .02f, .01f, 10000);
                FloatField(o, "Rotation (radians)", "rotation", o.Rotation, .01f);
                ImGui.TextUnformatted("Territory: " + o.Territory);
                if (ImGui.Button("Place near me")) PlaceNearPlayer(o.Id);
                ImGui.SameLine();
                if (ImGui.Button("Place on ground")) placing = o.Id;
                if (placing == o.Id) ImGui.TextWrapped("Click the ground outside the editor. Escape cancels.");
            }
            if (ImGui.Button("Duplicate")) Send(new { op = "duplicate", id = o.Id });
            ImGui.SameLine();
            if (ImGui.Button("Delete object")) Send(new { op = "delete", id = o.Id });
        }
        finally { ImGui.EndChild(); }
    }

    private void FloatField(SceneObject o, string label, string property, float value, float speed,
        float min = -100000, float max = 100000)
    {
        var changed = ImGui.DragFloat(label, ref value, speed, min, max);
        var group = Group(o.Id + ":" + property, changed);
        if (changed) Try(() => backend.Edit(o.Id, new() { [property] = value }, group));
    }

    private void PlaceNearPlayer(string id) => Try(() =>
    {
        var p = world.PlayerPosition ?? throw new InvalidOperationException("Log in to place a world object.");
        backend.Edit(id, new() { ["x"] = p.X + 1, ["y"] = p.Y + 1, ["z"] = p.Z, ["territory"] = world.Territory });
    });

    private void DrawConsole()
    {
        if (ImGui.Button("Clear")) Send(new { op = "clear_logs" });
        ImGui.TextWrapped("Script errors discard that event's changes. Edit or restart Play to retry a disabled event.");
        foreach (var line in backend.State.Logs) ImGui.TextWrapped(line);
        ImGui.Separator();
        ImGui.TextWrapped("Scripts: self:get(property), self:set(property, value), self:animate(property, target, seconds), scene:get(id), log(message). Available values: time, dt, event, math.");
        ImGui.TextUnformatted("Example: scene:get(\"orb1\"):set(\"color\", {1, 0.4, 0.7, 1})");
        ImGui.TextWrapped("Scene file: " + scenePath);
    }
}
