#!/usr/bin/env bash
# 逐页截图冒烟：在隔离的 HOME/XDG 目录里把每个页面渲染成 PNG。
#
# 用途：改完 UI 后确认所有页面都还能构造、不抛未处理异常。截图统一落在
# 仓库根目录的 artifacts/smoke/ 下，方便人工对比或挂到 PR。
#
# 用法：scripts/smoke-screenshots.sh [页面名…]
#   不给页面名时跑全部页面。页面名与 --page 的取值一致。
set -euo pipefail

cd "$(dirname "$0")/.."

pages=("$@")
if [ ${#pages[@]} -eq 0 ]; then
    pages=(launcher gacha beyond selfquery screenshot toolbox settings game-setting)
fi

out="artifacts/smoke"
tmp="$(mktemp -d /tmp/lunarward-smoke.XXXXXX)"
mkdir -p "$out" "$tmp/home" "$tmp/cfg" "$tmp/data" "$tmp/cache"
trap 'rm -rf "$tmp"' EXIT

fail=0
for page in "${pages[@]}"; do
    log="$tmp/$page.log"
    if HOME="$tmp/home" \
        XDG_CONFIG_HOME="$tmp/cfg" \
        XDG_DATA_HOME="$tmp/data" \
        XDG_CACHE_HOME="$tmp/cache" \
        dotnet run --project src/LunarWard.App -c Debug -- \
        --screenshot "$out/$page.png" --page "$page" --wait 500 >"$log" 2>&1; then
        echo "OK   $page"
    else
        echo "FAIL $page（日志：$log）" >&2
        tail -20 "$log" >&2
        fail=1
    fi
done

if [ "$fail" -ne 0 ]; then
    echo "有页面渲染失败，见上方日志。" >&2
    exit 1
fi
echo "全部页面渲染通过，截图在 $out/。"
