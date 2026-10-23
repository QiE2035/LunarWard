# 02 — 架构与选型

## 一、技术栈

| 层 | 选型 | 版本 | 核验 |
|---|---|---|---|
| 语言/运行时 | C# / .NET | **10** | 本机 `dotnet 10.0.302` |
| UI 框架 | **Avalonia** | **12.1.4** | MIT，**2026-10-22 推送**，31,698★ |
| 主题 | `Avalonia.Themes.Fluent` | 12.1.4 | **Fluent ≈ Starward 的 WinUI 风格** |
| MVVM | `CommunityToolkit.Mvvm` | 8.4.2 | 与 Starward 一致，ViewModel 可搬 |
| 日志 | `Serilog.Extensions.Logging` + `Sinks.File` | 10.0.0 / 7.0.0 | 沿用 Starward |
| INI | `Microsoft.Extensions.Configuration.Ini` | 10.0.11 | Starward 已在用 |
| HTTP | `System.Net.Http` + `Polly` | 内建 / 8.7.0 | 沿用 Starward 的重试策略 |
| 解压 | `ZstdSharp.Port` | 0.8.8 | 沿用 Starward |
| 视频背景 | **`LibVLCSharp.Avalonia`** | 3.10.2 | 见 `04-FEATURES.md` §5.4 |
| 终端 | Avalonia 终端控件（待选） | — | 见 `06-OPEN-QUESTIONS.md` Q3 |
| Markdown | `Markdig` | 待核验 | 应用内公告渲染 |
| VDF（Steam） | 待核验 | — | 见 `06-OPEN-QUESTIONS.md` Q2 |

### 为什么是 Avalonia 而不是 GTK4/GirCore

**因为本项目的目标是复刻 Starward 的观感，而 Starward 就是 WinUI 3 + Fluent Design。**

实测 Starward 的 XAML：`AcrylicBrush` 92 次、`ThemeShadow` 62 次、
`CornerRadius` 371 次。**Avalonia 的默认主题正是 Fluent**，设计语言天然对齐；
GTK4/libadwaita 用的是 Adwaita 语言，与 Starward 根本不是一个风格。

社区与成熟度对比：

| | Avalonia | GirCore |
|---|---|---|
| Stars | **31,698** | 524（差 60 倍） |
| Forks | 2,863 | 54 |
| 最后推送 | **2026-10-22** | 2026-10-16 |
| 稳定版 | **12.1.4**（2026-10-09） | 0.8.1 |
| 历史版本数 | 163 | 22 |

**Avalonia 还消解了两个原本被标为风险的问题**：

| 问题 | GirCore 方案 | Avalonia 方案 |
|---|---|---|
| 环境 shell 的终端 | `GirCore.Vte-2.91` **在 NuGet 不存在**，需手写 ~300 行 `libvte` P/Invoke | `RoyalApps.RoyalTerminal.Avalonia` / `Iciclecreek.Avalonia.Terminal` 等现成控件 |
| 循环视频背景 | `gtk::MediaFile` 依赖 `gtk4paintablesink`（由独立包 `gst-plugin-gtk4` 提供），需软探测 + 回退 | `LibVLCSharp.Avalonia`，不依赖系统 GStreamer 插件 |

### 唯一 GirCore 占优的地方

**原生 GTK 观感**。如果项目目标是"融入 GNOME 桌面的原生应用"，GirCore 更合适。
但本项目是 Starward 的重实现，此条不适用。

### 保守选项

Avalonia **12 是 2026 年的新大版本**（`12.0.0` → `12.1.4` 已 12 个补丁版）。
若追求最大稳定性可用 `11.3.22`（仍在维护）。本项目倾向 12.1.4。

### 为什么不是 Rust

技术上 Rust + relm4 完全可行（本机工具链现成，`relm4 0.11` / `gtk4 0.11.5` /
`libadwaita 0.9.2` 均已核验可用），但会导致 **`Starward.Core` 的 236 个文件零复用**，
项目性质从"Starward 的 Linux 重实现"变成"用 Rust 从零写一个 Starward"。
与本项目定位冲突，已否决。

---

## 二、仓库结构

