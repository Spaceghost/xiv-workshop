#!/usr/bin/env bash
# Source from build.sh. Explicit overrides also support standalone checkouts.
source "$ROOT/toolchain.env"
LOCAL_FFXIV_TOOLS="${LOCAL_FFXIV_TOOLS:-$ROOT/../ghostty-dalamud}"
NELUA="${NELUA:-$ROOT/.deps/nelua-lang/nelua}"
LUA_SRC="${LUA_SRC:-$ROOT/.deps/lua-$LUA_VERSION/src}"
LUA_HOST_LIB="${LUA_HOST_LIB:-$ROOT/.deps/lua-host}"
LUA_WIN_LIB="${LUA_WIN_LIB:-$ROOT/.deps/lua-win}"
if [[ ! -x "$NELUA" && -x "$LOCAL_FFXIV_TOOLS/vendor/nelua-lang/nelua" ]]; then
  NELUA="$LOCAL_FFXIV_TOOLS/vendor/nelua-lang/nelua"
fi
if [[ ! -f "$LUA_SRC/lua.h" && -f "$LOCAL_FFXIV_TOOLS/vendor/lua/src/lua.h" ]]; then
  LUA_SRC="$LOCAL_FFXIV_TOOLS/vendor/lua/src"
fi
if [[ ! -f "$LUA_WIN_LIB/liblua.a" && -f "$LOCAL_FFXIV_TOOLS/build/lua-win/liblua.a" ]]; then
  LUA_WIN_LIB="$LOCAL_FFXIV_TOOLS/build/lua-win"
fi
ZIG="${ZIG:-$HOME/.local/opt/zig-x86_64-linux-$ZIG_VERSION/zig}"
DOTNET="${DOTNET:-$HOME/.dotnet/dotnet}"
DALAMUD_HOME="${DALAMUD_HOME:-$HOME/.xlcore/dalamud/Hooks/dev}"
export ZIG DALAMUD_HOME
