# 真机验收记录 — 2026-10-25

> 环境：CachyOS / KDE Wayland，本机已安装《原神》国服（`~/games/hk4e_cn`）与《绝区零》国服。
> 记录方式：`--screenshot` 截图 + `launch-report.json` + 手工观察。

## 结论速览

| # | 场景 | 结果 | 证据 |
|---|---|---|---|
| R1 | 官方国服启动 | ✅ | 见下 |
| R12 | 退出无残留 | ✅（部分） | 见下 |
| R2–R11、R13–R15 | 见说明 | ⏳ | 需 B 站账号 / 额外 runner / 视频资源等前置 |

## R1 — 官方国服启动

```
$ lunarward startgame --biz hk4e_cn
STARTED hk4e_cn pid=699177
```

`launch-report.json`（2026-10-25T13:51:49+08:00，profile `genshin-cn`）：

- `success: true`、`exit_code: 0`
- 七个相位齐全：`Prepare` → `Launch`（`…/spritz-wine-cachyos-wow64-10.0-11/bin/wine YuanShen.exe`）→
  `AwaitImage`（pid=699177，2040ms）→ **`AwaitReady`（signal `WineDevice`，74ms）** →
  `PostLaunch`（0 个自动程序）→ `Running` → `Cleanup`

判定：登录界面出现（AwaitReady 命中 WineDevice），符合通过标准。

## R12 — 退出无残留（部分）

- 游戏退出后 `pgrep -af spritz-wine-cachyos` 无输出；本报告是在 `Cleanup` 相位写完后读取的。
- 没有做「强杀游戏」的崩溃路径（那属于 R15），所以记「部分」。

## 其余项的前置

| # | 缺什么 |
|---|---|
| R2/R3 | B 站区服切换需要 B 站账号与渠道 DLL（涉及第三方 SDK，仓库不带补丁二进制） |
| R4/R5/R6/R7 | 需要用户自备注入器/外部程序；功能已有单测与 UI 自检，真人场景待用户自测 |
| R8/R9/R10 | 需要在游戏设置里分别切 System / Managed / Proton runner（本机已装 sleepy-launcher 与 Proton） |
| R11 | sandbox 首期不做（见 roadmap §1 的 M1 备注） |
| R13 | 需要 `libvlc` + 一段背景视频 |
| R14 | 环境 shell 已实现，`wine regedit` 待在游戏运行态下人工验证 |
| R15 | 需强杀游戏再重启 |

## 本轮新增的自动化证据（可重复）

- `lunarward install-smoke <dir>`：真实 CDN 下载 + 续传（474 MB → 687 MB，未重下）。
- `--install-real <dir> [--install-info-popup]`：真实下载进度与「下载中/下载/安装/校验」卡片。
- `scripts/smoke-screenshots.sh`：八页渲染冒烟。
