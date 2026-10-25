# 真机验收记录 — 2026-10-25

> 环境：CachyOS / KDE Wayland，本机已安装《原神》国服（`~/games/hk4e_cn`）与《绝区零》国服。
> 记录方式：`--screenshot` 截图 + `launch-report.json` + 手工观察。

## 结论速览

| # | 场景 | 结果 | 证据 |
|---|---|---|---|
| R1 | 官方国服启动 | ✅ | 见下 |
| R8 | 系统 wine | ⏳ | 本机 PATH 里没有 wine（只有 sleepy-launcher 借用与 Steam Proton） |
| R10 | Proton（WineBinary） | ✅（启动到 WineDevice） | 见下 |
| R12 | 退出无残留 | ✅（部分） | 见下 |
| R2–R7、R9、R11、R13–R15 | 见说明 | ⏳ | 需 B 站账号 / 托管 runner 安装 / 视频资源等前置 |

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

## R10 — Proton 直接调 wine（Proton 10 / Experimental 的 files/bin 布局）

- 先在配置里临时加一个 `Proton` runtime（`steam_library_path=/home/qie2035/.local/share/Steam`，
  `proton_name=Proton - Experimental`）并设为默认，跑完立刻还原配置（`/tmp/lw-config-backup.json`）。
- 实测 `startgame --biz hk4e_cn`：`Launch` 相位用的是
  `…/Proton - Experimental/files/bin/wine`（**Proton 10 的新布局**），`AwaitReady` 69ms 命中 **WineDevice**，
  `success=true`。
- 但游戏本体随后以 **退出码 5** 结束（12s），属于「脱离 Steam 运行时直接调 Proton wine」的已知限制，
  不影响「安装器/启动器能起游戏进程」这一判定；如实记录，不粉饰。
- 顺带发现并修掉一个真实缺陷：`RunnerResolver.ResolveProton` 以前只认 `dist/bin/wine`（Proton ≤9），
  遇到 Proton 10 的 `files/bin/wine` 会直接抛「找不到 Proton wine 二进制」。现在两种布局都认，
  新增单测 `Proton10_FilesBinWine_IsAlsoSupported`。

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
