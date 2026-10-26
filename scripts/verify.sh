#!/usr/bin/env bash
# 全量验收门（docs/10 §6）：增量构建会跳过 XAML 编译，必须 no-incremental。
# 用法：scripts/verify.sh [项目]  —— 默认 LunarWard.slnx
#
# 构建产物与日志一律放 $HOME/.cache/lwart-verify。不要用 /tmp：本机 /tmp 是 12G tmpfs，
# 多个 agent 的 --artifacts-path 叠加会把它塞满（2026-10-26 全队停车事故，见 docs/10 §7.1）。
set -uo pipefail
cd "$(dirname "$0")/.."

TARGET="${1:-LunarWard.slnx}"
CACHE="${LUNARWARD_VERIFY_CACHE:-$HOME/.cache/lwart-verify}"
mkdir -p "$CACHE"
LOG="$CACHE/verify.log"
ART="$CACHE/artifacts"

echo "== 1/4 全量重建 $TARGET (no-incremental) =="
if ! dotnet build "$TARGET" --no-incremental --nologo --artifacts-path "$ART" >"$LOG" 2>&1; then
    grep -E "error (AVLN|CS|XAML|MSB)" "$LOG" | grep -v warning | sed -E 's#^.*/lunarward/##' | sort -u | head -40
    echo "-- 错误总数: $(grep -E "error (AVLN|CS|XAML|MSB)" "$LOG" | grep -vc warning)（完整日志: $LOG）"
    exit 1
fi
echo "   0 error"

echo "== 2/4 单元测试 =="
if ! dotnet test tests/LunarWard.UnitTests/LunarWard.UnitTests.csproj --nologo --no-build --artifacts-path "$ART" 2>&1 | tail -3; then
    echo "   测试失败"
    exit 1
fi

echo "== 3/4 截图自检（17 页） =="
DLL="$ART/bin/LunarWard/debug/LunarWard.dll"
SMOKE="$CACHE/smoke"
mkdir -p "$SMOKE"
fail=0
for p in launcher gacha beyond selfquery screenshot toolbox settings game-setting; do
    if dotnet "$DLL" --screenshot "$SMOKE/$p.png" --page "$p" --wait 600 2>&1 | grep -q "SCREENSHOT_OK"; then
        echo "   [$p] OK"
    else
        echo "   [$p] FAILED"
        fail=$((fail + 1))
    fi
done
for s in about general download files screenshot hotkey gamepad advanced toolbox; do
    if dotnet "$DLL" --screenshot "$SMOKE/settings-$s.png" --page settings --settings-section "$s" --wait 600 2>&1 | grep -q "SCREENSHOT_OK"; then
        echo "   [settings:$s] OK"
    else
        echo "   [settings:$s] FAILED"
        fail=$((fail + 1))
    fi
done
if [ "$fail" -ne 0 ]; then
    echo "-- 截图失败 $fail 页（图在 $SMOKE）"
    exit 1
fi

echo "== 4/4 对齐报告 =="
python3 scripts/check-alignment.py || exit 1
echo "== OK =="
