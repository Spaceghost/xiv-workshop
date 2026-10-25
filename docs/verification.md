# Verification — 2026-10-25

## Passed outside FFXIV

`tools/test.sh`: 14 Python tests call the actual compiled Nelua shared library,
with the actual Lua runtime bundled into each independent VM. Twelve additional
managed-host assertions exercise P/Invoke delegates, input, ticking, compiled
geometry, authored/runtime separation, Unicode persistence, invalid reload,
concurrent commands, disposed handles, and repeated native load/unload.

`tools/ui-test.sh`: 10 checks use the actual workshop UI and installed Dalamud
ImGui bindings with pinned native gc-cimgui sources. They exercise drawing,
selecting, dragging, one-step drag Undo, Play/Stop, clicking the rendered button,
connecting graph dots, typing/applying Lua, and the Save button. World projection
is simulated in this test. Actual ImGui triangles are software-rendered into
`build/ui-test/*.png` for inspection.

The UI tests exposed and verified fixes for new-window layering (a panel covered
its button) and the canvas background claiming input before the graph pins.

`tools/build.sh windows`: x64 native PE DLL and .NET 10 Windows plugin compile
against the locally installed Dalamud API 15 references, without warnings.

`tools/windows-test.sh`: the same 12 managed assertions also pass against the
actual Windows PE DLL, using XIVLauncher's installed Wine 10.8 and Windows .NET
10.0.0 runtime. This uses an isolated Wine prefix and Xvfb; it does not start
FFXIV or alter its game prefix. Native loading and the managed/native boundary
are therefore exercised on the target platform, including repeated load/unload.

The adapter serializes native calls, response copying, geometry, and disposal.
Saving captures the document and revision from the same snapshot, so concurrent
commands cannot acknowledge a different document as saved.

## Pending in a running game

Load the local developer plugin, run `/workshop`, place the example orb and cube
near the player, and test the button. Verify real camera projection and ground
raycasting, game input capture/release, territory changes, and unload/reload.

The game was closed during this work. No in-game verification is claimed.
