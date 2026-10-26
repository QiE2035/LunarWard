#!/usr/bin/env python3
"""对齐验收检查（docs/10-ALIGNMENT.md §6）。

对上游 Starward 只读副本做四类机械检查：

1. 工程表：本仓库是否具备与上游对应的工程（LunarWard / Core / Language / Setup.Core）。
2. 依赖表：上游用到的跨平台 NuGet 包，本仓库 Directory.Packages.props 是否有同名同版本。
3. 文件映射：上游 src/Starward、src/Starward.Core、src/Starward.Language、src/Starward.Setup.Core
   的每个源文件，在本仓库是否有同名落点（.xaml → .axaml）。
4. 泄漏：移植过来的文件里是否还残留 `Starward.` 命名空间 / `Microsoft.UI` / `Windows.*` 引用。

用法：
    python3 scripts/check-alignment.py            # 人类可读
    python3 scripts/check-alignment.py --strict   # 有任何一项失败就 exit 1
    python3 scripts/check-alignment.py --json     # 机器可读
"""

from __future__ import annotations

import json
import os
import re
import sys
import xml.etree.ElementTree as ET

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UPSTREAM = os.environ.get("STARWARD_UPSTREAM", "/home/qie2035/projects/csharp/Starward")

# 上游工程 → 本仓库工程（None = 允许缺失，原因写 third 列）
PROJECTS = [
    ("src/Starward/Starward.csproj", "src/LunarWard/LunarWard.csproj", ""),
    ("src/Starward.Core/Starward.Core.csproj", "src/LunarWard.Core/LunarWard.Core.csproj", ""),
    ("src/Starward.Language/Starward.Language.csproj", "src/LunarWard.Language/LunarWard.Language.csproj", ""),
    ("src/Starward.Setup.Core/Starward.Setup.Core.csproj", "src/LunarWard.Setup.Core/LunarWard.Setup.Core.csproj", ""),
    ("src/Starward.RPC/Starward.RPC.csproj", "src/LunarWard.RPC/LunarWard.RPC.csproj", ""),
    ("src/Starward.Setup/Starward.Setup.csproj", None, "以 src/LunarWard.Installer 等价（docs/10 §8.2）"),
    ("src/Starward.Launcher/Starward.Launcher.vcxproj", None, "以 Program.cs + .desktop 承担（docs/10 §8.3）"),
]

# Windows 专有包：本仓库用等价物，不要求同名
WINDOWS_ONLY_PREFIXES = (
    "Microsoft.WindowsAppSDK", "Microsoft.Windows.CsWinRT", "Microsoft.Windows.SDK.BuildTools",
    "Microsoft.Graphics.Win2D", "ComputeSharp.",
    "CommunityToolkit.WinUI.", "H.NotifyIcon.WinUI", "H.InputSimulator",
    "Scighost.WinUI", "Starward.Codec", "Starward.GameInput", "Starward.NativeLib",
    "Starward.Win2D", "Vanara.", "WindowsAppSDK", "Aprillz.MewUI", "WindowsShortcutFactory",
)

# 只被「本轮不建」的工程/Windows 专有面用到的包：登记原因，不算失败
DEFERRED_PACKAGES: dict[str, str] = {}


# 上游文件 → 本地等价实现（不是缺文件，而是平台替换/合并实现；原因必须能在 docs/09 找到）
EQUIVALENTS = {
    ("src/Starward", "Controls/ColorRectChart.xaml"):
        "Controls/ColorRectChart.cs（自定义绘制，docs/09 §8.1 第 2 条）",
    ("src/Starward", "Controls/ColorRectChart.xaml.cs"):
        "Controls/ColorRectChart.cs（自定义绘制，docs/09 §8.1 第 2 条）",
    ("src/Starward", "Helpers/FileDialogNative.cs"):
        "不移植：Win32 COM 细节，由 Avalonia StorageProvider 覆盖（docs/09 六）",
    ("src/Starward", "Features/ViewHost/SystemTrayWindow.xaml"):
        "托盘菜单改 Avalonia NativeMenu，由 Features/ViewHost/SystemTrayWindow.cs 承载（docs/09 五）",
}

SOURCE_TREES = [
    ("src/Starward", "src/LunarWard"),
    ("src/Starward.RPC", "src/LunarWard.RPC"),
    ("src/Starward.Core", "src/LunarWard.Core"),
    ("src/Starward.Language", "src/LunarWard.Language"),
    ("src/Starward.Setup.Core", "src/LunarWard.Setup.Core"),
]

