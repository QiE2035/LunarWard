#!/usr/bin/env python3
"""统计测试结果里的「跳过」数，并在超过阈值时失败。

审计 M-02/BT-01：测试里 43 处 `if (!环境) return;` 是静默跳过，报表上看不出「没跑」。
把 TRX 里的 NotExecuted 数出来，CI 就能对「跳过变多」变红。
"""

from __future__ import annotations

import argparse
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def count_trx(path: Path) -> tuple[int, int, int]:
    """返回 (passed, skipped, failed)。"""
    root = ET.parse(path).getroot()
    passed = skipped = failed = 0
    for result in root.iterfind(".//t:UnitTestResult", NS):
        outcome = result.get("outcome", "")
        if outcome in ("Passed",):
            passed += 1
        elif outcome in ("NotExecuted", "Inconclusive", "Pending"):
            skipped += 1
        else:
            failed += 1
    return passed, skipped, failed


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("results", nargs="*", default=["artifacts/test-results"])
    parser.add_argument("--max-skips", type=int, default=30)
    args = parser.parse_args()

    paths: list[Path] = []
    for raw in args.results:
        path = Path(raw)
        paths.extend(path.rglob("*.trx") if path.is_dir() else [path])

    if not paths:
        print("check-skips: 没有找到 .trx，跳过统计（是否没加 --logger trx？）")
        return 0

    total_passed = total_skipped = total_failed = 0
    for path in sorted(paths):
        passed, skipped, failed = count_trx(path)
        total_passed += passed
        total_skipped += skipped
        total_failed += failed
        print(f"  {path.name}: 通过 {passed} / 跳过 {skipped} / 失败 {failed}")

    print(f"合计：通过 {total_passed} / 跳过 {total_skipped} / 失败 {total_failed}（上限 {args.max_skips}）")
    if total_failed:
        print("check-skips: 有失败用例", file=sys.stderr)
        return 1
    if total_skipped > args.max_skips:
        print(
            f"check-skips: 跳过 {total_skipped} 个，超过上限 {args.max_skips}；"
            "新增静默跳过请改用 Assert.Skip 并说明环境条件",
            file=sys.stderr,
        )
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
