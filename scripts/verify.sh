#!/usr/bin/env bash
# 全量验收门（docs/10 §6）：增量构建会跳过 XAML 编译，必须 no-incremental。
# 用法：scripts/verify.sh [项目]  —— 默认 LunarWard.slnx
set -uo pipefail
cd "$(dirname "$0")/.."

TARGET="${1:-LunarWard.slnx}"
LOG="$(mktemp -t lunarward-verify.XXXXXX.log)"

echo "== 1/3 全量重建 $TARGET (no-incremental) =="
if ! dotnet build "$TARGET" --no-incremental --nologo >"$LOG" 2>&1; then
    grep -E "error (AVLN|CS|XAML)" "$LOG" | grep -v warning | sed -E 's#^.*/lunarward/##' | sort | uniq | head -40
    echo "-- 错误总数: $(grep -E "error (AVLN|CS|XAML)" "$LOG" | grep -vc warning)（完整日志: $LOG）"
    exit 1
fi
echo "   0 error"

echo "== 2/3 单元测试 =="
if ! dotnet test tests/LunarWard.UnitTests/LunarWard.UnitTests.csproj --nologo 2>&1 | tail -3; then
    echo "   测试失败"
    exit 1
fi

echo "== 3/3 对齐报告 =="
python3 scripts/check-alignment.py || exit 1
echo "== OK =="
