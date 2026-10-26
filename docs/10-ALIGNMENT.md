# 10 — 与上游 Starward 的对齐总纲（本 goal 的最高约束）

> 本文件是 2026-10-26 需求方下达的新目标的可执行版本：
> **LunarWard 必须是 Starward 的移植，不是偏移巨大的重实现。**
> 上游权威副本：`/home/qie2035/projects/csharp/Starward`（下称「上游」）。
> 与本文冲突的旧决策（尤其是 D1「重实现」、D12「去掉 gRPC」）一律以本文为准，
> 并在 `docs/00-DECISIONS.md` 中就地修订。

## 0. 判定标准

一处代码算「已对齐」，当且仅当：**把上游对应文件拿过来，除了平台等价替换，
不需要理解性重写**。三问自检：

1. 上游有这个包吗？→ 有且 Linux 可用，就必须用同一个包、同一个版本。
2. 上游文件在这个路径、这个类名吗？→ 是，就落到同名路径与类名。
3. 上游这段逻辑是这样写的吗？→ 是，就照抄；只有平台 API 不可用时才替换，
   且替换点集中、可 grep、在 `09-LINUX-DIVERGENCES.md` 登记。

反面清单（本 goal 要消灭的）：

- ❌ 上游 `Dapper`，本仓库手写 `SqliteCommand` 拼 SQL。
- ❌ 上游 `Features/GameLauncher/GameLauncherPage.xaml.cs`，本仓库拆成
  `Views/.../GameLauncherPage.axaml` + `ViewModels/.../GameLauncherPageViewModel.cs`。
- ❌ 上游 `Starward.RPC` 独立 gRPC 进程，本仓库并改进程内调用。
- ❌ 上游 `WebView2`，本仓库干脆不做（必须做 WebKitGTK 等价物）。
- ❌ 上游 `src/Starward/Features/X`，本仓库 `Services/XxxService.cs` 大杂烩。

## 1. 目标仓库布局（= 上游布局 + Linux 平台层）

上游解决方案 7 个工程，按同一职责重建：

| 上游 | 本仓库目标 | 说明 |
|---|---|---|
| `src/Starward/`（WinUI 3 主程序） | `src/LunarWard/` | Avalonia 12 主程序；`Features/ Controls/ Converters/ Frameworks/ Helpers/` 与上游同名同层级 |
| `src/Starward.Core/` | `src/LunarWard.Core/` | 已基本对齐；补齐 `Metadata/` 等缺失文件（见 §4） |
| `src/Starward.Language/` | `src/LunarWard.Language/` | **新建**：`Lang.resx` + `Lang.Designer.cs` + 卫星语言，替代 `Localization/AppStrings` |
| `src/Starward.RPC/` | `src/LunarWard.RPC/` | **新建**：gRPC 服务端 + `Common.proto` + `GameInstall/`，从 `LunarWard.Installer` 迁入 |
| `src/Starward.Setup.Core/` | `src/LunarWard.Setup.Core/` | **新建**：`ReleaseClient/ReleaseInfo/Github`，从 `Services/ReleaseChecker.cs` 迁入 |
| `src/Starward.Setup/` | `src/LunarWard.Setup/` | **新建（最后做）**：安装器 UI；Linux 以 PKGBUILD/deb 为主，能移植的逻辑先落 Setup.Core |
| `src/Starward.Launcher/`（C++ 启动壳） | `src/LunarWard.Launcher/` | 等价物为原生启动壳（`.desktop`/`lunarward` 脚本或 C++），随 P4 处理 |
| —（无） | `src/LunarWard.Wine/`、`src/LunarWard.Launch/` | **本轮不动**（Wine/DXVK 明确排除）；但 `Launch/Database`、Gacha、PlayTime 等**非 Wine** 逻辑要迁进 `src/LunarWard/Features/` |

`LunarWard.App`、`LunarWard.Games`、`LunarWard.Platform` 三个工程在迁移完成后消失：
- `App` → `LunarWard`
- `Games` → `LunarWard/Features/GameLauncher` + `Features/Account`（Wine 相关留在 `LunarWard.Wine`）
- `Platform` → `LunarWard/Frameworks`（上游 `Frameworks/` 放平台适配）

目标树（主程序）：

```
src/LunarWard/
├── App.xaml(.cs)  Program.cs  AppConfig.Common/Configuration/ServiceProvider/Setting.cs
├── Controls/            # ← 上游同名
├── Converters/          # ← 上游同名（BoolToVisibility…）
├── Features/            # ← 上游同名，每个功能一个目录
│   ├── Background/  CloudGame/  Codec/  Database/  Gacha/  GameAccount/
│   ├── GameInstall/  GameLauncher/  GamepadControl/  GameRecord/  GameSelector/
│   ├── GameSetting/  HoYoPlay/  Overlay/  PlayTime/  RPC/  Screenshot/
│   ├── SelfQuery/  Setting/  Update/  UrlProtocol/  ViewHost/
├── Frameworks/          # ← 平台适配（Linux 进程/文件/输入/托盘/WebView）
└── Helpers/             # ← 上游同名（含 Enumeration/）
```

