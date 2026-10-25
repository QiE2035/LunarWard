#!/usr/bin/env bash
# 本地跑一遍 CI 的检查（与 .github/workflows/ci.yml 同序）。
# 用法：scripts/ci.sh [--with-upstream /path/to/Starward]
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet restore LunarWard.slnx
dotnet build LunarWard.slnx -c Release --no-restore
dotnet test tests/LunarWard.UnitTests/LunarWard.UnitTests.csproj -c Release --no-build
dotnet test tests/LunarWard.Installer.UnitTests/LunarWard.Installer.UnitTests.csproj -c Release --no-build

if [[ "${1:-}" == "--with-upstream" ]]; then
  python3 scripts/check-parity.py --upstream "$2"
else
  python3 scripts/check-parity.py
fi

echo 'CI OK'
