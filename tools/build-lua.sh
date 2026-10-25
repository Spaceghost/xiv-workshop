#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT/tools/env.sh"
MODE="${1:-host}"
DEST="${2:-$ROOT/.deps/lua-$MODE}"
mkdir -p "$DEST"
if [[ "$MODE" == host ]]; then
  COMPILER=("${CC:-cc}"); FLAGS=(-fPIC -DLUA_USE_LINUX); ARCHIVER=(ar)
elif [[ "$MODE" == windows ]]; then
  COMPILER=("$ZIG" cc -target x86_64-windows-gnu); FLAGS=(-DLUA_USE_WINDOWS); ARCHIVER=("$ZIG" ar)
else echo 'Use host or windows' >&2; exit 1; fi
for FILE in "$LUA_SRC"/*.c; do
  NAME="${FILE##*/}"
  [[ "$NAME" == lua.c || "$NAME" == luac.c ]] && continue
  "${COMPILER[@]}" -O2 "${FLAGS[@]}" -I"$LUA_SRC" -c "$FILE" -o "$DEST/${NAME%.c}.o"
done
"${ARCHIVER[@]}" rcs "$DEST/liblua.a" "$DEST"/*.o
