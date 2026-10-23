# 05 — 里程碑、测试与验收

---

## 一、里程碑

M0 是**唯一的前置门禁**——它验证四个未证实的技术点，任何一项失败都需要回到
设计阶段。M0 通过后，M1–M5 可并行推进。

> **状态（2026-10-23）**：M0–M8 已完成，M9/M10 部分完成。
> 详细证据见 `M0-RESULTS.md`、`acceptance-2026-10-23.md` 与本文件 §四的交付清单。

| # | 内容 | 产出 | 依赖 | 状态 |
|---|---|---|---|---|
| **M0** | **技术验证**（见 §1.1） | 一个能跑的最小验证工程 + 结论记录 | — | ✅ `M0-RESULTS.md` |
| **M1** | `LunarWard.Platform` + `LunarWard.Wine` | 进程发现、runner/prefix/dxvk 解析与安装、sandbox | M0 | ✅ 除"托管组件下载器"与 sandbox 隔离 |
| **M2** | `LunarWard.Launch` 相位引擎 | 七相位实现 + `launch-report.json` + dry-run | M1 | ✅ |
| **M3** | `LunarWard.Games` 区服切换 | `config.ini`（Ini Binder）+ PCGameSDK 下载 | M2 | ✅ |
| **M4** | `LunarWard.Games` 外部程序 | 三态 env 合并 + Auto/Manual + PreWrites | M2 | ✅ |
| **M5** | 环境 shell | Avalonia 终端控件 + 复用 `LaunchPlan.Env` | M2, M0 | ✅ |
| **M6** | `LunarWard.App` UI 骨架 | 窗口、游戏切换、启动按钮状态机 | M2 | ✅ |
| **M7** | Starward 风格 UI | 公告三源 + 动态背景 + 双主题 | M6 | ✅（动态背景本机回退静态图） |
| **M8** | `LunarWard.Installer` | 移植 `Starward.RPC/GameInstall`，去 gRPC/Vanara | M2 | ✅ 移植完成；UI 入口待接、真机下载未验证 |
| **M9** | Starward P0/P1 功能 | 抽卡、便笺、时长、截图、URL 协议 | M7, M8 | ⚠️ 时长 ✅；抽卡/便笺/截图/URL 未接 UI（Core 侧客户端已就绪） |
| **M10** | 打磨 | i18n、崩溃上报、导入向导、文档 | M9 | ⚠️ i18n ✅、文档 ✅；导入向导与崩溃上报按 Q10 不做 |

### 1.1 M0 的三个验证项

**任何一项失败都必须暂停并重新评估设计。**

#### V1 — Avalonia 能在本机跑起窗口

```bash
dotnet new console -o /tmp/avalonia-probe && cd /tmp/avalonia-probe
dotnet add package Avalonia --version 12.1.4
dotnet add package Avalonia.Desktop --version 12.1.4
dotnet add package Avalonia.Themes.Fluent --version 12.1.4
# 写一个 Application + Window，dotnet run
```

**成功标准**：窗口出现，Fluent 主题生效，Wayland/X11 均可，无 native 加载错误。

**同时验证**：`Avalonia.Themes.Fluent` 的控件覆盖率是否够用（对照 Starward 的
`TextBlock`/`StackPanel`/`Grid`/`Button`/`Image`/`Border` 需求）。

**失败时的退路**：若 Avalonia 12 在本机有问题，先试 `11.3.22`（仍在维护）；
若 11 也不行，才需要重新选型（回到 GTK4/GirCore，代价是丢掉 Fluent 观感对齐）。

#### V2 — 视频背景与终端控件

Avalonia 路线下这两个都从"风险"降级为"选包"，但仍需实测。

**V2a — 视频背景**

```bash
dotnet add package LibVLCSharp.Avalonia --version 3.10.2
```

**成功标准**：`VideoView` 能循环播放一个 mp4，静音，游戏启动时能 `Pause()`，
`EndReached` 回调能安全重播（注意必须 `Dispatcher.UIThread.Post` 回主线程）。

