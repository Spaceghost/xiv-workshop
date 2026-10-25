#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT/tools/env.sh"
cd "$ROOT"
[[ -f build/ui-test/cimgui.so ]] || tools/build-ui-test.sh
"$DOTNET" run --project tests/UiSmoke/UiSmoke.csproj -c Release -p:NuGetAudit=false -- "$ROOT"
python3 tools/captures.py
