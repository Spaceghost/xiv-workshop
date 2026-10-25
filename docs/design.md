# XIV Workshop

Create interfaces and programmable objects while playing FFXIV. Select an object,
edit its properties, connect event/action nodes, and put short Lua scripts inside
the same graph. Neovim is not involved.

## Shared model

One versioned JSON scene stores objects, nodes, links, and script source. The
inspector and graph issue commands against this document; they never maintain
separate copies of behavior. Scripts are explicit graph nodes: arbitrary source
does not have to round-trip into blocks.

The first object palette contains screen panels, labels, buttons, world orbs,
and world cubes. World objects are projected overlays owned by this plugin,
with per-territory placement. They are not spawned game entities, and the first
renderer does not depth-test against the game scene. Only this plugin's objects
are editable.

## Runtime

Nelua provides the native ABI, a memory-limited Lua 5.4 VM, and compiled geometry.
Lua owns document validation, commands, history, graph traversal, animation, and
behavior execution. A small C# adapter uses Dalamud's ImGui and projection APIs,
passes input and elapsed time to the runtime, and saves scene files atomically.
There is no dependency on Ghostty's unfinished panel-core extraction.

Edit mode selects and moves interface objects. Play mode runs click, start, and
tick events. Graph actions set a property, toggle visibility, animate a number,
or run a script. Both paths use the same property API. Applying an edit resets
the runtime to the edited scene; play remains active. Stop restores authoring
values, so animation does not silently rewrite the saved document.

Undo and redo operate on authored changes. A live drag is grouped into one
history entry. Deleting an object also removes its nodes and their links.
Save writes authored values, including script source; load validates a complete
document before replacing the current scene.

## Script execution

Script nodes get `self`, `scene`, `event`, `time`, `dt`, `math`, and `log`.
Object handles expose `get`, `set`, and `animate`; `scene:get(id)` resolves another
object. There is no file/process/network API in user scripts. Each event runs
against staged object/animation state. An error or exhausted instruction budget
discards the event's changes and produces an inspectable log entry. A failing
tick event is disabled until the next edit or play restart.

The sandbox is an accidental-error boundary for locally authored scripts, not a
promise to execute hostile code safely. Lua allocation is bounded by the native
host. Scene size, graph size, source length, logs, history, and traversal are
bounded too.

## Verification

Native ABI tests must load the actual Nelua shared library and execute the actual
Lua runtime. They cover mixed graph/script behavior, rollback, instruction limits,
undo/redo, save/load, invalid documents, deletion, animation, and independent VM
lifetimes. The Windows core and managed plugin must build against the installed
Dalamud API. In-game placement, picking, input capture, and visual appearance
remain unverified until FFXIV is running.
