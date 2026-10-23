# M0 技术验证结果

**日期**：2026-10-23
**环境**：CachyOS Linux / KDE Wayland / `dotnet 10.0.302`

M0 是唯一的前置门禁（`docs/05-ROADMAP.md` §1.1）：验证四个未证实的技术点。
本文件记录**真实实测数据**，不是设计推演。

**结论：M0 通过。** 四项中 V1/V3/V4/V5 全部通过，V2a 按设计降级（不阻塞），
V2b 通过。所有降级路径都已在代码中落地且不崩溃。

---

## 一、总表

| 项 | 结论 |
|---|---|
| **V1** Avalonia 12.1.4 窗口 | ✅ **通过**。本机 KDE Wayland，`ActualTransparencyLevel=Transparent`，FluentTheme 生效 |
| **V2a** 视频背景 | ⚠️ **降级**。`libvlc 3.0.24` 已装，但 `libvlc_new` 返回 `NULL`（Arch 上 `libvlc` 与 VLC 插件包是分开的），故 `BackgroundService.IsVlcAvailable == false` → **按设计回退静态图**，不崩溃 |
| **V2b** 终端控件 | ✅ `Iciclecreek.Avalonia.Terminal 4.1.0`（依赖 Avalonia 12.0.4，与 12.1.4 二进制兼容）。API：`TerminalView.LaunchProcess(dir, shell, args)` / `EnvironmentVariables` / `StartingDirectory` / `SendInputAsync` / `Kill()` |
| **V3** Acrylic | ✅ L2 半透明 + L3 纯色落地，`ActualTransparencyLevel=Transparent` |
| **V4** `Starward.Core` 编译 | ✅ 236 个 `.cs`，Linux `0 error`。唯一 Windows 依赖 `Registry.GetValue` 已抽成 `IGameInstallPathProvider` |
| **V5** Steam VDF | ✅ 本机 `~/.steam/steam/steamapps/libraryfolders.vdf` 存在。按"退化方案"不引入 VDF 库，只抓 `"path"` 行 + 标准路径扫描 `steamapps/common/*/dist/bin/wine` |
| **工具链** | `dotnet 10.0.302` / CachyOS / KDE Wayland / mingw `16.2.0` / 7z `26.04` |

图例：✅ 通过　⚠️ 降级但可用　❌ 失败（需回到设计阶段）

---

## 二、逐项记录

### 2.1 V1 —— Avalonia 能在本机跑起窗口

| 检查项 | 实测 |
|---|---|
| 包版本 | `Avalonia` / `Avalonia.Desktop` / `Avalonia.Themes.Fluent` = **12.1.4** |
| 窗口是否出现 | ✅ |
| FluentTheme 是否生效 | ✅ |
| 会话类型 | `XDG_SESSION_TYPE=wayland`，`XDG_CURRENT_DESKTOP=KDE` |
| `ActualTransparencyLevel` | **`Transparent`** |
| native 加载错误 | 无 |

**判定：通过。** Avalonia 12 在这台机器上不需要退到 `11.3.22`。

`ActualTransparencyLevel=Transparent` 这一条同时是 V3 的输入：说明合成器支持透明，
可以走 L2（半透明层），不必退到 L3（纯色）。

### 2.2 V2a —— 视频背景（降级）

**实测事实**：

```bash
$ pacman -Q libvlc
libvlc 3.0.24-1.1
$ ls /usr/lib/libvlc.so*
/usr/lib/libvlc.so  /usr/lib/libvlc.so.5  /usr/lib/libvlc.so.5.6.1
$ pacman -Q vlc
error: 未找到软件包 'vlc'      # ← 关键：插件包没装
```

`libvlc_new()` 返回 **`NULL`**。

**根因**：**Arch 上 `libvlc` 与 VLC 的插件包是分开的两个包。**
只有 `libvlc.so` 而没有 codec 插件时，`libvlc_new()` 直接失败。
这与 "库文件存在就能用" 的直觉相反，是本机最容易踩的坑。

**代码行为（已落地，见 `src/LunarWard.App/Services/BackgroundService.cs`）**：

```
BackgroundService.ProbeVlc(out error)
  → Core.Initialize() + new LibVLC("--no-audio", "--quiet")
  → 抛异常 → 返回 false，error 存进 ProbeError
IsVlcAvailable == false
  → EnsurePlayer() 返回 null
  → 上层回退静态图（Image + Stretch="UniformToFill"）
```

**判定：⚠️ 降级，符合设计。** `docs/04-FEATURES.md` §5.4 与
`docs/05-ROADMAP.md` §五风险表都明确要求"探测失败 → 回退静态图 + UI 提示安装命令，**不崩溃**"。
本机正是这条路径的真实演练：**没有崩溃，没有异常冒泡，静态图正常显示。**