**前置**：本机需有 `libvlc`（Arch: `pacman -S vlc`）。**未装时**应能优雅回退静态图。

**V2b — 终端控件**

从 `Iciclecreek.Avalonia.Terminal` 4.1.0 / `RoyalApps.RoyalTerminal.Avalonia` 0.6.3 /
`AvaloniaTerminal` 1.0.0 中试 2 个，按 `04-FEATURES.md` §6.4 的四条标准选一个。

**成功标准**：能嵌入窗口、跑起 `bash`、能交互、窗口 resize 时子进程收到 `SIGWINCH`。

**失败时的退路**：自建 PTY（`openpty`） + 现成 VT 解析器；最差退化为只读日志查看器
+ 导出 `env.sh`。

#### V3 — Acrylic / 窗口模糊降级路径

Avalonia 无跨平台 backdrop blur API。需验证 `04-FEATURES.md` §5.6 的分级策略。

**成功标准**：
- 本机（KDE/CachyOS？需确认桌面环境）上 `TransparencyLevelHint` 的实际值是什么
- 半透明渐变层能达到可接受的 Fluent 观感
- 不支持复合时能降级为纯色且不崩溃

**本机需先确认桌面环境**：`echo $XDG_CURRENT_DESKTOP` / `$XDG_SESSION_TYPE`

#### V4 — `Starward.Core` 能在 Linux 编译

```bash
git subtree add --prefix=src/LunarWard.Core \
  https://github.com/Scighost/Starward.git main --squash
cd src/LunarWard.Core && dotnet build
```

**成功标准**：仅因 `Microsoft.Win32`（1 处）报错，做 §3.2 的最小改造后编译通过。

**失败时的退路**：若发现更多 Windows 依赖，评估是否值得继续；
极端情况下只移植 `Core` 的部分子目录（`Gacha`/`GameRecord`/`HoYoPlay` 优先）。

#### V5（顺带）— Steam VDF 库选型

见 `06-OPEN-QUESTIONS.md` Q2。M0 期间试 2–3 个候选，选定后记录。

---

## 二、测试策略

### 2.1 单元测试

项目：`tests/LunarWard.UnitTests`

| # | 测试 | 关键断言 |
|---|---|---|
| U1 | `config.ini` 四渠道往返 | `Parse(Generate(x)) == x`，且**逐字节**比对游戏原生格式 |
| U2 | `config.ini` 未知键保留 | 写入额外键 → 读 → 写 → 额外键仍在 |
| U3 | 作用域解析 | `PerGame > Shared > Default > System` 全组合（表驱动，16 种） |
| U4 | env 十层优先级 | 逐层覆盖，断言最终 `dict` 与 `EnvSources` 都正确 |
| U5 | **`EnvPatchKind` 三态矩阵** | 特别断言 `Override("")` ≠ `Remove`（结果 dict 里一个存在且为空，一个不存在） |
| U6 | 关键字替换 | `%build% %prefix% %game% %launcher% %temp% %exe% %channel% %runtime%` |
| U7 | 命令模板 | `%command% %bash_command% %windows_command% %launch_args% %post%` |
| U8 | `Trigger × ProgramKind` 合法性 | `Trigger` 仅 `Auto`/`Manual`；`Auto` 必须在 `AwaitReady` 之后触发（断言启动时序） |
| U9 | prefix 客户匹配 | 给定合成 `/proc` 快照（临时目录模拟），正确识别本 prefix 客户、排除僵尸、排除其他 prefix |
| U10 | B 站前置校验 | 四种组合（非国服/无 DLL/空 DLL/正常）各返回预期错误 |
| U11 | zip 条目 GBK 匹配 | 用真实字节序列构造条目名，验证前缀 `64` + 后缀匹配 |
| U12 | 配置原子写 | 模拟写失败 → 原文件不变 |
| U13 | 配置损坏恢复 | 写入非法 JSON → 备份为 `.bad-*` → 重建默认 |

