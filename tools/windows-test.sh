#!/usr/bin/env bash
# Run the managed adapter against the actual Windows DLL in an isolated Wine prefix.
set -euo pipefail
ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT/tools/env.sh"
cd "$ROOT"
if [[ -z "${WINE:-}" ]]; then
  WINE="$(find -L "$HOME/.xlcore/compatibilitytool/wine" -maxdepth 3 -type f -name wine64 2>/dev/null | sort | tail -1)"
fi
WINDOWS_DOTNET="${WINDOWS_DOTNET:-$HOME/.xlcore/runtime/dotnet.exe}"
[[ -x "$WINE" && -f "$WINDOWS_DOTNET" ]] || { echo 'Set WINE and WINDOWS_DOTNET to the installed Windows test runtime.' >&2; exit 1; }
[[ -f build/plugin/xiv_workshop.dll ]] || { echo 'Build first: tools/build.sh all' >&2; exit 1; }
"$DOTNET" build tests/AdapterSmoke/AdapterSmoke.csproj -c Release --nologo -p:NuGetAudit=false
PREFIX="$ROOT/build/wine-test-prefix"
mkdir -p "$PREFIX"
export WINEPREFIX="$PREFIX" WINEDEBUG="${WINEDEBUG:--all}" WINEFSYNC=0 WINEESYNC=0
export WINEDLLOVERRIDES="${WINEDLLOVERRIDES:-mshtml=}"
WINESERVER="$(dirname -- "$WINE")/wineserver"
trap '"$WINESERVER" -k >/dev/null 2>&1 || true' EXIT
winpath() { printf 'Z:%s' "${1//\//\\}"; }
# A failing test must exit rather than wait in Wine's interactive crash debugger.
xvfb-run -a "$WINE" reg.exe add 'HKLM\Software\Microsoft\Windows NT\CurrentVersion\AeDebug' \
  /v Debugger /t REG_SZ /d '' /f >/dev/null
xvfb-run -a "$WINE" "$(winpath "$WINDOWS_DOTNET")" \
  "$(winpath "$ROOT/tests/AdapterSmoke/bin/Release/net10.0/AdapterSmoke.dll")" "$(winpath "$ROOT")"