```
lunarward/
├── LunarWard.slnx
├── README.md
├── docs/                          # 本设计文档集
├── Directory.Build.props          # 统一 TargetFramework / Nullable / LangVersion
├── src/
│   ├── LunarWard.Core/            # ← Starward.Core 移植（见 §三）
│   ├── LunarWard.Installer/       # ← Starward.RPC/GameInstall 移植（见 §四）
│   ├── LunarWard.Wine/            # 运行时管理（新增）
│   ├── LunarWard.Launch/          # 相位引擎（新增，核心）
│   ├── LunarWard.Games/           # 游戏适配 + 区服 + 外部程序（新增）
│   ├── LunarWard.Platform/        # Linux 平台抽象（/proc、进程、锁、Xdg）
│   └── LunarWard.App/             # Avalonia 视图层
├── tests/
│   ├── LunarWard.UnitTests/
│   └── LunarWard.IntegrationTests/   # 合成游戏 + 临时 prefix，无需游戏本体
└── third_party/
    └── Starward/                  # git submodule（只读参考 + 移植来源）
```

### 解决方案文件格式：`.slnx`

**用 `LunarWard.slnx`（XML 格式），不用传统的 `.sln`。**

理由：
1. **Starward 自己就是 `Starward.slnx`**（见 `third_party/Starward/`），
   保持与上游一致的工程习惯
2. XML 可读可 diff，传统 `.sln` 是自定义文本格式，合并冲突难解
3. .NET 10 SDK 原生支持

**已实测验证**（本机 `dotnet 10.0.302`）：

```bash
$ dotnet new sln --format slnx -n Probe     # 生成 <Solution></Solution>
$ dotnet sln Probe.slnx add Lib/Lib.csproj  # 自动写入 <Project Path="..." />
# 结果：
# <Solution>
#   <Project Path="Lib/Lib.csproj" />
# </Solution>
```

创建与添加项目均正常，无需手写 XML。

参考 Starward.slnx 的实际结构：

```xml
<Solution>
  <Configurations>
    <Platform Name="Any CPU" />
    <Platform Name="x64" />
  </Configurations>
  <Folder Name="/Solution Items/">
    <File Path=".editorconfig" />
  </Folder>
  <Project Path="src/LunarWard.Core/LunarWard.Core.csproj" />
  <Project Path="src/LunarWard.Platform/LunarWard.Platform.csproj" />
  <Project Path="src/LunarWard.Wine/LunarWard.Wine.csproj" />
  <Project Path="src/LunarWard.Launch/LunarWard.Launch.csproj" />
  <Project Path="src/LunarWard.Games/LunarWard.Games.csproj" />
  <Project Path="src/LunarWard.Installer/LunarWard.Installer.csproj" />
  <Project Path="src/LunarWard.App/LunarWard.App.csproj" />
  <Project Path="tests/LunarWard.UnitTests/LunarWard.UnitTests.csproj" />
  <Project Path="tests/LunarWard.IntegrationTests/LunarWard.IntegrationTests.csproj" />
</Solution>
```

> 本项目只面向 Linux，不需要 Starward 那样的多平台（x86/x64/ARM64）矩阵，
> 因此 `Configurations` 只保留 `Any CPU` 即可。

### 依赖方向（严格单向，禁止反向）

```
App ──→ Games ──→ Launch ──→ Wine ──→ Platform
 │        │         │
 │        └─────────┴──→ Core
 └──→ Installer ──→ Core
```

- `Core`：纯 API/模型，**不依赖任何本项目其他项目**
- `Platform`：`/proc` 读取、进程控制、文件锁、XDG 路径。**唯一允许触碰 OS 细节的地方**
- `Wine`：runner/prefix/dxvk/sandbox 的解析与安装
- `Launch`：相位引擎
- `Games`：每游戏适配、区服、外部程序
- `App`：视图

---

## 三、`LunarWard.Core`：从 `Starward.Core` 移植

### 3.1 引入方式

**推荐用 `git subtree`**（而非 submodule），因为我们要就地打补丁：

```bash
git subtree add --prefix=src/LunarWard.Core \
  https://github.com/Scighost/Starward.git main --squash
```

理由：submodule 里改文件很别扭；vendored 后可以自由改，同时保留上游来源记录。

### 3.2 改造清单（全部）

| # | 文件 | 改动 |
|---|---|---|
| 1 | `Starward.Core.csproj` → `LunarWard.Core.csproj` | 重命名项目；`RootNamespace` 改 `LunarWard.Core`；`TargetFramework` 保持 `net10.0` |
| 2 | `Gacha/GachaLogClient.cs:299` | `Registry.GetValue(...)` → 通过 `IGameInstallPathProvider` 注入 |
| 3 | `Gacha/GachaLogClient.cs:1` | 移除 `using Microsoft.Win32;` |
| 4 | 全局 | `namespace Starward.Core.*` → `namespace LunarWard.Core.*` |

