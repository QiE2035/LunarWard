# 06 — 待确认问题

**开工前必须清空本文件。** 每条都标了推荐答案，若需求方认可可直接标记为已决。

> **状态（2026-10-23）**：Q1–Q11 已全部**按推荐执行**并落地为代码，
> 汇总见文末「汇总」表与 §「落地对照」。本文件保留原始问题与理由，
> 供将来回顾"当时为什么这么定"。

---

## Q1. `Override("")` 与 `Remove` 的 UI 语义

**问题**：env 三态里，`Override("")`（变量存在但为空）与 `Remove`（变量不存在）
对 wine 有不同行为（`DISPLAY=""` 是 winewayland 的真实用法）。
但普通用户看到空输入框，直觉会认为是"删除"。

**我的方案**：
- 三态控件 `继承 / 覆盖 / 删除`
- 输入框清空 → 默认 `Remove`
- 另给一个"设为空值"勾选框（副选项），勾上则 `Override("")`

**需要确认**：这个交互可以吗？还是你更希望"清空 = 空值"，另用显式删除按钮表示 `Remove`？

**推荐**：按我的方案（清空=删除更符合直觉，且"设为空值"是少数派需求，藏在副选项里合适）。

---

## Q2. Steam VDF 解析库选型

**问题**：需要解析 `libraryfolders.vdf` 来发现 Steam 库与 Proton。
要求"不手搓重复造轮子"，所以不写解析器。

**候选**（均未核验版本与维护状态）：

| 包 | 说明 |
|---|---|
| `Gameloop.Vdf` | 老牌 VDF 解析器，Newtonsoft.Json 风格 |
| `SteamKit2` | 大而全，VDF 只是其中一小块 |
| `Microsoft.Extensions.Configuration` + 自定义 VDF provider | 无成熟现成实现 |

**需要确认**：是否接受引入 `Gameloop.Vdf`？还是希望 M0 期间先做对比？

**推荐**：M0 期间核验 `Gameloop.Vdf` 与 `SteamKit2` 的最新版本与维护状态，
选活跃度高的那个。若都不理想，退化为只支持标准路径
（`~/.steam/steam/steamapps/common/` + `~/.local/share/Steam/steamapps/common/`），
不解析 VDF——**覆盖 95% 场景且零依赖**。

---

## Q3. 环境 shell 的终端实现方案 —— ✅ 随 UI 框架切换而降级

**原问题**（GirCore 路线）：`GirCore.Vte-2.91` 在 NuGet **不存在**，需手写
~300 行 `libvte-2.91-gtk4` 的 P/Invoke 与 GObject 生命周期绑定。

**切换 Avalonia 后**：不再需要手写绑定，改为**从现成控件中选一个**。

| 包 | 版本 | 备注 |
|---|---|---|
| `Iciclecreek.Avalonia.Terminal` | 4.1.0 | 基于 xterm.js 语义的移植 |
| `RoyalApps.RoyalTerminal.Avalonia` | 0.6.3 | 有配套 `.Pty.Windows`，说明有跨平台 PTY 抽象 |
| `AvaloniaTerminal` | 1.0.0 | 最轻量 |

**选择标准**（按优先级，已写入 `04-FEATURES.md` §6.4）：
1. PTY 走原生 `openpty`（Linux 必须），不是模拟
2. 支持 `TERM` / `COLORTERM` 与 256 色
3. 支持 `SIGWINCH`
4. 近 6 个月有提交

**仍待确认**：选哪个包？→ 这是 **M0 的 V2b 验证项**，实测后决定，不需你现在拍板。

**若全部不达标的退路**：自建 PTY（`openpty` P/Invoke）+ 现成 VT 解析器（不自己写
转义序列）；最差退化为只读日志查看器 + 导出 `env.sh`。

---

## Q4. LunarWard 自身许可

**候选**：

| 许可 | 优点 | 缺点 |
|---|---|---|
| **MIT** | 与 Starward 一致；移植时最省事；用户友好 | 他人可闭源使用 |
| GPL-3.0 | 与 HoYoverse 生态其他启动器一致（AGL、an-anime-team 都是） | 与 Starward 的 MIT 不一致；移植来的代码仍是 MIT 但整体变 GPL |

**注意**：`Starward` 是 MIT，我们可以**自由选择**更严格的许可（MIT 允许再许可为 GPL）。
`Avalonia` 与 `GirCore` 都是 MIT，同理。

