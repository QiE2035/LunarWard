#!/usr/bin/env bash
# 本地跑一遍 CI 的检查（与 .github/workflows/ci.yml 同序）。
# 用法：scripts/ci.sh [--with-upstream /path/to/Starward]
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet restore LunarWard.slnx
dotnet build LunarWard.slnx -c Release --no-restore
# 警告门禁：-t:Rebuild 强制全量重编，增量构建会漏掉未重编项目的警告。
dotnet build LunarWard.slnx -c Release -t:Rebuild -warnaserror --no-restore
rm -rf artifacts/test-results
dotnet test tests/LunarWard.UnitTests/LunarWard.UnitTests.csproj -c Release --no-build \
  --logger "trx;LogFileName=unit.trx" --results-directory artifacts/test-results
dotnet test tests/LunarWard.Installer.UnitTests/LunarWard.Installer.UnitTests.csproj -c Release --no-build \
  --logger "trx;LogFileName=installer.trx" --results-directory artifacts/test-results
# 审计 M-02：集成测试也要跑（启动链路/相位引擎），跳过数由下一步卡上限。
dotnet test tests/LunarWard.IntegrationTests/LunarWard.IntegrationTests.csproj -c Release --no-build \
  --logger "trx;LogFileName=integration.trx" --results-directory artifacts/test-results
python3 scripts/check-skips.py artifacts/test-results --max-skips 30

if [[ "${1:-}" == "--with-upstream" ]]; then
  python3 scripts/check-parity.py --upstream "$2"
else
  python3 scripts/check-parity.py
fi

echo 'CI OK'