**第 2 项的接口设计**：

```csharp
namespace LunarWard.Core;

public interface IGameInstallPathProvider
{
    /// <summary>按 GameBiz 查询游戏安装路径；返回 null 表示未知。</summary>
    string? GetInstallPath(GameBiz gameBiz);
}
```

- Linux 实现：从 Wine prefix 的 `user.reg` 里读 `[Software\miHoYo\...]` 的 `InstallPath`
- 兜底实现：永远返回 `null`

### 3.3 保持不动的部分

`Gacha`（28 文件）、`GameRecord`（154）、`HoYoPlay`（18）、`Metadata`（8）、
`SelfQuery`（11）、`JsonConverter`（5）、`GameNotice`（3）、`Localization`（1）
**全部原样保留**。这是本项目的最大复用点。

### 3.4 移除清单

无。**不删任何功能**，即使本项目暂时不用（如 HSR/崩坏3 的模型），
保留可以降低移植成本、便于将来扩展。

---

## 四、`LunarWard.Installer`：从 `Starward.RPC` 移植

### 4.1 保留（纯 .NET，几乎不用改）

| 文件 | 作用 |
|---|---|
| `GameInstall/GameInstallService.cs` | 主流程、`Parallel.ForEachAsync` 并行下载 |
| `GameInstall/GameInstallHelper.cs` | chunk 下载、MD5 校验、硬链接去重 |
| `GameInstall/FileSliceStream.cs` | 分片流 |
| `GameInstall/FileCombinedStream.cs` | 组合流 |
| `GameInstall/DiffMap.cs` | 差量映射 |
| `GameInstall/GamePackageService.cs` | 包列表 |
| `GameInstall/GameInstallFile.cs` / `GameInstallContext.cs` / `GameInstallState.cs` / `GameInstallOperation.cs` / `GameInstallDownloadMode.cs` | 模型 |
| `GameInstall/PkgVersionItem.cs` / `PredownloadFile.cs` / `GameUninstallService.cs` | 模型与服务 |

### 4.2 改造

| 项 | 原样 | 改为 |
|---|---|---|
| gRPC 层 | `Grpc.AspNetCore` + `.proto` + `GameInstallController` | **删除**，改进程内直接调用 |
| `Vanara.PInvoke.Kernel32` | 用于磁盘/文件操作 | 换 `System.IO.DriveInfo` / 原生 BCL |
| `Starward.NativeLib` | 原生库 | 评估后决定：若只用于 Windows API 则删除 |
| `DriveHelper.cs` | 磁盘空间与驱动器枚举 | 用 `DriveInfo` 重写 |
| 进度上报 | gRPC 流 | `IProgress<GameInstallContext>` |
| 后台服务 | `Program.cs` 的 `WebApplication` | 普通类，无宿主 |

### 4.3 必须保留的行为

这些是 Starward 在实践中打磨出来的，**不能"简化"掉**：

1. `Parallel.ForEachAsync` 文件级并行（默认无上限）
2. `Polly` 重试：`WaitAndRetryAsync(3, i => TimeSpan.FromSeconds(i))`
3. `SocketsHttpHandler` 配置：
   ```csharp
   new SocketsHttpHandler {
       AutomaticDecompression = DecompressionMethods.All,
       PooledConnectionLifetime = TimeSpan.FromMinutes(5),
       EnableMultipleHttp2Connections = true,
       EnableMultipleHttp3Connections = true,
   }
   ```
4. `HardLinkAsync`：同盘硬链接，避免重复下载
5. `CheckFileMD5Async`：文件已存在且 MD5 匹配则跳过
6. `HttpClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher`
7. chunk 缓存目录 `<InstallPath>/chunk/<chunk.Id>`
8. 断点续传：按 `fs.Length < chunk.Offset + chunk.UncompressedSize` 判断未完成

---

## 五、`LunarWard.Platform`：Linux 平台层

这是**唯一**允许直接读 `/proc` 或调系统调用的项目。