## 2. 依赖矩阵（Decision: 照用 / 等价替换）

版本一律取上游 `Starward.csproj` 的值（`Directory.Packages.props` 集中管理）。

### 2.1 直接照用（跨平台）

| 包 | 版本 | 用途 | 现状 |
|---|---|---|---|
| Dapper | 2.1.79 | 数据访问 | **必须引入**，替换手写 SQL |
| Microsoft.Data.Sqlite | 10.0.11 | SQLite | 已有，版本对齐 10.0.11 |
| Serilog / Serilog.Sinks.File | 4.x / 7.0.0 | 日志 | 已有 |
| Serilog.Extensions.Logging | 10.0.0 | MEL 桥接 | 引入 |
| Microsoft.Extensions.Caching.Memory | 10.0.11 | 缓存 | 引入（`BackgroundService` 等） |
| Microsoft.Extensions.Configuration(.Binder/.CommandLine/.Ini) | 10.0.11 | 配置 | 已有 Ini，补 Binder/CommandLine |
| Microsoft.Extensions.Http.Polly | 10.0.11 | HTTP 重试 | 引入（RPC 下载） |
| Polly | 8.7.0 | 重试 | 已有，版本降到上游 8.7.0 |
| CommunityToolkit.Mvvm | 8.4.2 | MVVM 源生成 | 已有 |
| MiniExcel | 1.46.0 | 导出 | 已有 |
| ZstdSharp.Port | 0.8.8 | 解压 | 已有 |
| NuGet.Versioning | 7.9.0 | 版本比较 | 引入（`AppVersion` 自研版作废） |
| System.IO.Hashing | 10.0.11 | xxHash | 引入 |
| Grpc.AspNetCore | 2.83.0 | RPC | 引入（P2） |
| Starward.Assets | 0.6.5 | 品牌素材 | 已有 |
| SharpCompress | 0.50.4 | 压缩包 | 引入，替代 `ArchiveExtractor` 自研解包 |

### 2.2 Windows 专有 → Linux 等价物（必须实现，不得「不做」）

| 上游包 | 替代 | 落点 | 备注 |
|---|---|---|---|
| Microsoft.Web.WebView2 | **WebKitGTK 4.1（P/Invoke）+ Avalonia NativeControlHost** | `Frameworks/WebView/` | 上游 6 个窗口用到：GameNotice / LoginPage / BBSWebBridge / CloudGameGacha / ZZZGachaInfo / Update / Welcome 检测 |
| H.NotifyIcon.WinUI | Avalonia `TrayIcon` | `Frameworks/TrayService` | 已有 `TrayService`，改成上游 `TrayIcon` 行为 |
| H.InputSimulator | X11 XTest / uinput（可选） | `Frameworks/Input/` | 手柄模拟输入；不可用时显式禁用并登记 |
| WindowsShortcutFactory | `.desktop` 写入 | `Frameworks/Shortcut/` | `UrlProtocol`/快捷键 |
| Microsoft.Graphics.Win2D / ComputeSharp.D2D1 / Starward.Win2D / Starward.Codec | SkiaSharp（Avalonia 自带） | `Features/Codec/` | 图像加载/缩略图/HDR 效果 |
| Vanara.PInvoke.* | BCL / `System.Diagnostics` / 手写 P/Invoke | `Frameworks/WindowsApi/` | DriveHelper、注册表、DWM、Shell32 |
| Microsoft.WindowsAppSDK / CsWinRT / WinUI 控件包 | Avalonia 12 + 社区等价控件 | 各 Feature | 只在 UI 层替换，逻辑照抄 |
| Aprillz.MewUI | 暂缓 | `LunarWard.Setup` | 安装器 UI；Linux 以发行包脚本为主 |
| Starward.NativeLib | 7-Zip/xxHash 原生库的托管替代 | `Frameworks/Native/` | 7z → SharpCompress；xxHash → System.IO.Hashing |
| Starward.GameInput | 暂缓/禁用 | `Features/GamepadControl/` | 需内核驱动，登记为平台限制 |

## 3. 代码组织规则

1. **复制优先**（沿用 D14）：`cp --reflink=auto` 上游文件 → 改命名空间 → 编译 → 改平台点。
2. **一个上游文件 = 一个移植文件**，路径/文件名/类名一致；不得把上游一个类拆成
   `View`+`ViewModel` 两个文件。
