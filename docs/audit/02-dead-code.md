# 死代码与无效代码审计

> 审计对象：`/home/qie2035/projects/lunarward`（.NET 10 + Avalonia 12.1.4；453 个 `src/**/*.cs`、53 个 `src/**/*.axaml`、63 个 `tests/**/*.cs`）。
> 方法：全量符号提取（类型 337 / 方法 737 / 属性 1794 / 字段 765 个声明），对每个名字在「全 corpus = src+tests 的 .cs 与 src 的 .axaml」做词频引用计数，`refs = 总命中 − 声明次数`；再对每个零/低引用项人工排除 Avalonia 绑定、CommunityToolkit 源生成、JSON 契约、P/Invoke、反射等"假死"。只读审计，未修改任何代码。

## 1. 结论摘要

1. **可直接删除｜`LunarWard.Core/Metadata/` 整个目录（8 文件 520 行）**：`MetadataClient` 全类只有 2 处自身命中（`:10` 声明 + `:33` 构造函数），5 个公开方法零引用；`Github*`、`VersionRelease`、`MetadataJsonContext` 只被它引用。App 已有等价实现 `Services/ReleaseChecker.cs`（同一 `releases/latest` 端点 + 自带版本归一），属移植期残留。
2. **可直接删除｜两个整文件 / 半个文件的死代码**：`Installer/GameUninstallService.cs`（187 行，仅 class+ctor 命中，从未被实例化；App 另有 `GameUninstaller.cs`）；`Platform/FileSystem.cs` 里的 `IFileLock`/`FlockFileLock`（全部命中都在本文件内，单实例已改用 `SingleInstance.cs` 的 Unix domain socket）。
3. **可直接删除｜`Platform/Unix/Libc.cs` 的 16 个零引用 P/Invoke**（PTY、fork/exec、waitpid、Close/Read/Write 等）。`rg 'Libc\.'` 全仓只剩 9 处，活的成员只有 `Flock`/`GetUid`/`Kill`/`SIGTERM`/`SIGKILL`/`SIGHUP`/`LOCK_EX`/`LOCK_NB`/`LOCK_UN`。
4. **可直接删除（低风险批量）｜16 个 `GameRegistry` 常量 + 14 个 App 无绑定成员 + 38 个零引用非 DTO 方法（其中一批在 §4-A/§4-B 分列）**，明细见 §3。
5. **需人工确认｜对外契约面**：`GameRecordClient` 10 个抽象方法（含 2 个实现的 override）无任何调用点；`HoYoPlayClient` 的 scan/chunk/WPF 三组 API 零调用**且** Installer 有一份私有重复实现（应合并，不宜直接删）；`Games` 项目 14 个 helper 属公共库 API。删除会改变对外形状，需同步改实现、测试与文档。
6. **不应按引用计数删**：DTO 模型 239 个"零引用"属性是 JSON 契约；`ConvertBack`、`[JsonConverter]`、P/Invoke 结构体字段、`[GeneratedRegex]`、CommunityToolkit 生成成员同理。遗留标记本身很干净：真 TODO 仅 2 条，≥10 行的注释代码仅 1 处（且已 `[Obsolete(..., true)]`）。

**可直接删除项：6 组代码 —— 9 个整文件、2 个类型（半文件）、46 个成员/常量，合计 57 个可删单元、约 870 行；另有 5 项包声明（DC-13，其中 `Serilog.Extensions.Logging` 需替换而非裸删）。**

## 2. 数据