```csharp
public interface IProcessTree
{
    /// <summary>列出所有 WINEPREFIX == prefix 的进程（排除僵尸与自身）</summary>
    IReadOnlyList<ProcessInfo> GetPrefixClients(string prefix);

    /// <summary>在上述进程中找已映射指定镜像的</summary>
    ProcessInfo? FindByImage(string prefix, string imageName);

    /// <summary>按名字找进程（用于 winedevice.exe 检测）</summary>
    IReadOnlyList<ProcessInfo> FindByName(string name);

    /// <summary>读取进程的启动时间，用于与 launch epoch 比较</summary>
    DateTime? GetStartTime(int pid);
}

public sealed record ProcessInfo(int Pid, string Comm, string? ImageName, ProcessState State);
public enum ProcessState { Running, Zombie, Dead }
```

### 5.1 实现要点（来自 `01-RESEARCH.md` §1.6）

**判定"是本 prefix 的客户"**：
```
读 /proc/<pid>/environ（NUL 分隔）
  → 任一条目等于 "WINEPREFIX=<prefix>"
  → 且 state != "Z"
  → 且 pid != Environment.ProcessId
```

**判定"游戏已加载"**：
```
读 /proc/<pid>/maps
  → 任一行包含 <imageName>
```

**必须按 maps 而非 comm**：steam stub 会把 unix `comm` 固定成 `steam.exe`，
真游戏是它 fork 的子进程。

**权限**：只有当前用户拥有的进程才能读 `environ`。用 `[ -O ]` 等价判断
（`/proc/<pid>` 的 owner uid == 当前 uid）。

### 5.2 其他平台服务

```csharp
public interface IFileLock : IDisposable;         // 进程级锁（flock）
public interface IXdgPaths { string DataHome, CacheHome, ConfigHome; }
public interface ITerminalLauncher { ... }        // 环境 shell，见 04-FEATURES
```

---

## 六、配置文件格式

### 6.1 位置

遵循 XDG：
- 配置：`$XDG_CONFIG_HOME/lunarward/config.json`（默认 `~/.config/lunarward/`）
- 数据：`$XDG_DATA_HOME/lunarward/`（默认 `~/.local/share/lunarward/`）
  —— prefix、runners、dxvks、components
- 缓存：`$XDG_CACHE_HOME/lunarward/`（默认 `~/.cache/lunarward/`）
  —— 背景、日志、缩略图

### 6.2 结构

```jsonc
{
  "schema_version": 1,

  "launcher": {
    "language": "zh-cn",
    "behavior": "Hide",                 // Nothing | Hide | Close（沿用 Starward）
    "style": "Modern",                  // Modern | Classic
    "video_background": true,
    "background_index": 0,
    "default_runtime": "shared-cachyos"
  },

  "runtimes": {
    "shared-cachyos": {
      "runner": { "kind": "Managed", "component": "spritz-wine-cachyos-wow64-10.0-11" },
      "dxvk":   { "kind": "Managed", "component": "dxvk-3.1.1" },
      "sync": "NTSync",                 // NTSync | ESync | FSync | None
      "env": {}
    },
    "native": {
      "runner": { "kind": "System" },
      "dxvk":   { "kind": "System" }
    }
  },

  "profiles": {
    "genshin-cn": {
      "game": "genshin",                // genshin | zzz
      "edition": "China",               // China | Global
      "path": "~/games/YuanShen",
      "channel": "Bilibili",            // Official | Bilibili（仅 genshin China）

      "prefix": { "kind": "Own", "path": "~/.local/share/lunarward/prefixes/genshin-cn" },
      "runtime": { "kind": "Shared", "name": "shared-cachyos" },

      "wine": {
        "sync": "NTSync",
        "language": "System",
        "borderless": false,
        "winewayland": false,
        "timeoutFix": false
      },
      "enhancements": {
        "fsr": { "enabled": false },     // ← 注意：默认 false（见 04-FEATURES §5）
        "hud": "None",
        "dx12": false
      },
      "sandbox": { "enabled": false, "isolateHome": true },

      "args": [],
      "env": {},
      "command_override": null,

      "external_programs": [
        {
          "id": "prog-1",
          "name": "（用户自定义）",
          "enabled": true,
          "kind": "WindowsExe",
          "path": "Z:\\home\\...\\something.exe",
          "args": ["--foo"],
          "working_directory": null,
          "env": [
            { "key": "SOME_VAR", "kind": "Override", "value": "x" },
            { "key": "UNWANTED",  "kind": "Remove"   }
          ],
          "trigger": "Auto",
          "wait": false,
          "on_game_exit": "Kill"
        }
      ]
    }
  }
}
```