3. **UI 用 code-behind**：上游页面逻辑在 `.xaml.cs`，移植后也在 `.axaml.cs`；
   现有 `ViewModels/*` 里的逻辑按上游落点合并回页面或服务。
4. **命名空间**：`Starward` → `LunarWard`，其余段原样（`LunarWard.Features.Gacha`）。
5. **平台替换点集中**：每个 Linux 分支加 `// Linux: 上游 <API>；这里 <替代>，因为 <原因>`
   注释，并在 `09-LINUX-DIVERGENCES.md` 增行。
6. **上游文件缺失即缺陷**：`comm` 对比出「上游有、本地无」的文件必须补；
   「本地有、上游无」的必须能说明是 Linux 平台层或已合并进上游对应文件。

## 4. 当前差距快照（2026-10-26，随进度更新）

### 4.1 工程

| 项 | 上游 | 现状 | 差距 |
|---|---|---|---|
| 工程数 | 7 | 7（但构成不同） | 缺 Language/RPC/Setup.Core/Setup/Launcher；多 Games/Installer/Launch/Platform/Wine |
| 主程序文件 | 232 `.cs` / 82 `.xaml` | 462 `.cs` / 54 `.axaml` | 组织与文件数均偏离 |

### 4.2 Core

`comm` 结果：上游 236 个 `.cs`，本地 232 个。

- **上游有、本地无（9）**：
  `GameRecord/Genshin/StygianOnslaught/StygianOnslaughtMonsterTag.cs`、
  `Metadata/`（8 个：`MetadataClient`、`MetadataJsonContext`、`VersionRelease`、
  `Github/GithubAsset|GithubAuthor|GithubMarkdownRequest|GithubRelease|GithubUploader`）
- **本地有、上游无（5）**：
  `Account/GameAuthLoginClient.cs`（应落 `Features/GameLauncher/GameAuthLoginService`）、
  `Gacha/GachaNoUp.cs`（上游在同目录 `Features/Gacha/GachaNoUp.cs`，位置对齐即可）、
  `IGameInstallPathProvider.cs` + `SelfQuery/ISelfQueryStore.cs` + `SelfQuery/SelfQueryService.cs`
  （Linux 抽象，保留并登记）

### 4.3 数据层（Dapper，需求方点名）

- 上游：`Features/Database/DatabaseService.cs`（1065 行，Dapper + `DatabaseSqls` 版本化 +
  `DapperSqlMapper` 类型处理器）；`Features/Gacha/*Service` 用 `con.Query/Execute`。
- 本地：`src/LunarWard.Launch/Database/{LunarWardDatabase,GachaLogStore,PlayTimeStore,
  SelfQueryLogStore,GenshinBeyondGachaStore}.cs` 手写 `SqliteCommand`（约 1700 行）。
- 动作：引入 Dapper → 移植 `DapperSqlMapper` + `DatabaseService`（文件名校对上游
  `StarwardDatabase.db` 的命名策略）→ 五个 Store 重写成上游同款 `Query/Execute` 调用。

### 4.4 WebView2

上游 6 处窗口 + Welcome 检测；本地无 WebView。动作见 §2.2。

## 5. 阶段计划

| 阶段 | 内容 | 完成判据 |
|---|---|---|
| **P0 基线** | 构建/测试绿；修掉现存 1 个失败用例 | `dotnet build` 0 错；`dotnet test` 全绿 |
| **P1 依赖** | `Directory.Packages.props` 对齐 §2.1；Dapper 数据层落地 | 无手写 `SqliteCommand` 业务 SQL；包清单与上游交集 100% |
| **P2 骨架** | 建 Language/RPC/Setup.Core；`App`→`LunarWard` | 工程表与上游一一对应；构建绿 |
| **P3 组织** | `Features/*` 迁移 + 文件改名 + VM 合并回 code-behind | `comm` 差异只剩 §4.2 登记项 |
| **P4 平台等价物** | WebKitGTK、托盘、快捷方式、7z、输入、Skia 图像 | 六个 WebView 窗口可用；差异清单逐条有实现 |
| **P5 功能对齐** | 逐 Feature 对照上游补实现/行为 | `docs/08-UI-PARITY.md` 与验收脚本通过 |
| **P6 收尾** | 决策文档修订（D1/D12）、第三方声明、README | 文档与代码一致 |

## 6. 验收

1. `scripts/check-parity.py` 与新增的 `scripts/check-alignment.py` 通过（工程表、
   包表、文件映射表三类检查）。
2. `dotnet build LunarWard.slnx` 0 错误；`dotnet test` 全绿。
3. 每处与上游的偏差都能在 `09-LINUX-DIVERGENCES.md` 找到平台依据；
   找不到依据的偏差视为未完成。