**U9 的实现提示**：不要真的读 `/proc`。把 `IProcessTree` 的实现做成可注入的，
测试时用内存数据结构。

### 2.2 集成测试（不需要游戏本体）

项目：`tests/LunarWard.IntegrationTests`

**思路**（照搬 `ZZZ-touch-unlocker/wine/zzz-touch-selftest.sh` 的路子）：
用 MinGW 交叉编译一个**合成"游戏"**（一个会 sleep 若干秒的 Windows exe），
配一个临时 wine prefix，验证相位引擎的完整流程。

```bash
# 本机已具备：x86_64-w64-mingw32-g++ 16.2.0
# 需要：一个可用的 wine（从 ~/.local/share/sleepy-launcher/runners/ 借用，或系统 wine）
```

| # | 测试 | 验证点 |
|---|---|---|
| I1 | 清理 | 启动前插入假 wine 客户端 → `Prepare` 后 `prefix_clients == []` |
| I2 | 首个客户 | 合成游戏是 `Launch` 后**第一个**连上 wineserver 的进程 |
| I3 | `AwaitImage` | 能通过 `/proc/<pid>/maps` 找到合成游戏 |
| I4 | `AwaitReady` | 三个信号各能触发（分别构造） |
| I5 | `Trigger.Auto` 时机 | 外部程序在 `AwaitReady` **之后**才启动 |
| I6 | 第二个客户 | 外部程序确实是该 prefix 的第二个客户端 |
| I7 | `Trigger.Manual` | **不会**自动启动 |
| I8 | `Enabled=false` | 即使 `Trigger.Auto` 也不启动 |
| I9 | 三态 env | 外部程序进程的 `/proc/<pid>/environ` 与预期 dict 完全一致 |
| I10 | 游戏退出收敛 | 游戏退出后 `prefix_clients` 在超时内变空 |
| I11 | `OnGameExit.Kill` | 游戏退出后外部程序被终止 |
| I12 | dry-run | 只打印计划，不改任何文件、不启动任何进程 |
| I13 | 启动报告 | `launch-report.json` 包含全部七相位记录 |

**I11 的关键**：注入器这类程序会 `WaitForSingleObject` 等游戏退出，所以它可能
比游戏晚一点点退出。`Kill` 策略要处理"已经自己退了"的情况（`HasExited` 检查）。

### 2.3 环境 shell 测试

| # | 测试 | 验证点 |
|---|---|---|
| S1 | env 一致 | shell 进程的 `/proc/<pid>/environ` 与 `launch-report.json` 的 `env` 逐键相等 |
| S2 | cwd | shell 的 `/proc/<pid>/cwd` == 游戏目录 |
| S3 | 可跑 wine | 在 shell 里执行 `wine --version` 成功 |
| S4 | 关闭无残留 | 关标签后该 shell 进程树全部退出 |
| S5 | 启动器退出 | 启动器退出时回收所有 shell |

### 2.4 真机清单（人工，需游戏本体）

每项都要记录实际结果到 `docs/acceptance-<date>.md`。

| # | 场景 | 通过标准 |
|---|---|---|
| R1 | 官方国服启动 | 进入登录界面，`launch-report.json` 显示 `signal: WineDevice` |
| R2 | 切 B 站 | 校验 DLL → 启动 → 能进 B 站账号登录 |
| R3 | B 站缺 DLL | **拒绝启动**，且给出下载按钮 |
| R4 | 添加一个 `Trigger.Auto` 注入器 | 游戏正常启动 + 注入生效（看注入器自己的日志） |
| R5 | 添加一个 `Trigger.Manual` 程序 | 不自动启动；点"立即启动"能起来 |
| R6 | `Enabled=false` | 既不自动也不提示 |
| R7 | 三态 env 生效 | 外部程序里 `echo $VAR` 结果符合预期 |
| R8 | 系统 wine | 用 `System` 来源能启动 |
| R9 | 托管 wine | 用 `Managed` 来源能启动 |
| R10 | Proton（WineBinary） | 用 Steam Proton 的 `dist/bin/wine` 能启动 |
| R11 | 沙箱 | 开启 sandbox 后 R4/R5 仍工作 |
| R12 | 退出无残留 | 游戏退出后 `pgrep -af wineserver` 无残留 |
| R13 | 动态背景 | 视频循环播放（`libvlc` 已装）；卸载后能回退静态图 |
| R14 | 环境 shell | 打开就能 `wine regedit` |
| R15 | 崩溃恢复 | 强杀游戏 → 下次启动能清理并正常起来 |