### 6.3 写入规则

- **原子写**：写 `config.json.tmp` → `fsync` → `rename`
- **损坏处理**：解析失败 → 备份为 `config.json.bad-<unix-ts>` → 重建默认
- **schema_version**：用于将来迁移
- **未知字段保留**：用 `JsonExtensionData` 承接，避免旧版本启动器抹掉新版本写的字段

---

## 七、错误模型

```csharp
public abstract record LaunchError
{
    public sealed record PrefixNotClean(IReadOnlyList<int> Pids) : LaunchError;
    public sealed record GameNotInstalled(string Path) : LaunchError;
    public sealed record RuntimeNotFound(string Name) : LaunchError;
    public sealed record ChannelPrerequisiteMissing(string Detail) : LaunchError;  // 如 PCGameSDK.dll
    public sealed record GameExitedDuringStartup(int ExitCode, string? LogPath) : LaunchError;
    public sealed record InjectorBinaryMissing(string Path) : LaunchError;
    public sealed record ExternalProgramFailed(string Id, int ExitCode) : LaunchError;
}
```

UI 层按类型给出不同的可交互提示：
- `PrefixNotClean` → 给"清理"按钮
- `ChannelPrerequisiteMissing` → 给"下载 PCGameSDK.dll"按钮
- `GameExitedDuringStartup` → 给"查看日志"按钮 + 解释保护期

---

## 八、日志

- 位置：`$XDG_CACHE_HOME/lunarward/logs/lunarward-<date>.log`
- 游戏 stdout/stderr：`$XDG_CACHE_HOME/lunarward/logs/game.log`（上限 8 MiB）
- **启动报告**：`$XDG_DATA_HOME/lunarward/launch-report.json`

启动报告是排障的核心，记录完整个解析链条：

```jsonc
{
  "timestamp": "2026-10-23T12:00:00+08:00",
  "profile": "genshin-cn",
  "phases": [
    { "name": "Prepare", "started": "...", "duration_ms": 120 },
    { "name": "Launch", "started": "...", "duration_ms": 35 },
    { "name": "AwaitImage", "started": "...", "duration_ms": 8200, "image": "YuanShen.exe", "pid": 12345 },
    { "name": "AwaitReady", "started": "...", "duration_ms": 6400, "signal": "WineDevice" }
  ],
  "runtime": {
    "runner":  { "kind": "Managed", "component": "spritz-wine-cachyos-wow64-10.0-11",
                 "resolved": "…/runners/spritz-wine-cachyos-wow64-10.0-11/bin/wine",
                 "source": "profile → shared-cachyos" },
    "dxvk":    { "kind": "Managed", "component": "dxvk-3.1.1", "source": "shared-cachyos" },
    "prefix":  "…/prefixes/genshin-cn"
  },
  "env": { "WINEPREFIX": "…", "WINEDLLOVERRIDES": "…" },
  "env_sources": { "WINEPREFIX": "baseline", "DXVK_HUD": "enhancements" },
  "command": ["…/bin/wine", "YuanShen.exe", "-screen-fullscreen", "0"],
  "external_programs": [
    { "id": "prog-1", "trigger": "Auto", "started_after": "AwaitReady", "pid": 23456 }
  ]
}
```

**这个文件同时被"环境 shell"复用**——两者必须基于同一份 `LaunchPlan`（见 `04-FEATURES.md`）。

---

## 九、许可

| 组件 | 许可 | 对我们的约束 |
|---|---|---|
| Starward | **MIT** | 可自由 fork/移植，保留版权声明 |
| Avalonia | **MIT** | 无 |
| `LibVLCSharp.Avalonia` | LGPL-2.1+ | 动态链接 VLC，无传染 |
| VLC (libvlc) | LGPL-2.1+ | 运行时依赖 |
| `Hk4eTouch` | AGPL-3.0 | **不打包**（用户自备），规避 |
| `ZZZ-touch-unlocker` | **无 LICENSE** | **不打包** |
| an-anime-team 组件 | 各自上游许可 | 仅下载，不随包分发 |

LunarWard 自身：建议 **MIT**（与 Starward 一致）。待确认 →
`06-OPEN-QUESTIONS.md` Q4。
