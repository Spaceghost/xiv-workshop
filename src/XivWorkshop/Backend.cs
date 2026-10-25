using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XivWorkshop;

/// <summary>Owns a native Nelua module and one Lua VM. Use on the UI thread only.</summary>
public sealed class Backend : IDisposable
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint Create([MarshalAs(UnmanagedType.LPUTF8Str)] string source);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint Call(nint handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string request);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint Error(nint handle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void Destroy(nint handle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void Vertex(int index, float x, float y, float z, float width, float height,
        float depth, float angle, out float ox, out float oy, out float oz);

    private nint library;
    private nint vm;
    private readonly Call call = null!;
    private readonly Error error = null!;
    private readonly Destroy destroy = null!;
    private readonly Vertex vertex = null!;
    public Snapshot State { get; private set; } = new();
    public Scene Scene => State.Scene ?? throw new InvalidOperationException("No workshop scene");

    public Backend(string libraryPath, string bundlePath)
    {
        try
        {
            library = NativeLibrary.Load(Path.GetFullPath(libraryPath));
            var create = Resolve<Create>("ws_create");
            call = Resolve<Call>("ws_call");
            error = Resolve<Error>("ws_error");
            destroy = Resolve<Destroy>("ws_destroy");
            vertex = Resolve<Vertex>("ws_box_vertex");
            vm = create(File.ReadAllText(bundlePath));
            var message = Marshal.PtrToStringUTF8(error(vm));
            if (vm == 0 || !string.IsNullOrEmpty(message))
                throw new InvalidOperationException(message ?? "Could not create Lua VM");
            Send(new { op = "snapshot" });
        }
        catch { Dispose(); throw; }
    }

    private T Resolve<T>(string name) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));

    public Snapshot Send(object request)
    {
        ObjectDisposedException.ThrowIf(vm == 0, this);
        var pointer = call(vm, JsonSerializer.Serialize(request, Json));
        if (pointer == 0)
            throw new InvalidOperationException(Marshal.PtrToStringUTF8(error(vm)) ?? "Native call failed");
        var next = JsonSerializer.Deserialize<Snapshot>(Marshal.PtrToStringUTF8(pointer)!, Json)
            ?? throw new InvalidOperationException("Empty response from Lua runtime");
        if (!next.Ok) throw new InvalidOperationException(next.Error ?? "Workshop command failed");
        next.Scene ??= State.Scene;
        State = next;
        return next;
    }

    public void Edit(string id, Dictionary<string, object> patch, string? group = null) =>
        Send(new { op = "edit", id, patch, group });

    public Vector3 BoxVertex(SceneObject o, int index)
    {
        vertex(index, o.X, o.Y, o.Z, o.Width, o.Height, o.Depth, o.Rotation,
            out var x, out var y, out var z);
        return new(x, y, z);
    }

    public void Dispose()
    {
        if (vm != 0) { destroy(vm); vm = 0; }
        if (library != 0) { NativeLibrary.Free(library); library = 0; }
    }
}