**R1/R2/R4 是核心**——这三项不通过，项目没有交付价值。

---

## 三、Starward 功能迁移矩阵

按"能否复用 `Starward.Core`"排序。

### 3.1 直接复用 Core（视图层重写）

| 功能 | Core 位置 | 文件数 | 优先级 | 视图工作量 |
|---|---|---|---|---|
| 抽卡记录 | `Core/Gacha` | 28 | P0 | 中（列表 + 统计图表） |
| UIGF 4.x 导入导出 | `Core/Gacha/UIGF` | — | P0 | 小（文件选择 + 进度） |
| 便笺 DailyNote | `Core/GameRecord` | 154 | P0 | 中（卡片布局） |
| 深渊/虚构/危局等战绩 | `Core/GameRecord/*` | — | P1 | 中 |
| 公告 | `Core/GameNotice` | 3 | P0 | 小 |
| 版本亮点/背景 | `Core/HoYoPlay` | 18 | P0 | 中（轮播） |
| 抽卡查询辅助 | `Core/SelfQuery` | 11 | P1 | 小 |
| 元数据（更新检查） | `Core/Metadata` | 8 | P2 | 小 |

### 3.2 移植后改造

| 功能 | 来源 | 改造点 | 优先级 |
|---|---|---|---|
| 安装/更新/修复/校验 | `RPC/GameInstall` | 去 gRPC、去 Vanara | P0 |
| 区服切换 | `Features/GameLauncher/GameConfigIni` | 原样搬 | P0 |
| 启动参数/env | `Features/GameLauncher/GameLauncherService` | 改写为相位引擎 | P0（本项目核心） |

### 3.3 重写

| 功能 | 原实现 | 新实现 | 优先级 |
|---|---|---|---|
| 账号切换 | registry 操作 | 切 Wine `user.reg` 的 `[Software\miHoYo\...]` 段 | P0 |
| 动态背景 | `Features/Background`（Win2D） | `LibVLCSharp.Avalonia` 的 `VideoView` | P0 |
| 游戏时长 | `Features/PlayTime` | 复用相位引擎的起止时间 → ✅ 已实现（`LunarWard.Launch/PlayTimeService.cs`） | P1 |
| 截图管理 | `Features/Screenshot`（WIC） | ✅ 目录发现 + 文件操作（`LunarWard.Launch/ScreenshotService.cs`），不做图像解码 | P1 |
| 画质设置 | `Features/GameSetting`（registry blob） | Wine `user.reg` 的 hex（**先只读**） | P1 |
| URL Protocol | `Features/UrlProtocol`（注册表） | `.desktop` + `MimeType` | P1 |
| 设置页 | `Features/Setting`（WinUI） | `Adw.PreferencesPage` | P1 |
| Discord RPC | `Features/RPC` | IPC socket | P2 |
| 通知 | `H.NotifyIcon` | `notify-rust` 等价物（D-Bus） | P2 |
| 自更新 | `Features/Update` | AppImage/Flatpak 外部处理 | P2 |

### 3.4 不做

| 功能 | 原因 |
|---|---|
| `Features/Overlay`（游戏内覆盖） | 需 Vulkan layer / X11 overlay，与 Wine 兼容性差 |
| `Features/GamepadControl` | 手柄控制启动器，Linux 上价值低 |
| `Features/ViewHost` | WinUI 特有 |
| `Features/Codec` | 依赖 Windows 媒体栈 |
| `Features/CloudGame` | 区域限制，价值低 |
| 插件系统 | 用"外部程序"替代，不做 in-process 插件 |

