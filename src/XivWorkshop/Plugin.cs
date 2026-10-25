using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace XivWorkshop;

public sealed class Plugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface pi;
    private readonly ICommandManager commands;
    private readonly IPluginLog log;
    private readonly Backend backend;
    private readonly WorkshopUi ui;
    private bool registered;
    private string? lastFrameFailure;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager,
        IPluginLog pluginLog, IGameGui gameGui, IClientState client, IObjectTable objects)
    {
        pi = pluginInterface; commands = commandManager; log = pluginLog;
        var root = pi.AssemblyLocation.DirectoryName!;
        backend = new Backend(Path.Combine(root, "xiv_workshop.dll"), Path.Combine(root, "lua", "workshop.lua"));
        try
        {
            var path = Path.Combine(pi.ConfigDirectory.FullName, "scene.json");
            ui = new WorkshopUi(backend, new DalamudWorld(gameGui, client, objects), path,
                Path.Combine(root, "examples", "starter.json"));
            // A damaged saved scene is reported in the editor; it is never overwritten on load.
            if (File.Exists(path)) ui.Try(() => SceneStore.Load(backend, path));
            else ui.LoadExample();
            registered = commands.AddHandler("/workshop", new CommandInfo(OnCommand)
            {
                HelpMessage = "Open the live workshop. /workshop play, stop, save, or load.",
            });
            if (!registered) throw new InvalidOperationException("/workshop is already registered");
            pi.UiBuilder.Draw += Draw;
            pi.UiBuilder.OpenMainUi += Open;
            pi.UiBuilder.OpenConfigUi += Open;
        }
        catch { backend.Dispose(); if (registered) commands.RemoveHandler("/workshop"); throw; }
    }

    private void OnCommand(string command, string argument)
    {
        switch (argument.Trim().ToLowerInvariant())
        {
            case "play": ui.Try(() => backend.Send(new { op = "play", enabled = true })); break;
            case "stop": ui.Try(() => backend.Send(new { op = "play", enabled = false })); break;
            case "save": ui.Save(); break;
            case "load": ui.Reload(); break;
            default: ui.Open = true; break;
        }
    }

    private void Open() => ui.Open = true;

    private void Draw()
    {
        try { backend.Send(new { op = "tick", dt = Math.Clamp(ImGui.GetIO().DeltaTime, 0, .1f) }); }
        catch (Exception ex)
        {
            ui.Error = ex.Message;
            if (lastFrameFailure != ex.Message) log.Error(ex, "XIV Workshop runtime update failed");
            lastFrameFailure = ex.Message;
        }
        try
        {
            ui.DrawObjects();
        }
        catch (Exception ex)
        {
            ui.Error = ex.Message;
            if (lastFrameFailure != ex.Message) log.Error(ex, "XIV Workshop object drawing failed");
            lastFrameFailure = ex.Message;
        }
        // A renderer/runtime error must not hide Stop, Undo, or the scene editor.
        try { ui.Draw(); }
        catch (Exception ex)
        {
            ui.Error = ex.Message;
            if (lastFrameFailure != ex.Message) log.Error(ex, "XIV Workshop editor drawing failed");
            lastFrameFailure = ex.Message;
        }
    }

    public void Dispose()
    {
        pi.UiBuilder.Draw -= Draw;
        pi.UiBuilder.OpenMainUi -= Open;
        pi.UiBuilder.OpenConfigUi -= Open;
        if (registered) { commands.RemoveHandler("/workshop"); registered = false; }
        backend.Dispose();
    }
}
