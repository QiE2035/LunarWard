#!/usr/bin/env bash
# 从上游 Starward 复制源文件到本仓库，作为移植起点。
#
# 使用方式（在仓库根目录执行）：
#   scripts/port-from-starward.sh src/Starward/Features/Gacha/UIGF/UIGF4GachaWindow.xaml.cs src/LunarWard.App/Views/Starward/Uigf4GachaWindow.axaml.cs
#   STARWARD_SRC=/path/to/Starward scripts/port-from-starward.sh ...
#
# 流程（见 docs/00-DECISIONS.md D14）：
#   1. 先原样复制上游文件（保留缩进、注释、字段名），避免"凭记忆重写"。
#   2. 在文件顶部加一段来源注释（本脚本自动加，可再改）。
#   3. 只在必要处改名/换 API/去 Windows 依赖，逐处留 `// 上游：` 注释。
#   4. 复制后先编译，再逐步修改，避免一次改太多。
#
# 环境变量：STARWARD_SRC，默认 /home/qie2035/projects/csharp/Starward。
set -euo pipefail

STARWARD_SRC="${STARWARD_SRC:-/home/qie2035/projects/csharp/Starward}"

if [[ $# -ne 2 ]]; then
    echo "用法: $0 <上游相对路径> <本仓库相对路径>" >&2
    exit 2
fi

src="$STARWARD_SRC/$1"
dst="$2"

if [[ ! -f "$src" ]]; then
    echo "找不到上游文件: $src" >&2
    exit 1
fi

mkdir -p "$(dirname "$dst")"

if [[ -e "$dst" ]]; then
    echo "目标已存在，拒绝覆盖: $dst" >&2
    echo "如需重新复制，先自己移走旧文件。" >&2
    exit 1
fi

cp --reflink=auto "$src" "$dst"
echo "已复制: $src -> $dst"
echo
echo "下一步："
echo "  1. 在文件顶部补来源注释（上游路径 + 移植日期 + 改动点）。"
echo "  2. 先编译，再逐处改名/替换 API，每处留 '// 上游：' 注释。"
echo "  3. 改名后的 public 类型要同步改 using 与调用点。"