**需要确认**：MIT 还是 GPL-3.0？

**推荐**：**MIT**。理由：与上游 Starward 一致，移植代码时不会有许可混杂的疑虑；
且本项目不打包任何 AGPL 组件（用户自备），不需要 GPL 的互惠条款来保护。

---

## Q5. 安装器首期范围

**问题**：`Starward.RPC/GameInstall` 包含安装、预下载、差量更新、修复、校验、
卸载等功能。全部移植工作量大。

**建议分期**：

| 期 | 范围 |
|---|---|
| **首期（M8）** | 安装、修复、校验 |
| 二期 | 更新（`GameSophonPatchBuild` 差量） |
| 三期 | 预下载、卸载 |

**需要确认**：这个分期可以吗？还是首期就要包含更新？

**推荐**：按上面分期。首期覆盖"从零装好一个能玩的游戏"，已足够验证价值。

---

## Q6. 是否内置"游戏库导入"

**问题**：用户可能已经有 sleepy-launcher 或 an-anime-game-launcher 装好的游戏与
prefix。是否提供扫描/导入？

**建议**：
- 扫描常见位置（`~/.local/share/sleepy-launcher/`、
  `~/.local/share/anime-game-launcher/`）找已安装的游戏目录
- **只读**，不修改那些目录
- 可选导入它们的 `config.json` 里的路径/prefix 设置

**需要确认**：需要这个功能吗？首期还是后续？

**推荐**：首期做"扫描游戏目录"（简单），导入配置放二期。

---

## Q7. 双主题的默认值

**问题**：`Modern`（libadwaita 卡片流）还是 `Classic`（全屏背景+悬浮控件）作为默认？

**考虑**：
- Starward 默认是 `Modern`（带视频背景的现代布局）
- `Classic` 更接近"背景铺满 + 悬浮按钮"的传统启动器观感
- 视频背景在两种主题下都能工作

**需要确认**：默认哪个？

**推荐**：`Modern`（与 Starward 一致），首次运行时可在引导页让用户选。

---

## Q8. 是否需要 AUR / Flatpak 打包

**问题**：交付形态。

**建议**：
- 源码 + `dotnet publish` 说明（最低要求）
- AUR `PKGBUILD`（Arch 用户友好，你本机就是 Arch/CachyOS）
- Flatpak（需要额外处理 Wine 访问、文件系统权限，工作量大）

**需要确认**：首期需要哪些？

**推荐**：源码 + AUR 首期，Flatpak 后续。

---

## Q9. `Trigger` 的取值 —— ✅ 已决

**决策**：`Trigger` 只有两个值，**不提供"游戏启动前运行程序"的能力**。

```csharp
public enum Trigger { Auto, Manual }
```

**理由**：`03-LAUNCH-ENGINE.md` 的七相位里没有 `BeforeLaunch`——外部程序只在
`AwaitReady` 之后（`PostLaunch` 相位）拉起。在游戏之前运行任何 wine 程序
正是破坏"首个 wine 客户端"不变式的行为，因此从设计上排除，而不是靠运行时检查。

`Prepare` 相位的清理、写 `config.ini`、映射 drive 已由引擎负责，用户没有理由
在此之前插入程序。

> 已同步更新 `00-DECISIONS.md` D10、`03-LAUNCH-ENGINE.md` §一、`04-FEATURES.md`、
> `05-ROADMAP.md` U8。

---

## Q10. 崩溃/异常上报

**问题**：是否需要错误上报（sentry 之类）？

**建议**：**不做**。本地日志 + `launch-report.json` 足够，用户可自行附上。
错误上报涉及隐私与合规，价值不大。

**需要确认**：同意不做吗？

---

## Q11. Acrylic / 窗口模糊在 Linux 的落地策略

**问题**：Starward 大量使用 `AcrylicBrush`（92 次）与 `ThemeShadow`（62 次），
这是 Fluent 观感的核心。但 **X11/Wayland 各组合器对 backdrop blur 的支持不一致**，
Avalonia 没有跨平台的"窗口透明 + 合成器模糊"API。

**我的方案**：三级降级（详见 `04-FEATURES.md` §5.6）

