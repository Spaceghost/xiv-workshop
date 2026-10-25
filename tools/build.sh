#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
source tools/env.sh
MODE="${1:-all}"
[[ "$MODE" == host || "$MODE" == windows || "$MODE" == all ]] || { echo 'Use: tools/build.sh [host|windows|all]' >&2; exit 1; }
[[ -x "$NELUA" && -f "$LUA_SRC/lua.h" ]] || { echo 'Run tools/bootstrap.sh first (or set NELUA and LUA_SRC).' >&2; exit 1; }
mkdir -p build/native build/plugin
python3 tools/bundle.py
mkdir -p build/plugin/examples
cp examples/*.json build/plugin/examples/
if [[ "$MODE" == host || "$MODE" == all ]]; then
  [[ -f "$LUA_HOST_LIB/liblua.a" ]] || tools/build-lua.sh host "$LUA_HOST_LIB"
  "$NELUA" --cc "${CC:-cc}" -P nogc -P noentrypoint -L . --cache-dir build/nelua-host \
    --cflags="-O2 -fPIC -I\"$LUA_SRC\" -L\"$LUA_HOST_LIB\"" \
    -H -o build/native/libxiv_workshop.so native/workshop.nelua
fi
if [[ "$MODE" == windows || "$MODE" == all ]]; then
  [[ -x "$ZIG" && -f "$LUA_WIN_LIB/liblua.a" ]] || { echo 'Missing Windows toolchain; run tools/bootstrap.sh windows and set ZIG if needed.' >&2; exit 1; }
  "$NELUA" --cc "$ROOT/tools/cc-win.sh" -P nogc -P noentrypoint -L . --cache-dir build/nelua-windows \
    --cflags="-O2 -I\"$LUA_SRC\" -L\"$LUA_WIN_LIB\"" \
    -H -o build/plugin/xiv_workshop.dll native/workshop.nelua
  [[ -f "$DALAMUD_HOME/Dalamud.dll" ]] || { echo 'Set DALAMUD_HOME to the installed Dalamud reference directory.' >&2; exit 1; }
  "$DOTNET" build src/XivWorkshop/XivWorkshop.csproj -c Release --nologo -p:NuGetAudit=false
fi
echo "Workshop build complete ($MODE)."