---

## 四、交付前检查清单

> 状态更新（2026-10-23）。✅ = 已完成并有证据；⏳ = 待真机/后续。

| 检查项 | 状态 | 证据 |
|---|---|---|
| `dotnet build` 无警告 | ✅ | `dotnet build LunarWard.slnx` → 0 warning / 0 error |
| 全部单元测试通过 | ✅ | 145（Unit）+ 31（Installer） |
| 集成测试 I1–I13 通过 | ✅ | 25 个集成测试通过（合成游戏 + 真 `/proc`，不需要真 wine） |
| 真机清单 R1–R15 全部记录 | ⚠️ | 已记录，但仅 R9/R12 ✅、R13/R15 ⚠️、其余 ⏳ → `acceptance-2026-10-23.md` |
| `launch-report.json` 能正确输出 | ✅ | `I13` / `Report_HasSevenPhasesAndFullChain` |
| 配置损坏时能恢复 | ✅ | `U13` / `PlayTimeTests.CorruptStore_IsBackedUpAndReset` |
| 崩溃时不留下 wine 进程残留 | ✅ | `I10` / `R12_NoResidualWineClientsAfterExit` / `R15_...AreCleanedOnNextLaunch` |
| i18n：至少 zh-CN + en-US | ✅ | `App/Localization/AppStrings.cs`（zh-CN + en-US，键缺失时回显键名）；Core 另有 13 种语言资源 |
| 文档：README、用户手册、排障指南 | ✅ | `README.md` / `docs/USER-GUIDE.md` / `docs/TROUBLESHOOTING.md` |
| 许可文件与第三方声明 | ✅ | `LICENSE` / `THIRD-PARTY-NOTICES.md` |
| **不包含**任何补丁二进制 | ✅ | 仓库内无 `.exe`/`.dll` 补丁；`git ls-files` 可核 |
| `.desktop` 文件 + 图标 | ✅ | `packaging/lunarward.desktop`（`desktop-file-validate` 通过）+ `Assets/lunarward.ico` |
| Flatpak 或 AppImage 打包 | ⏳ | 首期只做源码 + Arch（`packaging/PKGBUILD`），Flatpak 后续（Q8） |

未关闭的 2 项：R1–R8/R10/R11/R14 需游戏本体；Flatpak（首期只做源码 + Arch）。

---

## 五、风险登记

| 风险 | 概率 | 影响 | 缓解 |
|---|---|---|---|
| `Starward.Core` 移植发现更多 Windows 依赖 | 中 | 高 | M0 的 V4 提前暴露；必要时只移植子目录 |
| **XAML → Avalonia 改写量（21,508 行）超预期** | **高** | 中 | ViewModel/Service 保留；按功能分批交付；`{x:Bind}` → `CompiledBinding` 可脚本化辅助 |
| **Avalonia 12 尚新**（2026 新大版本） | 中 | 中 | M0 的 V1；必要时退到 `11.3.22` |
| **Acrylic/模糊在 Linux 无法真实现** | **高** | 低 | 分级降级（L1 真模糊 / L2 半透明 / L3 纯色），见 `04-FEATURES.md` §5.6 |
| 终端控件不满足 PTY / `SIGWINCH` 要求 | 中 | 中 | M0 的 V2b；退路：自建 PTY + 现成 VT 解析器 |
| `libvlc` 在用户机器上缺失 | 中 | 低 | 软探测 → 回退静态图 + 提示安装 |
| Proton `ProtonScript` 破坏不变式 | 中 | 中 | 默认 `WineBinary`；UI 标注实验性 |
| 游戏/补丁更新导致特征码失配 | 高 | 低 | 补丁外部化，本项目不感知 |
| 组件索引（an-anime-team）停止维护 | 低 | 中 | 用户可手动放索引或改用 System/Custom |
| 画质设置 blob 解析困难 | 高 | 低 | P1 只读；写操作长期不做 |