**这不是 M0 失败**：V2a 的验收标准是"能循环播放 + 能优雅回退"，
后半条已实测；前半条需要装 `vlc` 完整包（`pacman -S vlc`），属于环境问题而非技术风险。
真机播放验证记录在 `docs/acceptance-2026-10-23.md` 的 R13（状态：⚠️ 部分）。

### 2.3 V2b —— 终端控件

| 项 | 实测 |
|---|---|
| 选中包 | **`Iciclecreek.Avalonia.Terminal` 4.1.0** |
| 其依赖的 Avalonia | **12.0.4** |
| 与本项目 Avalonia 12.1.4 是否兼容 | ✅ **二进制兼容**，NuGet 解析无冲突，运行时无 `TypeLoadException` |
| 许可 | MIT |

**可用 API**（已核对）：

| API | 用途 |
|---|---|
| `TerminalView.LaunchProcess(dir, shell, args)` | 在指定目录拉起 shell |
| `TerminalView.EnvironmentVariables` | **注入 `LaunchPlan.Env`** ← 环境 shell 的关键 |
| `TerminalView.StartingDirectory` | 设为游戏目录 |
| `TerminalView.SendInputAsync(...)` | 命令预设按钮（`winecfg` 等） |
| `TerminalView.Kill()` | 关标签时回收 |

**传递依赖**（读自 nuspec，全部为本机可用的 Linux 版本）：
`Porta.Pty 2.2.3`（Linux 走 `openpty`）、`XTerm.NET 3.0.0`（VT 解析）、
`SkiaSharp 3.119.4` + `SkiaSharp.HarfBuzz`（字形）、`Wcwidth 4.0.1`、
`Unicode.net 2.0.0`。

**判定：通过。** `04-FEATURES.md` §6.4 的四条选型标准（原生 `openpty` / 256 色 /
`SIGWINCH` / 维护活跃）由 `Porta.Pty` + `XTerm.NET` 满足。
Avalonia 12.0.4 → 12.1.4 的次版本差异没有造成任何 API 断裂。

### 2.4 V3 —— Acrylic 分级

| 级别 | 实现 | 本机结果 |
|---|---|---|
| **L2** 半透明 | `Window.TransparencyLevelHint="Transparent"` + 半透明渐变层 `Border` | ✅ **生效**，`ActualTransparencyLevel=Transparent` |
| **L3** 纯色 | `TransparencyBackgroundFallback = SolidBrush`，不透明明暗背景 | ✅ 落地（作为兜底路径存在） |
| L1 真模糊 | KDE `_KDE_NET_WM_BLUR_BEHIND_REGION` | ❌ **按决策不做**（`04-FEATURES.md` §5.6） |

本机 GUI 栈：KDE Wayland，合成正常，因此探测落到 L2。
`ThemeShadow` → `DropShadowEffect`（`BlurRadius=24`，见 `Styles/LunarWard.axaml`）。

**判定：通过。** 分级策略的两条路径都已落地，且 L2 是实测生效的那个。

### 2.5 V4 —— `Starward.Core` 在 Linux 编译

| 项 | 实测 |
|---|---|
| 源文件数 | **236 个 `.cs`**（上游原样） |
| 目标框架 | `net10.0`（**非** `net10.0-windows`） |
| NuGet 依赖 | **零**（纯 BCL） |
| Linux 编译结果 | ✅ **0 error** |
| 唯一 Windows 依赖 | `Gacha/GachaLogClient.cs:299` 的 `Registry.GetValue(...)` |

**改造（仅此三处，与 `docs/00-DECISIONS.md` D2 一致）**：

1. `Registry.GetValue(...)` → 抽出 `IGameInstallPathProvider` 接口注入；
2. 移除 `using Microsoft.Win32;`；
3. 其余**一行不改**。

新增的 `src/LunarWard.Core/IGameInstallPathProvider.cs` 使目录里实际有 **237 个 `.cs`**
（236 上游 + 1 新增接口）。这是**新增**文件，不是修改，符合 D2 的"三处最小改造"。

**判定：通过。** 这是"不重复造轮子"的最大兑现点：抽卡（28 文件）、便笺/战绩
（154）、HoYoPlay（18）、SelfQuery（11）、公告（3）、元数据（8）、JsonConverter（5）
全部原样继承。

### 2.6 V5 —— Steam VDF 库选型

| 项 | 实测 |
|---|---|
| 本机 VDF 文件 | ✅ `~/.steam/steam/steamapps/libraryfolders.vdf` 存在 |
| 方案 | **不引入 VDF 库**（`docs/06-OPEN-QUESTIONS.md` Q2 的"退化方案"） |
| 解析方式 | 只抓 `"path"  "<dir>"` 行 + 标准路径扫描 |
| 扫描目标 | `<lib>/steamapps/common/*/dist/bin/wine` |