SKIP_DIR_NAMES = {"bin", "obj", "Properties", ".vs"}
SKIP_SUFFIXES = (".Designer.cs",)

LEAK_PATTERNS = [
    (re.compile(r"^\s*using\s+Starward[.;]", re.M), "using Starward.*"),
    (re.compile(r"\bnamespace\s+Starward[.;\s]"), "namespace Starward"),
    (re.compile(r"Starward\.(Core|Language|Features|Frameworks|Helpers|Services)\b"), "Starward.* 命名空间引用"),
    (re.compile(r"^\s*using\s+(Microsoft\.UI|Windows\.)", re.M), "using Microsoft.UI / Windows.*"),
]


def iter_sources(root: str):
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIR_NAMES]
        for name in filenames:
            if not name.endswith((".cs", ".xaml", ".resx", ".proto")):
                continue
            if name.endswith(SKIP_SUFFIXES):
                continue
            full = os.path.join(dirpath, name)
            yield os.path.relpath(full, root)


def map_name(rel: str) -> str:
    """上游 .xaml → 本仓库 .axaml；.xaml.cs → .axaml.cs；其余原样。"""
    if rel.endswith(".xaml.cs"):
        return rel[: -len(".xaml.cs")] + ".axaml.cs"
    if rel.endswith(".xaml"):
        return rel[: -len(".xaml")] + ".axaml"
    return rel


def check_projects():
    rows = []
    for up, ours, why in PROJECTS:
        up_abs = os.path.join(UPSTREAM, up)
        if not os.path.exists(up_abs):
            rows.append((up, ours, "上游不存在", False))
            continue
        if ours is None:
            rows.append((up, None, why, True))
            continue
        ok = os.path.exists(os.path.join(REPO, ours))
        rows.append((up, ours, "" if ok else "缺失", ok))
    return rows


def upstream_packages():
    """解析上游所有 csproj 的 PackageReference（含版本），返回 {name: set(versions)}。"""
    pkgs: dict[str, set[str]] = {}
    for dirpath, dirnames, filenames in os.walk(os.path.join(UPSTREAM, "src")):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIR_NAMES]
        for name in filenames:
            if not name.endswith(".csproj"):
                continue
            try:
                tree = ET.parse(os.path.join(dirpath, name))
            except ET.ParseError:
                continue
            for node in tree.iter():
                if node.tag.endswith("PackageReference"):
                    pkg = node.attrib.get("Include")
                    ver = node.attrib.get("Version")
                    if not pkg:
                        continue
                    if ver is None:
                        for child in node:
                            if child.tag.endswith("Version"):
                                ver = (child.text or "").strip()
                    pkgs.setdefault(pkg, set()).add(ver or "?")
    return pkgs


def ours_packages():
    path = os.path.join(REPO, "Directory.Packages.props")
    pkgs: dict[str, set[str]] = {}
    tree = ET.parse(path)
    for node in tree.iter():
        if node.tag.endswith("PackageVersion"):
            pkg = node.attrib.get("Include")
            if pkg:
                pkgs[pkg] = {node.attrib.get("Version", "?")}
    return pkgs


def check_packages():
    up = upstream_packages()
    ours = ours_packages()
    missing, mismatch, windows = [], [], []
    for pkg, versions in sorted(up.items()):
        if pkg.startswith(WINDOWS_ONLY_PREFIXES):
            windows.append(pkg)
            continue
        if pkg in DEFERRED_PACKAGES:
            continue
        if pkg not in ours:
            missing.append(pkg)
        elif not (versions & ours[pkg]):
            mismatch.append((pkg, sorted(versions), sorted(ours[pkg])))
    extra = sorted(set(ours) - set(up))
    return missing, mismatch, windows, extra


def check_files():
    rows = []
    for up_tree, our_tree in SOURCE_TREES:
        up_abs = os.path.join(UPSTREAM, up_tree)
        if not os.path.isdir(up_abs):
            continue
        present, missing, equiv = 0, [], []
        for rel in sorted(iter_sources(up_abs)):
            target = os.path.join(REPO, our_tree, map_name(rel))
            if os.path.exists(target):
                present += 1
            elif (up_tree, rel) in EQUIVALENTS:
                equiv.append(rel)
            else:
                missing.append(rel)
        rows.append((up_tree, our_tree, present, missing, equiv))
    return rows


