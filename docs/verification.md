# Verification — 2026-10-25

## Passed outside FFXIV

`tools/test.sh`: 14 Python tests call the actual compiled Nelua shared library,
with the actual Lua runtime bundled into each independent VM. Nine additional
managed-host assertions exercise P/Invoke delegates, input, ticking, compiled
geometry, authored/runtime separation, Unicode persistence, and invalid reload.

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

## Pending in a running game

Load the local developer plugin, run `/workshop`, place the example orb and cube
near the player, and test the button. Verify real camera projection and ground
raycasting, game input capture/release, territory changes, and unload/reload.

The game was closed during this work. No in-game verification is claimed.