实现位置：`src/LunarWard.App/Services/GameScanner.cs` 的 `RuntimeScanner.SteamLibraries()`。

**为什么够用**：`libraryfolders.vdf` 的新旧两种格式都在**同一行**里给 `path`，
逐行抓字符串就能覆盖，不需要完整的递归 VDF 解析器。而 Proton 的识别标准
是"目录下有 `dist/bin/wine`"，这也只是文件系统检查，与 VDF 语法无关。

**判定：通过（选型结论：不引库）。** 代价是若 Valve 将来把 `path` 拆到多行，
需要改这一处；收益是少一个依赖、少一类解析失败模式。

---

## 三、M0 后的偏差记录

M0 验证出四个**设计与现实不符**的点，全部已按下列方式修正。
这些偏差影响 `docs/02-ARCHITECTURE.md` 的选型表，需一并知悉。

### 3.1 `Snap.HPatch` 的 nupkg 只有 `runtimes/win-x64/native`

| 项 | 内容 |
|---|---|
| **原计划** | 用 `Snap.HPatch`（托管封装，内置 native）做差量补丁 |
| **实际问题** | 该包 0.0.2 的 nupkg 里**只有 `runtimes/win-x64/native`**，**没有 linux-x64**。Linux 上加载即失败 |
| **修正** | 改用**外部 `hpatchz` CLI**，实现于 `src/LunarWard.Installer/DiffPatcher.cs` |
| **查找顺序** | `LUNARWARD_HPATCHZ` 环境变量 → `hpatchz` → `hpatch` |
| **缺失时** | 记录 `Patch file failed` 警告，该文件走全量下载（**不中断整个更新**） |
| **用户可见影响** | 需要额外装 `hdiffpatch` 包（Arch: `pacman -S hdiffpatch`） |

> `Snap.HPatch` 仍是 `Directory.Packages.props` 里的一个 `PackageVersion`，
> 但**不再被任何项目引用**，实际不参与构建。

### 3.2 `SharpSevenZip` 的 native 只有 Windows

| 项 | 内容 |
|---|---|
| **原计划** | 用 `SharpSevenZip`（内置 7z.dll 的托管壳）解包 |
| **实际问题** | 其 native 资产**只有 Windows**（`7z.dll`），Linux 无对应物 |
| **修正** | 改用**外部 7z / BCL `ZipFile`** 的两级回退，实现于 `src/LunarWard.Installer/ArchiveExtractor.cs` |
| **一级** | 外部可执行：`7zz` → `7z` → `7za`（或 `LUNARWARD_7Z` 指定）；支持分卷 `.7z.001` |
| **二级** | 若压缩包是**普通 `.zip`** 且没有 7z → BCL `ZipFile.ExtractToDirectory` |
| **都不可用** | 抛 `InstallerCapabilityException`，消息里写明缺什么、怎么装 |
| **用户可见影响** | 需要装 `7zip` 包（Arch: `pacman -S 7zip`）；纯 zip 无需 |

### 3.3 gRPC / protobuf 全部删除，改为进程内 POCO

| 项 | 内容 |
|---|---|
| **原计划** | 移植 `Starward.RPC/GameInstall` 时保留 gRPC 层 |
| **修正** | **全部删除** `Grpc.AspNetCore` + `.proto` + `GameInstallController`，改进程内直接调用 |
| **消息模型** | 改为 POCO：`src/LunarWard.Installer/InstallMessages.cs` |
| **进度上报** | gRPC 流 → `IProgress<GameInstallContext>` |
| **宿主** | `WebApplication` → 普通类，无宿主 |
| **理由** | 本项目是**单进程桌面应用**，不存在跨进程 RPC 需求。gRPC 只会增加一层序列化开销、一堆不能跨平台的原生依赖、以及一个必须维护的 `.proto` 契约 |

> 同时移除了 `Vanara.PInvoke.Kernel32`（`DriveHelper` 改用 `DriveInfo`）
> 与 `Starward.NativeLib`（只用于 Windows API）。详见 `docs/02-ARCHITECTURE.md` §4.2。

### 3.4 托管组件下载：M0 时未实现，**现已实现**（不引 git）

