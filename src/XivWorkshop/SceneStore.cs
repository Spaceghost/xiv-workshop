using System.Text;
using System.Text.Json;

namespace XivWorkshop;

public static class SceneStore
{
    // Write beside the destination, so replacement stays on the same filesystem.
    public static void Save(Backend backend, string path)
    {
        var revision = backend.State.Revision;
        var text = JsonSerializer.Serialize(backend.Scene, new JsonSerializerOptions(Backend.Json) { WriteIndented = true });
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, full, overwrite: true);
            backend.Send(new { op = "saved", revision });
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static void Load(Backend backend, string path)
    {
        var info = new FileInfo(path);
        if (info.Length > 768 * 1024) throw new InvalidOperationException("Scene exceeds 768 KiB");
        // Validation and replacement are atomic in the Lua command handler.
        var scene = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(path));
        backend.Send(new { op = "load", scene });
    }
}
