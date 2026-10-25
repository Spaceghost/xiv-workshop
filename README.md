# XIV Workshop

Open **`/workshop`** in FFXIV. Create an interface or a world object, select it,
edit its properties, and attach visual wiring or a Lua script.

Nelua runs the native host and geometry; Lua 5.4 runs the shared scene model and
editable behavior. Dalamud supplies the ImGui editor, input, and game projection.

## Try the starter scene

1. Select **Orb** in Objects, then click **Place near me**.
2. Select **Cube** and click **Place near me**; drag its X value to separate it.
3. Press **Play**, then click **Make the orb glow** on the floating interface.
4. Open **Behaviors** to inspect the color action and Lua lift script.
5. Press **Stop** to restore authored values. **Save** preserves your scene.

World orbs and cubes are local, projected overlays. They are not game entities,
and their first renderer does not hide them behind terrain. They appear only in
the territory where they were placed. The plugin does not edit arbitrary game UI.

## Edit and wire

- In Edit mode, click an interface object to select it; drag it to move it.
  Use the inspector for dimensions, RGBA color, visibility, and world position.
- In Behaviors, add an event, action, animation, or script node. Click an output
  dot, then an input dot to connect them. Drag a node body to change its layout.
- Select a node, edit its fields, and click **Apply changes**. Invalid Lua source
  leaves the previous script intact. **Remove wire** disconnects a selected node.
- Undo/Redo covers authored edits, nodes, wires, duplication, deletion, and loads.
  A continuous drag is one undo entry. Scripts and animation do not rewrite the
  authored scene. Applying an object/behavior edit restarts its live values;
  moving a graph node preserves the running simulation.
- **Console** shows script output and errors. A failed event discards its changes
  and stays disabled until an edit or a Play restart.

Scripts are explicit graph nodes, so visual edits never regenerate or overwrite
arbitrary Lua source. For example:

```lua
scene:get("orb1"):set("color", {1, 0.4, 0.7, 1})
self:animate("y", self:get("y") + 0.7, 0.8)
log("My object moved")
```

`self:get`, `self:set`, and `self:animate` use the same validated properties as
visual actions. Scripts also receive `event`, `time`, `dt`, `math`, and `log`.
There is no file/process/network API. Instructions and VM allocation are bounded
to contain accidental infinite loops and excessive allocation.

## Build and install on Linux/Wine

Requires a C/C++ compiler, Python 3, .NET 10, Nelua, Lua 5.4.8 source/static libs,
Zig 0.16 for Windows cross-compilation, and installed Dalamud API 15 references.
`tools/env.sh` accepts explicit tool paths and can reuse the adjacent Ghostty
checkout's pinned compiler and Lua source without changing that checkout.

```sh
tools/build.sh all
tools/test.sh
tools/ui-test.sh
python3 tools/install.py --register
```

For a standalone checkout, run `tools/bootstrap.sh windows` first and set `ZIG`
to an installed Zig executable. This fetches the compiler and checksum-verified
Lua source using `toolchain.env`; it does not install .NET, Zig, or Dalamud.
Set `DALAMUD_HOME` if references are outside `~/.xlcore/dalamud/Hooks/dev`.

The installer stages into `~/dalamud-plugins/XivWorkshop` and can register that
path in `~/.xlcore/dalamudConfig.json` while the game is closed. It backs up an
existing installation and the previous config. It does not publish anything.

The default scene lives at `scene.json` inside Dalamud's plugin config directory.
Save replaces it atomically. Reload validates the whole file before replacing
the current document; Undo can restore the pre-load scene.

## Verification status

The actual native ABI and managed adapter are tested outside FFXIV. The headless
UI test uses the installed ImGui binding, real mouse/key events, the actual
workshop editor, and a simulated world projector. Its software-rendered captures
are in `build/ui-test/*.png`. These are editor captures, not in-game screenshots.

**In-game loading, input capture, projection, and ground placement still require
a running FFXIV session.** See [design](docs/design.md) and
[third-party sources](docs/third-party.md).