| 项 | 内容 |
|---|---|
| **原计划** | 从 an-anime-team 组件索引（`https://github.com/an-anime-team/components`）用 `git` 同步，进而下载托管 wine / dxvk 组件 |
| **M0 时状态** | ❌ 未实现。`ComponentRepository` 只读 `<data>/components/` 下已有的 JSON |
| **现在状态** | ✅ 已实现：`LunarWard.Wine/ComponentInstaller.cs` |
| **与计划的一处偏差** | **不引入 git**：直接下载 `components.json` 与分组 JSON（`raw.githubusercontent.com`），语义等价、零外部依赖（没装 git 的用户也能用） |
| **安装安全性** | 下载 → `.part` → rename；解压 → `<dest>.tmp` → rename。**绝不产出半个组件**；失败保留 `.part` 供续传 |
| **解压** | `.tar.xz` / `.tar.gz` / `.tar.zst` 走系统 `tar`（POSIX base，所有发行版都有）；`.zip` 走 BCL；无 `tar` 时退回 BCL `TarReader`（仅纯 `.tar`） |
| **测试** | `ComponentInstallerTests`（6 个，真实 `HttpListener` + 真实 `tar.gz`；HTTP 失败时断言不留残骸） |
| **仍未覆盖** | 真实 an-anime-team 索引的端到端下载（需要几百 MB 与网络），留待真机验收 |

### 3.5 实现期间发现的三个"只有真跑才会暴露"的缺陷

这三条都不是设计错误，而是**M0 的小规模验证覆盖不到、只有在完整实现 + 测试时才浮现**的问题。
记录在此，供后续类似项目参考。

| # | 缺陷 | 表现 | 根因 | 定位方式 |
|---|---|---|---|---|
| 1 | 进程组信号被拒绝 | 环境 shell 关标签后子进程残留；**静默失败** | `Signal()` 里 `if (pid <= 0) return false` 把 `kill(2)` 的"负 pid = 进程组"语义挡掉了，且返回值无人检查 | 集成测试 `S4` 间歇失败 |
| 2 | 启动按钮渲染为灰色 | 属性、绑定、命令状态**全部正确**，屏幕上是灰的 | Fluent 的 `ControlTheme` 自己管模板内 `ContentPresenter` 的背景，在 `Button` 上设 `Background` 不穿透 | **截图采样像素** |
| 3 | 首次建 prefix 卡 2 分半 | 每个新用户首次启动都会超时；留下半成品 prefix | `wineboot -u` 在冷 prefix 上弹「下载 wine-mono」并**阻塞等待输入** | 真 wine 集成测试耗时异常（3m25s） |

**共同点**：三条都不产生编译错误、不产生异常、日志里也没有"错误"字样。
它们分别由**集成测试、像素采样、耗时异常**发现——这正是"必须有真跑验证"的理由。

**修复与回归**：

| # | 修复 | 回归测试 |
|---|---|---|
| 1 | `Signal` 守卫改 `pid == 0`；新增 `TerminateGroup`；`setsid -w` | `S4_KillingShellLeavesNoOrphan`（10s → 98ms） |
| 2 | 改用 `/template/ ContentPresenter` 选择器 | `--screenshot` 自检 + 人工像素采样 |
| 3 | 合并 `WINEDLLOVERRIDES=mscoree,mshtml=`；超时 2→3 分钟；超时删半成品 | `RealWine_PhaseEngine_...`（断言冷 prefix < 30 秒） |

---

**这四条偏差的共同特征**：三个是"Windows 专属 native 依赖在 Linux 上不可用"，
一个是"跨进程 RPC 在单进程应用里是纯负担"。它们都不是设计错误，
而是**只有真机编译才会暴露的实现细节**——M0 的价值正在于此。
第四条（组件下载）在 M0 之后已经补上，且选择了比"引 git"更省的实现。

---

## 四、对决策的影响

| 决策 | M0 是否推翻了它 | 说明 |
|---|---|---|
| **D2**（复用 Starward.Core） | ❌ 未推翻，**强化** | 236 文件 0 error，只改三处 |
| **D3**（Avalonia） | ❌ 未推翻 | V1/V3 全部通过，不需要退 `11.3.22` |
| **D9**（Proton 默认 WineBinary） | ❌ 未推翻 | V5 顺带确认了 `dist/bin/wine` 路径存在 |
| **D12**（删独立下载器） | ❌ 未推翻 | 见 §3.3，gRPC 层确实是纯负担 |

**没有一项需要回到设计阶段。** M0 的门禁条件（`docs/05-ROADMAP.md` §1.1：
"任何一项失败都必须暂停并重新评估设计"）未被触发。

---

## 五、复现命令

```bash
# V1 / V3：跑窗口并截图自检（不需要人工观察）
dotnet run --project src/LunarWard.App -- --screenshot /tmp/lunarward-m0.png

# V4：Core 单独编译
dotnet build src/LunarWard.Core/LunarWard.Core.csproj -c Release

# V2a：探测 libvlc 是否真能用
pacman -Q libvlc vlc          # Arch：缺 vlc 时 libvlc_new 返回 NULL
ls /usr/lib/libvlc.so*

# V5：Steam 库发现
ls -la ~/.steam/steam/steamapps/libraryfolders.vdf

# 工具链
dotnet --version; x86_64-w64-mingw32-g++ --version | head -1; 7z | head -2
```
