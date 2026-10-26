#!/usr/bin/env python3
"""对照 08-UI-PARITY 表：上游文件与 LunarWard 目标文件是否存在、行数是否漂移。

用法：
    scripts/check-parity.py [--upstream /path/to/Starward]

退出码：0 = 全部可解析文件都存在；1 = 有缺失文件（打印清单）。
行数漂移超过 15% 只告警（审计 M-28：原阈值 50% 太松，135 行的文件漂到 368 也不报）；
文件名重名（如两个 GameSelector.xaml）直接失败——按 basename 兜底会静默挑错一个。
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DOC = ROOT / 'docs' / '08-UI-PARITY.md'
PATH_RE = re.compile(r'`((?!\.)[\w./-]+\.(?:xaml|cs|axaml))`(?:\s*\([约\s]*(\d+)[^)]*\))?')


def rows(text):
    for line in text.splitlines():
        if not line.startswith('|'):
            continue
        cells = [c.strip() for c in line.strip().strip('|').split('|')]
        if len(cells) < 4 or cells[0] in ('页面', '---'):
            continue
        yield cells[0], cells[1], cells[2]


def main() -> int:
    upstream = None
    if '--upstream' in sys.argv:
        upstream = Path(sys.argv[sys.argv.index('--upstream') + 1])

    text = DOC.read_text(encoding='utf-8')
    if '【已废弃' in text:
        print('docs/08-UI-PARITY.md 已废弃（重实现时期的页面计划，已被 Features/** 上游同名页取代）')
        print('当前验收改用：python3 scripts/check-alignment.py')
        return 0
    missing, checked = [], 0
    for page, src, dst in rows(text):
        for cell, base in ((src, upstream), (dst, ROOT)):
            if base is None:
                continue
            for path, count in PATH_RE.findall(cell):
                target = base / path
                checked += 1
                if not target.exists():
                    # 文档里的路径常省略 src/.../Views/Starward 前缀，退化到按文件名找；
                    # 但重名文件必须报错而不是随便挑一个（审计 M-28）。
                    matches = list(base.rglob(Path(path).name))
                    if not matches:
                        missing.append((page, str(target), '文件不存在'))
                        continue
                    if len(matches) > 1:
                        names = ', '.join(str(m.relative_to(base)) for m in matches[:4])
                        missing.append((page, str(target), f'文件名重名，请写全路径（{names}）'))
                        continue
                    target = matches[0]
                if count and target.suffix in ('.xaml', '.cs', '.axaml'):
                    actual = sum(1 for _ in target.open(encoding='utf-8', errors='ignore'))
                    expected = int(count)
                    if expected and abs(actual - expected) / max(expected, 1) > 0.15:
                        print(f'[warn] {page}: {path} 行数 {actual}（文档写 {expected}）')

    print(f'检查 {checked} 个文件引用')
    for page, path, why in missing:
        print(f'[FAIL] {page}: {why} {path}')
    return 1 if missing else 0


if __name__ == '__main__':
    sys.exit(main())