| 扫描对象 | 总数 | 零/低引用 | 其中真实死代码（排除契约性引用） |
|---|---|---|---|
| 类型声明（class/record/struct/interface/enum） | 337 | `refs≤1` 47 | 9 个类型（集中在 `Core/Metadata/` + 2 个 Platform 类型 + 1 个 orphan 模型） |
| 方法（`public`/`internal`） | 737 | `refs=0` 69 | 38（非 DTO） |
| 属性 | 1794 | `refs=0` 266 | 27（非 DTO） |
| 字段 | 765 | `refs=0` 80 | 64（非 DTO），其中 30 个是 P/Invoke 结构体字段、16 个是 `GameRegistry` 常量 |
| `.axaml` | 53 | 未引用 0 | 0（53 个都有 `x:Class`；唯一无 code-behind 的 `Styles/Starward.axaml` 是 ResourceDictionary，被 `App.axaml:13` 引用，合法） |
| `Assets/` 文件 | 3 | 未引用 0 | 0（`lunarward.ico` 用于托盘+应用图标；2 个 xlsx 是导出模板） |
| `PackageVersion`（Directory.Packages.props） | 23 | 未使用 5 | 5（3 个连 csproj 都没引用，2 个被引用但代码零命中） |
| `third_party/` | 1（README） | — | 0 |
| `TODO/FIXME/HACK/XXX` | 5 命中 | 假阳性 3 | 2 条真待办 |
| `NotImplementedException` | 5 | 契约桩 3 | 2（`FileCombinedStream` 的写路径桩） |
| `NotSupportedException` | 11 | `ConvertBack` 假阳性 5 | 0（其余 6 处是流/不变量保护，合法） |
| `#if` 分支 | 8 | 4 个在死文件内 | 3（`#if DEBUG` 自检） |
| ≥10 行注释掉的代码 | 1 | — | 1（`GameRecordClient.cs:587-609`） |

复现命令（示例）：`rg -c '\bMetadataClient\b' src tests`、`rg -n 'Libc\.' src tests`、`rg -n '\bFlockFileLock\b|\bIFileLock\b' src tests`、`rg -c 'PackageVersion Include' Directory.Packages.props`。

> 口径说明：字段正则把 `=>` 表达式体属性也算进字段（`=` 前缀匹配），因此字段表里有 13 个名字与属性表重复（`IsChina`、`ChannelKeys`、`SupportsDailyNote` 等）；去重后真实零引用字段 67 个。

## 3. 问题清单

### DC-01 ｜ P0 ｜ `Core/Metadata/` 整目录死（含 GitHub Release 模型与 Markdown 渲染）
- **证据**：`MetadataClient.cs:10`（声明）、`:33`（ctor）是仅有的 2 处命中；`GetVersionAsync:77`、`GetReleaseAsync:96`、`GetGithubLatestReleaseAsync:120`、`GetGithubReleaseAsync:129`、`RenderGithubMarkdownAsync:145` 全零引用。
- **引用计数**：`MetadataClient` 2 命中（0 外部）；`MetadataJsonContext` 3 命中（自身 + `MetadataClient.cs:55/154`）；`GithubRelease` 10 命中（3 文件，全在 Metadata 内）；`GithubAsset`/`GithubAuthor`/`GithubUploader` 各 2 命中（互为属性类型）；`GithubMarkdownRequest` 4 命中（3 文件）；`ReleaseVersion` 6 命中（3 文件）；`ReleaseFile` 2 命中（`VersionRelease.cs:41/80`）。
- **重复实现**：`src/LunarWard.App/Services/ReleaseChecker.cs:43`（`LatestReleaseApi`）、`:113`（`ParseVersion` 归一）；另一份审计 `docs/audit/05-structure.md` 也独立指出这对重复。
- **建议**：整目录删除；若后续要"检查更新"，用 `ReleaseChecker` 泛化，不要复活这套 DTO。

### DC-02 ｜ P1 ｜ `Installer/GameUninstallService.cs` 整文件死
- **证据**：`:11` `internal class GameUninstallService`、`:20` ctor；全仓再无命中，没有任何 `new GameUninstallService(...)`。
- **引用计数**：2 命中 / 0 外部调用。
- **建议**：删除 187 行；卸载能力已由 `src/LunarWard.App/Services/GameUninstaller.cs`（200 行）承担。若打算把卸载下沉到 Installer，应先迁移再删（属批次 B）。

