# Third-party sources

`lua/json.lua` is unmodified json.lua 0.1.2 by rxi, MIT licensed. Its full license
notice is retained in that file. Source:
https://github.com/rxi/json.lua/blob/dbf4b2dd2eb7c23be2773c89eb059dadd6436f94/json.lua

The build uses Nelua and Lua 5.4.8, both MIT licensed; versions and Lua's source
archive checksum are in `toolchain.env`. The native Lua API declarations in this
project are deliberately limited to the functions used by the host.

Dalamud and its ImGui bindings are external installed references. They are not
copied into the plugin package. APIs used by the adapter:

- https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IGameGui/
- https://dalamud.dev/api/Dalamud.Plugin/Interfaces/IDalamudPluginInterface/
- https://nelua.io/faq/
- https://www.lua.org/manual/5.4/manual.html
