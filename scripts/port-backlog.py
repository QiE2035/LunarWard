#!/usr/bin/env python3
"""生成 docs/11-PORT-BACKLOG.md：上游 src/Starward 每个文件在本仓库的移植状态。

用法：
    python3 scripts/port-backlog.py            # 打印
    python3 scripts/port-backlog.py --write    # 写入 docs/11-PORT-BACKLOG.md

判定：上游相对路径在本地存在（.xaml 对应 .axaml）即视为「有文件」；
但「有文件」不等于「已对齐」——对齐程度由人工在 BACKLOG 表格的「备注」列维护，
本脚本只负责机械盘点，避免漏文件。
"""
import os, sys, collections

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UP = os.path.join(ROOT, '../csharp/Starward/src/Starward')
LOCAL = os.path.join(ROOT, 'src/LunarWard')


def walk(root, exts):
    out = []
    for dp, dn, fn in os.walk(root):
        dn[:] = [d for d in dn if d not in ('bin', 'obj')]
        for f in fn:
            if f.endswith(exts):
                out.append(os.path.relpath(os.path.join(dp, f), root))
    return out


up = sorted(walk(UP, ('.cs', '.xaml')))
local = walk(LOCAL, ('.cs', '.axaml'))
local_map = {p.replace('.axaml', '.xaml'): p for p in local}
up_set = {p.replace('.xaml', '.axaml') for p in up}

rows = []
for p in up:
    got = local_map.get(p)
    rows.append((p, got))
local_only = sorted(p for p in local if p.replace('.axaml', '.xaml') not in up_set)

groups = collections.defaultdict(list)
for p, got in rows:
    top = '/'.join(p.split('/')[:2]) if p.startswith('Features/') else p.split('/')[0]
    groups[top].append((p, got))

missing = sum(1 for _, g in rows if not g)
lines = []
lines.append('# 11 — 移植清单（上游 src/Starward → 本仓库 src/LunarWard）')
lines.append('')
lines.append('> 由 `python3 scripts/port-backlog.py --write` 生成，勿手改表格；'
             '人工结论写在每节标题下的「备注」里。')
lines.append('')
lines.append(f'上游文件 **{len(up)}** 个，本地已有同名落点 **{len(up) - missing}** 个，'
             f'**缺 {missing}** 个；本地独有文件 **{len(local_only)}** 个（需并入 Features 或删除）。')
lines.append('')
lines.append('> 注：上表「缺」包含以**等价实现**承载的上游文件（WinUI/Win32 专有物，见'
             '`scripts/check-alignment.py` 的 `EQUIVALENTS`）；等价项在 `check-alignment.py` 里单独计数、'
             '不计为失败。文件级对齐以 `check-alignment.py` 的 `missing: []` 为准。')
lines.append('')
lines.append('判定标准：上游相对路径在本地存在（`.xaml` ↔ `.axaml`）。「有文件」只说明'
             '路径对齐，内容是否已按上游实现，需要逐节核对。')
lines.append('')
lines.append('## 总览')
lines.append('')
lines.append('| 分组 | 上游文件 | 已有 | 缺失 |')
lines.append('|---|---:|---:|---:|')
for k in sorted(groups, key=lambda k: -len(groups[k])):
    g = groups[k]
    have = sum(1 for _, x in g if x)
    lines.append(f'| {k} | {len(g)} | {have} | {len(g) - have} |')
lines.append('')
for k in sorted(groups, key=lambda k: -len(groups[k])):
    g = groups[k]
    have = sum(1 for _, x in g if x)
    lines.append(f'## {k}（{have}/{len(g)}）')
    lines.append('')
    if have == len(g):
        lines.append('路径已全部对齐（内容仍需逐文件核对）。')
        lines.append('')
        continue
    lines.append('| 上游文件 | 本地落点 |')
    lines.append('|---|---|')
    for p, got in g:
        lines.append(f'| `{p}` | {"`" + got + "`" if got else "❌ 缺"} |')
    lines.append('')
lines.append('## 本地独有文件（上游无对应）')
lines.append('')
lines.append('按 D1（严格移植）的判定：这些文件要么并入对应 Features 目录并改成上游同名文件，'
             '要么删除（若其职责由上游文件覆盖），要么登记为平台等价物。')
lines.append('')
for p in local_only:
    lines.append(f'- `{p}`')
lines.append('')

text = '\n'.join(lines)
if '--write' in sys.argv:
    with open(os.path.join(ROOT, 'docs/11-PORT-BACKLOG.md'), 'w', encoding='utf-8') as f:
        f.write(text + '\n')
    print('wrote docs/11-PORT-BACKLOG.md')
else:
    print(text[:2000])