### DC-03 ｜ P1 ｜ 文件锁子系统死（`IFileLock` / `FlockFileLock`）
- **证据**：`Platform/FileSystem.cs:9`、`:16`、`:23`（私有 ctor）、`:33`（`TryAcquire`）、`:38`/`:62`（`Libc.Flock`）——全部命中都在本文件内。
- **引用计数**：`IFileLock` 3 命中、`FlockFileLock` 4 命中，外部 0。
- **替代**：`App/Services/SingleInstance.cs`（`$TMPDIR/lunarward-{Environment.UserName}.sock`）。
- **建议**：删掉 lock 部分（约 70 行），保留同文件的 `AtomicFile`（`:78` 起，仍在使用）。

### DC-04 ｜ P1 ｜ `Platform/Unix/Libc.cs` 16 个零引用 P/Invoke
- **证据（行号）**：`WaitPid:38`、`OpenPty:42`、`GrantPt:46`、`UnlockPt:50`、`PtsName:54`、`Ioctl:58`、`SetSid:62`、`Dup2:66`、`Close:70`、`Read:74`、`Write:78`、`ExecVp:82`、`PATH_MAX:12`、`LOCK_SH:15`、`SIGCONT:23`、`TIOCSWINSZ`（文件末尾表达式体）。
- **引用计数**：每个 `Libc.X` 限定引用 0；`Close/Read/Write` 的 38/68/39 次命中全是别的类型的同名成员。全仓 `Libc.` 只有 9 处（`ProcessController.cs:27-29/46/73/80`、`LinuxProcessTree.cs:91`、`FileSystem.cs:38/62`）。
- **建议**：删这 16 个成员；`Libc.cs` 从 88 行降到约 40 行。若确定不做 PTY 注入，连 `#if` 平台分支都不必保留。

### DC-05 ｜ P1 ｜ `Core/GameRegistry.cs` 16 个零引用注册表常量
- **证据/计数**：`LauncherPath_hk4e_cn:8`、`_global:9`、`_cloud:10`、`hkrpg_cn:12`、`hkrpg_global:13`、`bh3_cn:15`、`bh3_overseas:16`、`bh3_global:17`、`bh3_tw:18`、`bh3_kr:19`、`bh3_jp:20`、`HYP_cn:25`、`HYP_os:26`、`GamePath_HYP_cn:51`、`GamePath_HYP_os:52`、`GraphicsSettings_Model_h2986158309:61` —— 每个都是「0 次外部引用 / 1 次声明」。
- **仍活**：`GamePath_hk4e_*`、`GamePath_hkrpg_*`、`GamePath_bh3_*`、`GamePath_nap_*`（各 1 次，来自 `GameBiz.cs:146-155`）；`MIHOYOSDK_ADL_PROD_CN_h3123967166`(6)、`GENERAL_DATA_h2389025596`(4)、`MIHOYOSDK_ADL_PROD_OVERSEA_h1158948810`(3)。
- **建议**：删 16 个 `LauncherPath_*`/`HYP_*`/`GamePath_HYP_*`/`GraphicsSettings_*`；注意 `LauncherPath_*` 是"启动器注册表路径"，与 `GamePath_*`（游戏安装路径）不是一套，删前确认 Wine 注册表 provider 不需要。

### DC-06 ｜ P1 ｜ App 里 14 个无 XAML 绑定、全 corpus 零引用的成员
- **证据（名字在全 corpus 只出现 1 次）**：`ShiyuDefensePageViewModel.cs:504 HasBuffs`、`:541 HasMonsters`、`:581 HasFourthBuff`、`:585 HasFourthChallengeTime`、`:656 HasBuff`；`GameSettingPageViewModel.cs:105 GameSubtitle`；`GameLauncherPageViewModel.cs:86 SupportsDailyNote`、`:89 SupportsGacha`；`App.axaml.cs:22 HasTray`；`StarwardAssets.cs:136 RefreshContentAsync`、`:246 AllArt`；`LauncherService.cs:62 CurrentPlan`、`:65 RunningPrograms`；`ApocalypticShadowPageViewModel.cs:431 Node3BossDefeated`。
- **判定**：之上已排除 `{Binding X}`（axaml 中 0 命中）、`CompiledBinding`、命令源生成（都不是 `Command` 后缀）、DTO（App 层不是 DTO）。
- **建议**：直接删。`CurrentPlan`/`RunningPrograms` 若计划做"启动中"UI 状态，先接线再留。