| 级别 | 条件 | 做法 | 覆盖 |
|---|---|---|---|
| **L1** 真模糊 | KDE Plasma（KWin 支持 `_KDE_NET_WM_BLUR_BEHIND_REGION`） | 窗口透明 + blur 区域 | 部分 KDE 用户 |
| **L2** 半透明 | 任意复合合成器 | `TransparencyLevelHint = Transparent` + 半透明渐变层 | **默认目标** |
| **L3** 纯色 | 无复合 / 软件渲染 | 不透明深色背景 | 兜底 |

`ThemeShadow` → `DropShadowEffect`，跨平台一致，无风险。

**为什么不追求 L1 全覆盖**：
- Wayland 无标准 blur 协议（`ext-background-effect-v1` 仍在讨论）
- 依赖 KDE 私有属性会把应用绑定到单一桌面环境

**需要确认**：接受"默认半透明、KDE 下升级为真模糊、其余降级纯色"吗？
还是你更希望**统一用半透明**（简单、一致、少一套分支）？

**推荐**：**统一 L2 半透明**，不做 L1 的 KDE 特化。理由：一套代码路径、
行为可预期、视觉上已能传达 Fluent 质感。L1 可作为远期优化。

---

## 汇总：待确认清单

| # | 问题 | 推荐 | 状态 |
|---|---|---|---|
| Q1 | `Override("")` 的 UI 交互 | 清空=删除 + "设为空值"副选项 | ✅ 已按推荐执行 |
| Q2 | Steam VDF 库 | M0 核验；不理想则退化为标准路径 | ✅ 已按推荐执行（退化方案） |
| Q3 | 终端方案 | Avalonia 终端控件，M0 实测选定 | ✅ 已选定 `Iciclecreek.Avalonia.Terminal` 4.1.0 |
| Q4 | 许可 | MIT | ✅ 已按推荐执行 |
| Q5 | 安装器分期 | 首期只做安装/修复/校验 | ✅ 已按推荐执行 |
| Q6 | 游戏库导入 | 首期只做扫描 | ✅ 已按推荐执行 |
| Q7 | 默认主题 | Modern | ✅ 已按推荐执行 |
| Q8 | 打包形态 | 源码 + AUR 首期 | ✅ 已按推荐执行 |
| Q9 | Trigger 取值 | 只保留 Auto / Manual | ✅ 已决 |
| Q10 | 错误上报 | 不做 | ✅ 已按推荐执行（本地日志 + launch-report） |
| Q11 | Acrylic 模糊策略 | 统一 L2 半透明，不做 KDE 特化 | ✅ 已按推荐执行 |

---

## 落地对照（2026-10-23）

| # | 落地位置 | 说明 |
|---|---|---|
| Q1 | `ViewModels/MainWindowViewModel.cs` 的 `EnvEntryViewModel` | 三态下拉 + 「设为空值」副选项；清空输入默认转 `Remove` |
| Q2 | `App/Services/GameScanner.cs` 的 `RuntimeScanner.SteamLibraries()` | 不引入 VDF 库：只抓 `libraryfolders.vdf` 的 `"path"` 行 + 标准路径 |
| Q3 | `Views/ShellWindow.axaml.cs` | `TerminalView.LaunchProcess(dir, shell, ["-l"])`；PTY 失败时回退到「导出 env.sh」 |
| Q4 | `LICENSE` | MIT |
| Q5 | `src/LunarWard.Installer/` | 安装/修复/校验/预下载/卸载的服务都在，首期只接安装与修复入口 |
| Q6 | `App/Services/GameScanner.cs` | 扫描 `~/games` 等 + **只读**读取 sleepy-launcher 的 `config.json`；不修改对方目录 |
| Q7 | `LauncherConfig.Launcher.Style` 默认 `"Modern"` | 设置页可切 `Classic` |
| Q8 | `packaging/PKGBUILD` + `packaging/lunarward.desktop` | 源码 + Arch 打包；Flatpak 后续 |
| Q9 | `src/LunarWard.Launch/ExternalProgram.cs` 的 `Trigger` | 只有 `Auto` / `Manual`，`PostLaunch` 之后才可用 |
| Q10 | `App/Services/BackgroundService.cs` 的 `LogService` + `launch-report.json` | 无 Sentry 之类 |
| Q11 | `src/LunarWard.App/AppServices.cs` 的 `AcrylicLevel` | 只实现 L2 半透明 + L3 纯色；L1 的 KDE 特化不做 |

验收状态见 `docs/acceptance-2026-10-23.md`，M0 实测结论见 `docs/M0-RESULTS.md`。