def check_leaks():
    """检查移植文件里是否残留上游命名空间/WinUI using。

    只看「代码行」：`//`、`///`、`*`、`<!--` 开头的行（注释、XML 文档、上游路径说明）跳过，
    否则「上游：src/Starward/Helpers/xxx.cs」这种注释会被误判。
    """
    leaks = []
    roots = [os.path.join(REPO, "src", "LunarWard"), os.path.join(REPO, "src", "LunarWard.Core")]
    for root in roots:
        for dirpath, dirnames, filenames in os.walk(root):
            dirnames[:] = [d for d in dirnames if d not in SKIP_DIR_NAMES]
            for name in filenames:
                if not name.endswith(".cs"):
                    continue
                full = os.path.join(dirpath, name)
                try:
                    lines = open(full, encoding="utf-8-sig").read().splitlines()
                except UnicodeDecodeError:
                    continue
                for line in lines:
                    stripped = line.strip()
                    if not stripped or stripped.startswith(("//", "///", "*", "/*", "<!--")):
                        continue
                    if "Starward/" in stripped:
                        continue
                    for pattern, label in LEAK_PATTERNS:
                        if pattern.search(line):
                            leaks.append((os.path.relpath(full, REPO), f"{label} → {stripped[:90]}"))
                            break
    return leaks


def main() -> int:
    strict = "--strict" in sys.argv
    as_json = "--json" in sys.argv

    projects = check_projects()
    missing_pkgs, mismatch_pkgs, windows_pkgs, extra_pkgs = check_packages()
    files = check_files()
    leaks = check_leaks()

    total_up = sum(row[2] + len(row[3]) + len(row[4]) for row in files)
    total_present = sum(row[2] for row in files)
    total_equiv = sum(len(row[4]) for row in files)
    missing_files = [(row[0], rel) for row in files for rel in row[3]]

    ok = (
        all(row[3] for row in projects)
        and not missing_pkgs
        and not mismatch_pkgs
        and not missing_files
        and not leaks
    )

    if as_json:
        print(json.dumps({
            "ok": ok,
            "projects": projects,
            "packages": {
                "missing": missing_pkgs,
                "mismatch": mismatch_pkgs,
                "windows_only": windows_pkgs,
                "local_extra": extra_pkgs,
            },
            "files": {
                "upstream": total_up,
                "present": total_present,
                "equivalent": total_equiv,
                "missing": [f"{t}/{r}" for t, r in missing_files],
            },
            "leaks": leaks,
        }, ensure_ascii=False, indent=2))
        return 0 if (ok or not strict) else 1

    print("# 对齐验收检查（scripts/check-alignment.py）\n")
    print(f"上游：{UPSTREAM}\n")

    print("## 1. 工程表\n")
    for up, ours, why, good in projects:
        print(f"- [{'OK' if good else 'MISS'}] `{up}` → {f'`{ours}`' if ours else '—'}{('  ' + why) if why else ''}")

    print("\n## 2. 依赖表\n")
    if missing_pkgs:
        print(f"- 缺（跨平台，必须补）：{', '.join(missing_pkgs)}")
    if mismatch_pkgs:
        for pkg, upv, ourv in mismatch_pkgs:
            print(f"- 版本不一致：{pkg} 上游 {upv} / 本地 {ourv}")
    if not missing_pkgs and not mismatch_pkgs:
        print("- 跨平台包交集版本全部一致。")
    print(f"- Windows 专有包（走等价物，不计失败）：{len(windows_pkgs)} 个")
    if extra_pkgs:
        print(f"- 本地额外包（平台/工具链）：{', '.join(extra_pkgs)}")

    print("\n## 3. 文件映射\n")
    for up_tree, our_tree, present, missing, equiv in files:
        print(f"- `{up_tree}` → `{our_tree}`：{present}/{present + len(missing) + len(equiv)}"
              f"（等价实现 {len(equiv)}）")
        for rel in missing[:400]:
            print(f"    - 缺 `{rel}`")
        if len(missing) > 400:
            print(f"    - …还有 {len(missing) - 400} 个")
        for rel in equiv:
            print(f"    - 等价 `{rel}` → {EQUIVALENTS[(up_tree, rel)]}")
    print(f"\n合计：{total_present}/{total_up}（等价实现 {total_equiv}）")

    print("\n## 4. 命名空间泄漏\n")
    if leaks:
        for path, label in leaks[:80]:
            print(f"- `{path}`：{label}")
        if len(leaks) > 80:
            print(f"- …还有 {len(leaks) - 80} 处")
    else:
        print("- 无。")

    print("\n## 结论\n")
    print("PASS" if ok else "FAIL（细则见上；移植进行中时 FAIL 属预期）")
    return 0 if (ok or not strict) else 1


if __name__ == "__main__":
    sys.exit(main())