### DC-07 ｜ P2 ｜ `LunarWard.Games` 14 个零引用 helper（公共库，需确认）
- **证据/计数**：`GameCatalog.cs:48 IsChina`、`:62 ToGameBiz`、`:160 GetGlobal`、`:164 AllFor`、`:220 GetDisplayName`、`:45 GetConfigIniPath`；`GameAdapter.cs:151 BuildEnvOnly`、`:196 SupportsBilibiliChannel`、`:203 MergeInto`；`GameConfigIni.cs:171 OtherSections`、`:225 TryRead`、`:392 AddKnownKey`；`ChannelModel.cs:66 IsSelectable`、`:114 RequiresBilibiliPlugin`、`:125 ChannelKeys`（均为 0 外部引用）。
- **说明**：`GameAdapter.BuildArguments:167` 是**活**的（`App/Services/LauncherService.cs:121`），但它的 `gameKind` 形参在方法体内未使用（调用方仍传 `definition.Kind`）→ 见 DC-14。
- **建议**：这些是"库面"，若 Games 只服务本仓库，删；若要作为独立库发布，保留并在 XML 注释里标注 `Not used by app`。

### DC-08 ｜ P2 ｜ Installer/Core/Launch/Platform 其余零引用成员（31 个）
- **Installer（10）**：`InstallMessages.cs:108 GameIdPair`、`:111 UpdateTask`、`:210 BytesPerSecond`；`ParentProcessWatcher.cs:45 ParentPid`、`:163 WpfVersion`（`[GeneratedRegex]` partial）、`:167 TryParseVersion`；`GameInstallService.cs:159` 内层 `TryGetTask`；`HardLink.cs:80 DescribeError`；`GameInstallHelper.cs:225 CheckFileMD5InReadProgressAsync`；`DriveHelper.cs:238 IsDeviceRemovableOrOnUSB`。
- **Core（11）**：`GameBiz.cs:60 AllGameBizs`、`:125 ToGameName`、`:135 ToGameServerName`；`LanguageUtil.cs:38 FilterAudioLanguage`、`:59 GetAllLanguages`；`GameId.cs:46 IsBilibiliServer`；`LauncherId.cs:21 IsChinaOfficial`、`:27 IsGlobalOfficial`；`GameBackground.cs:72 FromPosterUrl`、`:83 FromCustomFile`；`GachaLogItem.cs:66 GetGachaType`（virtual，无 override 无调用）。
- **Launch（7）**：`ProfileConfig.cs:129 IsolateHome`（配置项，序列化会用到 → 实际是**假阳性**，见 §5）；`RuntimeSettings.cs:50 Vkd3d`、`:68 Wrapper`；`WineAccountService.cs:67 Adl0`；`PlayTimeStatsCalculator.cs:127 PerDay`；`GachaService.cs:44 IsFourStar`；`GenshinBeyondGachaStore.cs:185 GetIcon`。
- **Platform/App（3）**：`ProcessController.cs:79 TerminateGroup`、`AnnouncementService.cs:185 MarkRead`、`ShellViewModel.cs:243 NavigateToToolbox`。
- **建议**：`ParentProcessWatcher.WpfVersion`/`TryParseVersion` 连同 `[GeneratedRegex]` 一起删（11 行）；其余按批次 A 删，`IsolateHome` 保留。

