#!/usr/bin/env bash
# Optional standalone setup; existing compatible local toolchains can be reused.
set -euo pipefail
ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT/toolchain.env"
mkdir -p "$ROOT/.deps"
if [[ ! -d "$ROOT/.deps/nelua-lang/.git" ]]; then
  git clone --no-checkout "$NELUA_REPOSITORY" "$ROOT/.deps/nelua-lang"
fi
git -C "$ROOT/.deps/nelua-lang" checkout --detach "$NELUA_COMMIT"
make -C "$ROOT/.deps/nelua-lang" -j2
if [[ ! -f "$ROOT/.deps/lua-$LUA_VERSION/src/lua.h" ]]; then
  curl -fL "https://www.lua.org/ftp/lua-$LUA_VERSION.tar.gz" -o "$ROOT/.deps/lua.tar.gz"
  printf '%s  %s\n' "$LUA_SHA256" "$ROOT/.deps/lua.tar.gz" | sha256sum -c -
  tar -xzf "$ROOT/.deps/lua.tar.gz" -C "$ROOT/.deps"
fi
"$ROOT/tools/build-lua.sh" host "$ROOT/.deps/lua-host"
if [[ "${1:-host}" == windows ]]; then
  "$ROOT/tools/build-lua.sh" windows "$ROOT/.deps/lua-win"
fi
