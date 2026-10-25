#!/usr/bin/env bash
# Optional headless UI test dependency; not shipped with the game plugin.
set -euo pipefail
ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT/toolchain.env"
DEST="$ROOT/.deps/gc-cimgui"
mkdir -p "$DEST" "$ROOT/build/ui-test"
if [[ ! -f "$DEST/cimgui.cpp" ]]; then
  curl -fL "https://codeload.github.com/goatcorp/gc-cimgui/tar.gz/$CIMGUI_COMMIT" -o "$ROOT/.deps/cimgui.tar.gz"
  tar -xzf "$ROOT/.deps/cimgui.tar.gz" --strip-components=1 -C "$DEST"
fi
if [[ ! -f "$DEST/imgui/imgui.cpp" ]]; then
  mkdir -p "$DEST/imgui"
  curl -fL "https://codeload.github.com/goatcorp/gc-imgui/tar.gz/$IMGUI_COMMIT" -o "$ROOT/.deps/imgui.tar.gz"
  tar -xzf "$ROOT/.deps/imgui.tar.gz" --strip-components=1 -C "$DEST/imgui"
fi
for FILE in "$DEST/cimgui.cpp" "$DEST"/imgui/imgui{,_draw,_widgets,_tables,_demo}.cpp "$ROOT/tests/ui_bridge.cpp"; do
  NAME="${FILE##*/}"
  OBJECT="$ROOT/build/ui-test/${NAME%.cpp}.o"
  if [[ "$OBJECT" -nt "$FILE" && "$OBJECT" -nt "${BASH_SOURCE[0]}" ]]; then continue; fi
  "${CXX:-g++}" -std=c++11 -O1 -fPIC -D__cdecl= -DIMGUI_DISABLE_OBSOLETE_FUNCTIONS=1 -DIMGUI_ENABLE_TEST_ENGINE=1 \
    -I"$DEST" -I"$DEST/imgui" -c "$FILE" -o "$OBJECT"
done
"${CXX:-g++}" -shared -o "$ROOT/build/ui-test/cimgui.so" "$ROOT"/build/ui-test/*.o
