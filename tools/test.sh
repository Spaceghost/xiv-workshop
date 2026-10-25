#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
tools/build.sh host
python3 tests/test_runtime.py
source tools/env.sh
"$DOTNET" run --project tests/AdapterSmoke/AdapterSmoke.csproj -c Release -p:NuGetAudit=false -- "$ROOT"
