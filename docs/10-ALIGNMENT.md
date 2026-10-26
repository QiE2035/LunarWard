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
- ⚠️ 旧偏离已推翻：上游 `Starward.RPC` 独立 gRPC 进程**已按上游重建**（`src/LunarWard.RPC`，Unix socket 替代 NamedPipe，见 docs/09 §十八）。
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
| 工程数 | 7 | 5 src + 3 tests | **一一对应**：`Starward`→`LunarWard`、`Starward.Core`→`LunarWard.Core`、`Starward.Language`→`LunarWard.Language`、`Starward.RPC`→`LunarWard.RPC`、`Starward.Setup.Core`→`LunarWard.Setup.Core`；`Starward.Setup` 以 `Features/GameInstall/Installer`（进程内）等价、`Starward.Launcher` 以 `Program.cs` + `.desktop` 等价（§8.2/§8.3） |
| 主程序文件 | 314 | 308 同名落点 + **6 等价** | **0 缺失**（`check-alignment.py` PASS）；本地独有 118 个文件按平台层/等价物分类登记在 `docs/17-PLATFORM-LAYER.md` |

非上游的 5 个工程（Launch/Games/Platform/Wine/Installer）已在 P6 折叠进 `LunarWard`（提交 06c0553、8d431f4、c5d4269、4221c97），
类型与命名空间保持不变，文件位于 `src/LunarWard/{Launch,Platform,Wine}/` 与 `src/LunarWard/Features/GameInstall/Installer/`。

### 4.2 Core

`comm` 结果已收敛：此前缺的 Metadata 8 文件已补齐（`src/LunarWard.Core/Metadata/`）。
本地独有的 5 个文件按 §3 规则保留并登记：`Account/GameAuthLoginClient.cs`、`Gacha/GachaNoUp.cs`、
`IGameInstallPathProvider.cs`、`SelfQuery/ISelfQueryStore.cs`、`SelfQuery/SelfQueryService.cs`。

### 4.3 数据层（Dapper，需求方点名）

- **已完成**（提交 f825be9 / c458997）：`Directory.Packages.props` 引入 Dapper 2.1.79；
  `src/LunarWard/Features/Database/{DatabaseService,DapperSqlMapper}.cs` 按上游落地；
  四个 Store 的业务 SQL 全部改为 `con.Query/Execute`，`grep "new SqliteCommand" src/LunarWard.Launch/Database` = 0。
- 遗留：`LunarWard.Launch/Database/LunarWardDatabase.cs` 是启动期/安装器用的旧建库器，收尾时并入 `DatabaseService` 后删除。

### 4.4 WebView2

- **已完成**（提交 7fbe85a）：`src/LunarWard/Frameworks/WebView/`，`WebView : NativeControlHost` +
  WebKitGTK 4.1 + GTK3，WebView2 形状门面（`CoreWebView2`/`Settings`/`Profile`/`CookieManager`/全套事件参数）；
  自检 9~11 项 PASS，XWayland 下可嵌入、无 XID 时降级独立 GTK 窗口。
- 已知缺口见 `09-LINUX-DIVERGENCES.md` §五「尚未覆盖」。

### 4.5 进度看板（同名落点/上游文件）

| 项 | 读数（2026-10-26 终验 `scripts/verify.sh`） |
|---|---|
| 文件映射 | `python3 scripts/check-alignment.py`：**613 上游 = 606 同名落点 + 7 等价，missing 0，命名空间泄漏 0，PASS** |
| 分树 | `src/Starward` 308/314（6 等价）、`src/Starward.Core` 248/248、`src/Starward.Language` 13/13、`src/Starward.Setup.Core` 11/11、`src/Starward.RPC` 26/27（1 等价：NamedPipe→Unix socket） |
| 分组 | 全部 0 缺失：GameRecord 47、Gacha 40、GameLauncher 26、Setting 25、Screenshot 23、ViewHost 20（2 等价）、Helpers 16（2 等价）、PlayTime 14、Background 13、Converters 11、GameInstall 9、Codec 8、Update 8、GameSetting 7、GameSelector 6、GameAccount 5、Controls 5（2 等价）、Overlay 4、SelfQuery 4、CloudGame 3、GamepadControl 3、UrlProtocol 3、Database 2、Frameworks 2、AppConfig 4、App.xaml(.cs)、Program.cs、GameFeatureConfig、HoYoPlay、RPC |
| 编译 | `dotnet build LunarWard.slnx --no-incremental` **0 错误**（252 警告，全部为上游原样的 nullable/CA 风格警告 + Avalonia 弃用提示） |
| 测试 | `LunarWard.UnitTests` **532/532**、`LunarWard.Installer.UnitTests` **36/36**、`LunarWard.IntegrationTests` **27/27**（合计 595，全绿） |
| 自检 | 17 页 `--screenshot` 全过（`launcher gacha beyond selfquery screenshot toolbox settings game-setting` + 9 个设置分页），图在 `$HOME/.cache/lwart-verify/smoke/` |
| 依赖 | 跨平台包与上游版本逐项一致（Dapper 2.1.79 等）；Windows 专有包 25 个走等价物（清单见 §2.2） |
| 工程 | 上游 7 工程 → 本仓库 5 个 `src` 工程 + 3 个测试工程；Setup/Launcher 的处置见 §8 |