### DC-09 ｜ P2 ｜ `GameRecordClient` 10 个抽象方法零调用（需同步改 2 个实现）
- **证据/计数**：每个都只有「接口/抽象声明 + `HoyolabClient`/`HyperionClient` 的 override」，调用点 0：`GameRecordClient.cs:214 GetGameRecordUserAsync`、`:224 GetAllGameRolesAsync`、`:253 UpdateGameRoleHeadIconAsync`、`:280 GetBH3DailyNoteAsync`、`:299 GetGenshinGameRolesAsync`、`:393 GetStarRailGameRolesAsync`、`:525 GetZZZGameRolesAsync`、`:537 GetZZZGachaRecordAsync`、`:653/:664 GetZZZThresholdSimulation*Async`。
- **注意**：客户端本身是活的（14 个 VM `new HoyolabClient/HyperionClient`，如 `ToolboxPageViewModel.cs:245`、`Launch/DailyNoteService.cs:225`），所以这是"未被使用的 API 面"，不是整类死。
- **建议**：批次 B —— 删除前用 grep 确认无反射/`nameof` 调用，并同步删两个 override；保留 `GetGenshinDailyNoteAsync` 等在用方法。

### DC-10 ｜ P2 ｜ `HoYoPlayClient` 三组 API 零调用 + 与 Installer 私有实现重复
- **证据**：`HoYoPlayClient.cs:431/446/465 GetGameScanInfosAsync`（三重载）、`:534 GetGameChunkBuildAsync`、`:625/658 GetWPFPackagesAsync`（两重载），全部 0 调用。
- **重复实现**：`Installer/GamePackageService.cs:990 GetGameSophonChunkBuildAsync`、`:1005 GetGameSophonPatchBuildAsync`、`:1027 GetGameChannelSDKAsync`（同样的分支/包 URL 组装与 JSON 解析）。
- **建议**：不要直接删。优先级是"把 Installer 的私有实现上移成 HoYoPlayClient 的调用"，再删 Installer 副本；否则两套逻辑继续漂移（这与 05-structure.md 的"下沉债"结论一致）。

### DC-11 ｜ P2 ｜ `SelfQueryClient` 4 个方法零调用且 `type` 形参未用
- **证据**：`:203 GetGenshinQueryBeyondCostumeAsync`、`:219 GetGenshinQueryBeyondCostumeSuitAsync`、`:235 GetGenshinQueryBattlePathAsync`、`:257 GetGenshinQueryBeyondBattlePathAsync`；活的是 `:170 GetGenshinQueryItemsAsync`（`SelfQueryService.cs:59` 调用）。
- **建议**：删 4 个方法（连带 4 个 `GenshinSelfQueryItem_*` 若仅它们使用，需再查类型计数）。

### DC-12 ｜ P3 ｜ `[Obsolete(error:true)]` + `NotImplementedException` + 23 行注释代码
- **证据**：`GameRecordClient.cs:583 GetZZZGachaWikiAsync` 带 `[Obsolete("不可用，返回未登录错误", true)]`，`:586` 抛 `NotImplementedException`，`:587-609` 是唯一 ≥10 行的注释代码块；`FileCombinedStream.cs:116/121`、`FileSliceStream.cs:85/91` 是 Stream 不可写成员的合法桩。
- **建议**：`GetZZZGachaWikiAsync` 整段（含注释块）删除，或保留 `[Obsolete]` 但删注释块——obsolete+throw 已足够表达"不可用"。

### DC-13 ｜ P2 ｜ 声明了但没用到的包（5 个）
- **连 csproj 都没引用（纯噪音）**：`Avalonia.Diagnostics`、`Avalonia.Controls.ItemsRepeater`（只在 `SimulatedUniversePage.axaml:146`、`ImaginariumTheaterPage.axaml:300` 的注释里出现，无 `<ItemsRepeater>` 控件）、`Microsoft.Extensions.Configuration.Json`。
- **被引用但代码零命中**：`Markdig`（仅 `src/LunarWard.App/LunarWard.App.csproj:27`，`rg Markdig src` 无命中）；`Serilog.Extensions.Logging`（`App.csproj:25`，src 里没有 `Microsoft.Extensions.Logging`/`AddSerilog`，日志是 `BackgroundService.cs:265` 直接 `new Serilog.LoggerConfiguration()`）。
- **建议**：删 3 个纯噪音项；删 `Markdig`；`Serilog.Extensions.Logging` **不能直接删**——Serilog 本体靠它传递依赖，删包要同时显式加 `Serilog`（否则 `using Serilog;` 编译失败）。这属于"包声明冗余"，不影响运行时。