一条命令跑完全部四项：`bash scripts/verify.sh`（构建产物与日志写 `$HOME/.cache/lwart-verify`，不占 `/tmp`）。

总览随时用 `python3 scripts/port-backlog.py` 重算（`--write` 写回 `docs/11-PORT-BACKLOG.md`；其中「缺」包含等价项，以 `check-alignment.py` 为准）。
注意：「有文件」只代表路径对齐，内容是否按上游实现要逐节核对；`docs/09` 记录了每一处平台替换的依据。

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

1. `bash scripts/verify.sh` 一次跑完四项：`--no-incremental` 全量构建 0 错误、
   三个测试工程全绿、17 页截图自检全 `SCREENSHOT_OK`、`check-alignment.py` PASS
   （工程表、包表、文件映射表、命名空间泄漏四类检查）。
2. `dotnet build LunarWard.slnx` 0 错误；`dotnet test` 全绿（532 + 36 + 27）。
   `scripts/check-parity.py` 已随 `docs/08` 废弃而停用（它校验的是重实现时期的旧路径）。
3. 每处与上游的偏差都能在 `09-LINUX-DIVERGENCES.md` 找到平台依据；
   找不到依据的偏差视为未完成。

## 7. 收尾删除计划（endgame，P6）

移植期允许「新 Features 文件」与「旧 Views/ViewModels/Services」并存（否则无法渐进切换）。
**最终态不允许并存**。删除顺序（每一项都要先确认对应 Features 已完整可用、截图自检通过）：

| 顺序 | 删除对象 | 数量 | 替代 |
|---|---|---|---|
| 1 | `src/LunarWard/Views/Starward/Pages/Record/**`（13 个战绩页） | 13 | `Features/GameRecord/<Game>/` |
| 2 | `src/LunarWard/Views/Starward/Pages/{GameLauncher,Gacha,Screenshot,…}` + `Views/Starward/Dialogs/*` | ~40 | 对应 `Features/*` |
| 3 | `src/LunarWard/ViewModels/**`（含 `StarwardPage`/`PageContext` 体系） | 49 | 逻辑已并入上游 `.axaml.cs` |
| 4 | `src/LunarWard/Services/**`（26 个） | 26 | 上游 `Features/*Service` + `AppConfig` |
| 5 | `src/LunarWard/Views/**`、`ViewModels/**`、`AppServices.cs`（旧 UI 层整层） | 125+ | **已完成**：`Features/ViewHost/MainWindow+MainView` 成为唯一外壳；`App.axaml.cs` 重写为上游 `App.xaml.cs` 结构（`OnFrameworkInitializationCompleted` ≡ OnLaunched / `HandleStartupAsync` / `EnsureMainWindow` / `EnsureSystemTray` / `Exit`），`AppInstance`→`Frameworks/SingleInstance`，`Timer`→`DispatcherTimer`。提交 `675af80`、`8893576`、本步多个提交 |
| 6 | `src/LunarWard.Launch/**`（69 个，含旧 Database/启动逻辑） | 69 | **已完成**：Launch 折叠进 `src/LunarWard/Launch/`，旧 `Database/**` 16 个生产文件与 13 个孤儿测试删除（f394de9、2a9c6fe），调用点迁到 `Features/Database/DatabaseService` |
| 7 | `src/LunarWard.{Games,Platform,Wine,Installer}` | 4 工程 | **已完成**：Games/Platform/Wine/Installer 折叠进 `src/LunarWard/{Wine,Platform}/` 与 `Features/GameInstall/Installer/`（06c0553…4221c97）；`LunarWard.slnx` = 5 src + 3 tests |
| 8 | `src/LunarWard/Localization/AppStrings.cs`、`Styles/Starward.axaml` 等本地独有层 | ~10 | **已完成**：`AppStrings` + `Resources*.resx` + 相关测试整层删除（176bdda，task-22），9 个页面改 `{x:Static lang:Lang.Xxx}`；`Styles/Starward.axaml` 等其余本地层按 `docs/17-PLATFORM-LAYER.md` 分类保留（平台层） |

删除前置条件：`scripts/check-alignment.py` 报告「本地独有文件数 = 已登记白名单」（脚本已落地，见 §6）。

### 7.1 本轮已执行（2026-10-26 傍晚）

- **旧 UI 层已整层删除**：`src/LunarWard/Views/**`（84 文件）、`src/LunarWard/ViewModels/**`（41 文件）、`AppServices.cs`。`Views.Starward/Controls/StarwardContentDialog` 迁到 `Frameworks/Dialogs/`（提交 `8893576`），旧重复卡片 `Controls/{GachaStatsCard,ZZZGachaStatsCard}.axaml(.cs)` 删除（新实现在 `Features/Gacha/`）。
- **启动骨架重写**：`App.axaml.cs` 现在与上游 `App.xaml.cs` 一一对应（见 §7 第 5 行），自检工具集中到文件末尾 `RunCaptureTooling`（`--screenshot`/`--page`），不再有旧壳分支、`LUNARWARD_LEGACY_SHELL`、`--window settings|account|dailynote`。
- **`Program.cs`** 只保留上游 `Program.cs` 的职责 + 本仓库 CLI 工具（`--version/--help/--scan-games`）+ 自检参数解析；`rpc`/`startgame`/`playtime`/`lunarward://` 四类子命令回到 `App.HandleStartupAsync`（与上游一致）。
- **验证**：新外壳下 17 个页面/设置分页截图自检全过（命令见 `docs/12-XAML-PORT-GUIDE.md` 与 `scripts/smoke-screenshots.sh`），单测 859/859。
- **五个非上游工程折叠完成**（task-18）：见 §7 第 6/7 行；`check-alignment.py` 的工程表 7/7 matched。
- **旧数据层删除完成**：`Launch/Database/**`（旧手搓 store）、`Wine/GameAccountStore`、旧 `Launch/{Gacha*,Uigf4*,PlayTime*}` 共 16 个生产文件 + 13 个孤儿测试；`DatabaseService` 成为唯一入口。
- **Linux 数据库加固补回**（a5c04a0）：上游在 Windows 下依赖目录 ACL，Linux 无等价物 → `DatabaseFilePermissions` 在打开连接后把主库与 WAL 侧车收紧到 0600。
- **平台层账面**：新增 `docs/17-PLATFORM-LAYER.md` 逐类登记上游没有的 118 个文件（平台层/等价物/进程内化/本地扩展），本 goal 的「偏移」以此文件为唯一索引。
- **`AppStrings` 删除完成**（176bdda，task-22）：`Localization/` 整目录 + 两个 resx + 相关测试删除，`{Binding Strings[...]}` 0 命中，全仓走上游 `{x:Static lang:Lang.Xxx}`。
- **关于页更新路径统一完成**（fb80654，task-23）：删除自造 `ReleaseChecker` 与 12 个孤儿用例，改走上游 `UpdateService`+`NuGetVersion` 比较+`UpdateWindow.NewVersion`；预览渠道开关恢复可写。
- **抓屏回退链完成**（fbfd517，dapper-db）：grim→spectacle→gnome-screenshot→import→xwd 五级降级、XWayland 先试 xdotool、补回 `DatabaseFilePermissionsTests` 5 用例。
- **`Services/**` 剩余 14 个文件**已逐条登记在 `docs/17-PLATFORM-LAYER.md`（平台层等价物：XDG 路径/进程树/Wine 注册表/背景目录/Avalonia 对话框宿主等），保留而非删除。
- 终验（`bash scripts/verify.sh`）：构建 0 错误、595 测试全绿、17 页截图全过、`check-alignment.py` PASS。


## 8. 开放决策（需 Lead 拍板，暂记）

1. **RPC 工程**：上游 `Starward.RPC` 是独立进程（NamedPipe + Windows ACL + `Starward.NativeLib` zstd + Vanara）。
   **决策已推翻**（2026-10-26）：NamedPipe → Unix domain socket（`NamedPipesConnectionFactory` → `UnixSocketConnectionFactory`），
   独立进程照建（`src/LunarWard.RPC`，rpc-port 已交付，`SelfTest` 见到 `SELFTEST_OK`）。ACL 部分用 `chmod 0600` + 同用户校验，
   NativeLib 的 zstd 用 `ZstdSharp.Port` 等价。详见 `docs/09-LINUX-DIVERGENCES.md` 的 RPC 节。
2. **Setup 安装器**：上游用 `Aprillz.MewUI`（Windows 专有 UI）。等价物 = 安装引擎进程内化到
   `src/LunarWard/Features/GameInstall/Installer/`（25 文件，承接原 `LunarWard.Installer` 工程），
   由主程序内 UI 驱动，不复刻 MewUI；`Starward.Setup.Core` 已整项目移植为 `LunarWard.Setup.Core`。
3. **Launcher（C++ 壳）**：上游 3 个 C++ 文件负责启动前环境；Linux 由 `Program.cs` + `.desktop` 承担，不写 C++。
4. **`Controls/ColorRectChart`**：本地已有等价 Avalonia 实现（`ChartLegend` 记录 + 硬边渐变），不重复造，登记 §09。
5. **`Helpers/FileDialogNative.cs`**：Win32 COM 对话框细节，StorageProvider 已覆盖，**不移植**。