### DC-14 ｜ P3 ｜ 无用参数 / 恒真判断
- **无用参数（3 类）**：`Wine/DxvkManager.cs:118 UninstallAsync(string prefix, CancellationToken ct = default)` —— 方法体不读 `ct`（同文件 `InstallAsync` 会用）；`Core/SelfQuery/SelfQueryClient.cs:203/219/235/257` 的 `type`；`Games/GameAdapter.cs:167 BuildArguments` 的 `gameKind`（调用方 `LauncherService.cs:121` 仍传值）。
- **恒真属性**：`GameLauncherPageViewModel.cs:86 SupportsDailyNote => true`；`GachaPageViewModel.cs:244 IsUigfAvailable => true`、`:256 IsImportAvailable => true`、`:261 IsLanguageChangeAvailable => true`（三者均 0 引用/未绑定）。
- **建议**：恒真属性直接删（UI 若需要"总是可用"就直接不显示禁用态）；无用参数删除会改动 public 签名，按批次 B 处理。

### DC-15 ｜ P2 ｜ 重复实现：`LUNARWARD_TOOLBOX_DEMO` 环境变量读取 14 文件 / 26 处
- **证据**：`Environment.GetEnvironmentVariable("LUNARWARD_TOOLBOX_DEMO") == "1"` 在 `ToolboxPageViewModel.cs:57/152`、`SpiralAbyssPageViewModel.cs:119`、`PureFictionPageViewModel.cs:113`、`ForgottenHallPageViewModel.cs:115`、`ChallengePeakPageViewModel.cs:139`、`ApocalypticShadowPageViewModel.cs:130`、`InterKnotMonthlyReportPageViewModel.cs:210`、`TravelersDiaryPageViewModel.cs:60`、`TrailblazeCalendarPageViewModel.cs:182`、`ImaginariumTheaterPageViewModel.cs:153`、`SimulatedUniversePageViewModel.cs:131`、`ShiyuDefensePageViewModel.cs:161/331`、`StygianOnslaughtPageViewModel.cs:147`、`DeadlyAssaultPageViewModel.cs:173` 重复出现。
- **建议**：收敛为 `ToolboxDemoMode.IsEnabled`（`static readonly bool`，进程内只读一次环境变量）；这不是死代码，是重复实现，收益是 26 处 → 1 处。

### DC-16 ｜ P2 ｜ 重复实现：图标加载两两雷同
- **证据**：`if (!string.IsNullOrWhiteSpace(url)) x.Icon = await assets.BitmapAsync(url)` 模式全仓 `BitmapAsync(` 45 处；`ShiyuDefensePageViewModel.cs:187-235 LoadIconsAsync` 与 `DeadlyAssaultPageViewModel.cs:211-259 LoadIconsAsync` 逐行同构（EnumerateAvatars/Buddies/FifthChallenges/Monsters 四轮），`ApocalypticShadowPageViewModel` 4 处、`ChallengePeakPageViewModel` 5 处同型。
- **建议**：抽 `Task LoadIconsAsync<T>(IEnumerable<T> items, Func<T,string?> url, Action<T,IImage?> set)` 到 `StarwardAssets`；可同时把 45 处的空判断收成一处。

### DC-17 ｜ P3 ｜ 重复实现：版本解析 6 套
- **证据**：`App/Services/ReleaseChecker.cs:113 ParseVersion`（含 `1.2`/`1` 归一）、`Games/GameConfigIni.cs:328/344/580 Version.TryParse`、`Installer/GamePackageService.cs:68`（regex+TryParse）、`Installer/GameInstallHelper.cs:892 SdkVersionRegex`、`Installer/ParentProcessWatcher.cs:163-176 WpfVersion+TryParseVersion`（后者已死）。
- **建议**：统一到 `Core` 一个 `LunarVersion`（解析 + 归一 + 比较）；先删 DC-08 里的 `WpfVersion/TryParseVersion`，再合并其余 5 处。

### DC-18 ｜ P3 ｜ 遗留标记与 `#if` 明细
- **真待办（2）**：`Installer/GamePackageService.cs:390` `// todo 存在少数文件大小一致但内容不同`；`Installer/GameInstallService.cs:754` `// todo celar useless audio`（拼写错误，语义是清理无用语音包）。
- **假阳性（3）**：`ArchiveExtractor.cs:226`（注释里的 `Path = xxx` 示例）、`GachaExcelExport.cs:12/106`（`{{list.Xxx}}` 模板占位符）。
- **`#if`（8 处）**：`#if DEV` 4 处全在死文件（`MetadataClient.cs:23/79/98`、`VersionRelease.cs:55`）；`#if DEBUG` 3 处是调试自检（`ImageViewWindow2.axaml.cs:309/477`、`GameRecordClient.cs:177`）+ `GameRecordClient.cs:30 #if !DEBUG`。无 `#if false`，无被注释掉的整段逻辑（除 DC-12 那一处）。
- **建议**：两条 TODO 转 issue 后删除注释；`#if DEBUG` 自检保留（成本低，排障有用）。

### DC-19 ｜ P3 ｜ 唯一 `refs=0` 的类型
- **证据**：`Core/GameRecord/Genshin/StygianOnslaught/StygianOnslaughtMonsterTag.cs:4 public class StygianOnslaughtMonsterTag` —— 全 corpus 0 命中（连作为别的 DTO 属性类型都没有）。
- **建议**：删除；若它是上游 API 的新字段模型，标注 `// reserved for API v?` 并加测试引用，否则下次审计还会被报出来。

### DC-20 ｜ P3 ｜ `Path.Join/Combine` 集中度与可收敛点
- **证据**：`Games/GameScanner.cs` 21 处、`Installer/GameInstallHelper.cs` 20、`Installer/GamePackageService.cs` 16、`Wine/ComponentInstaller.cs` 15、`Wine/XdgPaths.cs` 14、`Wine/DxvkManager.cs` 13。
- **建议**：不是死代码；若后续要防"游戏目录 + 相对路径"拼接不一致，抽 `GamePaths.CombineUnderRoot(root, relative)` 并做越界校验（`XdgPaths` 已有雏形）。

## 4. 建议的删除批次

### 批次 A —— 零风险批量（无调用链、无契约、编译期可验证）
| # | 内容 | 规模 | 校验方式 |
|---|---|---|---|
| A1 | `src/LunarWard.Core/Metadata/` 整目录（DC-01） | 8 文件 / 520 行 | `dotnet build`；`rg -i metadata src` 应为 0 |
| A2 | `src/LunarWard.Installer/GameUninstallService.cs`（DC-02） | 1 文件 / 187 行 | build + 卸载流程手测走 `GameUninstaller` |
| A3 | `FileSystem.cs` 的 `IFileLock`/`FlockFileLock`（DC-03） | ~70 行 / 2 类型 | build + 双开实例验证 `SingleInstance` |
| A4 | `Libc.cs` 的 16 个零引用成员（DC-04） | ~45 行 | build；Linux 上跑进程管理/单实例冒烟 |
| A5 | `GameRegistry.cs` 16 个常量（DC-05） | 16 行 | build + 启动器路径检测冒烟 |
| A6 | App 14 个无绑定成员（DC-06） | 14 成员 | build + 打开对应 5 个页面（史余/末日/设质/启动器/启动）看无 XAML 报错 |
| A7 | 包声明 4 项（DC-13：Diagnostics/ItemsRepeater/Configuration.Json/Markdig）+ `Serilog.Extensions.Logging` 换成显式 `Serilog` | 5 行 props/csproj | `dotnet restore` + build + 日志文件仍生成 |

批次 A 合计：**9 个整文件、2 个类型、46 个成员、5 项包声明，约 870 行代码 + 5 行项目文件**。

### 批次 B —— 需要测试保护（改对外形状）
1. `GameRecordClient` 10 个抽象方法 + 2 个实现 override（DC-09）：先加/补 `tests/LunarWard.UnitTests` 对在用 API 的覆盖，再删；注意 JSON 反序列化测试可能间接覆盖这些 DTO。
2. `HoYoPlayClient` 三组 API 与 Installer 私有实现合并（DC-10）：先让 `GamePackageService` 调 `HoYoPlayClient`，用现有 Installer 单测锁行为，再删双份。
3. `SelfQueryClient` 4 个方法（DC-11）与 `GenshinSelfQueryItem_*` 模型：查清模型是否被别的查询复用。
4. `Games` 项目 14 个 helper（DC-07）：若作为公共库发布则保留，改标 `[EditorBrowsable(Never)]` 或 XML 注释；否则删。
5. 无用参数（DC-14）与 `GameCatalog` 主构造参数 `DefaultArguments`：public 签名变更，需 SemVer 说明。
6. 统一版本解析（DC-17）与 DEMO 环境变量（DC-15）、图标加载（DC-16）：纯重构，需 UI 回归。

## 5. 假阳性说明（看起来没用但必须保留）

1. **JSON DTO 模型**：`Core/GameRecord/`、`HoYoPlay/`、`SelfQuery/`、`GameNotice/` 下 239 个"零引用"属性与 24 个方法由 `System.Text.Json` 按约定/`[JsonPropertyName]` 反射使用，grep 计数天然为 0。**唯一例外**是 `Core/Metadata/` 的 DTO —— 它们只被自己的死客户端引用，可删（DC-01）。
2. **`IValueConverter.ConvertBack`**：`GachaStatsCard.axaml.cs:113`、`GachaPage.axaml.cs:47`、`SelfQueryPage.axaml.cs:315/351/366` 抛 `NotSupportedException` 是接口实现，Avalonia 会调用，不能删。
3. **`[JsonConverter(typeof(X))]` 引用**：`GachaLogItem.cs:43 GachaItemIdJsonConverter`、`GameId.cs:17 GameBizJsonConverter` 只以特性形式出现，grep 名字确实命中，但删 converter 会破坏序列化。
4. **P/Invoke 结构体字段**：`DriveHelper.cs:381-415` 的 `Statx` 字段（`TvSec`/`Mask`/`Blksize`/`MntId`…）是 ABI 布局，字段"未读"不等于可删，删了会读错内存。
5. **源生成与绑定**：CommunityToolkit 的 `XxxCommand`（由 `[RelayCommand]` 生成）、Avalonia 编译绑定 `{Binding X}`、`x:DataType` 属性都不以源码符号出现；本报告已按"属性名是否在 axaml 出现"判定，未把绑定属性报死。
6. **`[GeneratedRegex] partial` 方法**：`ParentProcessWatcher.WpfVersion` 报死是对的（无调用），但删除时必须连 `[GeneratedRegex]` 特性一起删，否则源生成器会因缺少 partial 实现报错。
7. **`record` 声明与属性**：`LaunchError.cs:62 InjectorBinaryMissing`、`:70 ExternalProgramFailed` 被我的 METHOD 正则当成方法，实际是 record 类型；`record` 位置参数"未引用"是 DTO 语义。
8. **JSON 回调**：`GenshinGachaInfo.cs:39 OnDeserialized`、`:48 OnSerializing` 由序列化器调用，零 grep 命中但必须保留。
9. **设计如此的 null 实现**：`InstallerLogger.cs:54 NullInstallerLogger.IsEnabled => false`、`:56` 空 `Log` 是 Null Object 模式，参数"未使用"是刻意的。
10. **配置项**：`ProfileConfig.cs:129 IsolateHome`、`RuntimeSettings` 的若干 `{ get; set; }` 属性由配置文件反序列化写入，属 DC-08 里的**应保留**项（我在 DC-08 已标注）。
11. **`IFileLock` 作为 `FlockFileLock` 的基接口**：接口本身"有引用"，但整个子系统无调用方，故仍列入 DC-03 —— 这是"引用计数不为 0 但实际是死代码"的典型，删除时两个类型一起删。
