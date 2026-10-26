# 14 · 启动器簇（GameLauncher / GameInstall / GameSelector / GameSetting / GameAccount / HoYoPlay）行为级审计

- 审计人：`launcher-port`（task-15）
- 日期：2026-10-26
- 上游权威：`/home/qie2035/projects/csharp/Starward/src/Starward/Features/…`（只读）
- 本地落点：`src/LunarWard/Features/{GameLauncher,GameInstall,GameSelector,GameSetting,GameAccount,HoYoPlay}/`
- 移植批次：`b2bfc44` `474f7f6` `126ad2f` `d394725` `a449aa1` `e37b300` `e31ef9d` `7d295eb` `2f737a0` `088aea2`（+ 文档 `6aa6dce` `8c1d6ff` `8ddbb70`）
- 方法：逐文件 `diff -u`（把命名空间 `Starward.`→`LunarWard.` 归一后只看行为差异）+ 方法签名集合差 + `AppConfig.*` 调用集合差 + 关键算法手工对照（config.ini、启动命令行、注册表键、包清单、预下载判定）。

> 结论速览：54 个上游文件，52 个同名落点（`NoAnimationImageExStyle.xaml` 与 `GameInstallService.cs` 见 §5）；`AppConfig.*` 调用集合逐文件完全一致（无漏删/漏加的行为开关）；行为差异集中在 **平台替换**（注册表→wine、进程探测→/proc、启动→wine、GPU 枚举→sysfs、Win2D→降级预览）与 **三处「上游有、本地无」**（见 §3，已补齐 2 处、登记 1 处）。

---

## 1. 审计矩阵（行数 / 结论）

结论列：`一致` = 除命名空间/格式外无行为差异；`平台等价` = 有替换但语义等价（见 docs/09 §十一/§十二）；`待办` = 见 §3。

### 1.1 Features/GameLauncher（26 文件）

| 文件 | 上游行 | 本地行 | 差异点 | 结论 |
|---|---:|---:|---|---|
| `GameConfigIni.cs` | 41 | 49 | 仅 `= default!` 可空初始化；`[ConfigurationKeyName]` 8 键逐字一致 | 一致 |
| `GameState.cs` | 32 | 35 | 枚举值/注释一致 | 一致 |
| `StartGameAction.cs` | 12 | 15 | 一致 | 一致 |
| `GameStartedMessage.cs` / `GameAnnouncementSettingChangedMessage.cs` / `GameInstallPathChangedMessage.cs` / `GameNoticeWindowClosedMessage.cs` | 6/6/9/6 | 9/9/9/9 | 只有文档注释 | 一致 |
| `GamePostGroup.cs` | 41 | 40 | `GroupBy(Type).OrderBy(Key)`、`CoreLang.PostType_*` 逐字 | 一致 |
| `GameLauncherService.cs` | 547 | 604 | ①启动改为 `wine` + `WINEPREFIX/WINEARCH/WINEDLLOVERRIDES` 基线环境 + ZZZ-DX12 的 DXVK/VKD3D 变量；②命令行保持单字符串（不按空格拆）；③`Process.GetProcessesByName`+SessionId → `Process.GetProcesses()` 同名匹配；④线程挂起 → `/proc/<pid>/stat` 的 `T`；⑤去掉 `PlayTimeRecordService`（时长由 `Launch.PlayTimeService` 记录）；⑥`cmd /c start` 忽略、`runas` 无等价物；⑦`SetGenshinEnableHDR`→`...Async`；⑧`BuildStartInfo` 提为 `public static`（可测缝） | 平台等价（详见 docs/09 §十二，§4.2 复核） |
| `GameAuthLoginService.cs` | 249 | 285 | `GameRecordService.UpdateDeviceFpAsync` → 本地私有复刻（`HyperionClient` + `AppConfig.HyperionDeviceId/Fp/FpLastUpdateTime`，3 天过期）；请求体/`x-rpc-*` 头/`CreateAuthTicketByGameBiz` 逐字 | 平台等价 |
| `GameNoticeService.cs` | 107 | 122 | ①`GameRecordService.GetLastSelectGameRecordRoleOrTheFirstOne` → 同 SQL 直查 `GameRecordRole`+`Setting`（task-5 落地后切回服务）；②注册表取 UID → `WineAccountService.Read(biz)?.Uid` | 平台等价（有回落待办） |
| `GameNoticeWindow.xaml` | 32 | 28 | `WebView2`→`WebView`（WebKitGTK）；`ProgressRing`→`ProgressBar`；`DefaultBackgroundColor`/资源字典去掉 | 平台等价 |
| `GameNoticeWindow.xaml.cs` | 273 | 245 | ①`XamlRoot`/`AppWindowId` 贴边定位 → `Activate()` = `Show()`；②`WindowEx` 子类化输入钩子（`InputSiteSubclassProc`）**未实现**（窗口不因焦点丢失自动关闭）；③注入 JS 逐字；④`Launcher.LaunchUriAsync`→`TopLevel.Launcher` | 平台等价 + 1 待办（§3.3） |
| `GameAuthLoginButton.xaml(.cs)` | 73 / 82 | 59 / 131 | `Flyout` 保留；`ToggleSwitch.IsOn`→`IsChecked`；`FontIcon`→`TextBlock.icon`；去掉 `DateTimePickerFlyoutButtonStyle`；`[INotifyPropertyChanged]`→手写 | 平台等价 |
| `GameBannerAndPost.xaml(.cs)` | 149 / 331 | 132 / 382 | `FlipView`→`Carousel`、`Pivot`→`TabControl`、`ItemsRepeater`→`ItemsControl`、`DispatcherQueueTimer`→`DispatcherTimer`；`PipsPager` 的「1/3」改由 `CurrentBannerIndexText` 提供（绑定不支持静态方法调用）；`Launcher`→`TopLevel.Launcher` | 平台等价 |
| `DX12IntroDialog.xaml(.cs)` | 93 / 32 | 55 / 33 | `ContentDialog`→`StarwardContentDialog`；`Close()` 与基类同名递归 → `CloseDialog()`；预览图 `Image`（待 task-4 `CachedImage`） | 平台等价 |
| `StartGameButton.xaml(.cs)` | 412 / 364 | 277 / 420 | ①`ThemeShadow`→`BoxShadow`、`AnimatedIcon`→`FontIcon`、`Popup.DesiredPlacement`→`Placement`；②`Button_GameAction_IsEnabledChanged` 事件订阅 → 在 `UpdateActionButtonState` 里同步设置（等价）；③`ActualThemeChanged`→`DynamicResource`（删除处理器）；④`AccentFillColor*`→`StarAccentBrush` 等资源键 | 平台等价 |
| `GameLauncherPage.xaml` | 684 | 686 | `NavigationView`→无、`WrapPanel` 保留；`cwc:Segmented`→`ToggleButton`；`PlayTimeButton`/`CloudGameButton`/`DailyNoteButton` 三个未移植控件留 TODO 注释（§3.2） | 平台等价 + 3 待办 |
| `GameLauncherPage.xaml.cs` | 1182 | 1244 | 见 §4.3：`[ObservableProperty]` 手写、`XamlRoot`→`TopLevel`、拖放 `DataView`→`Data.GetFiles()`、剪贴板 `SetStorageItems`→`Clipboard.SetFilesAsync`、`FolderLauncherOptions`→`LaunchDirectoryInfoAsync`、`DispatcherQueueTimer`→`DispatcherTimer`；`GameState` 赋值点与上游逐处对应 | 平台等价 |
| `GameLauncherSettingDialog.xaml` | 724 | 597 | `NavigationView`→`ListBox`+`Carousel`；`cwc:Segmented`→`ToggleButton`；`Hyperlink`→`Button.link` | 平台等价 |
| `GameLauncherSettingDialog.xaml.cs` | 1232 | 1263 | `NavigationView_ItemInvoked`→`SelectionChanged`；`[ObservableProperty]` 手写；`TextBlock_IsTextTrimmedChanged` 删除；388/445/579 三处 `GameInstallService` 调用归 rpc-port（§3.1） | 平台等价 + 1 待办 |
| `CompactCheckBoxStyle.xaml` | 375 | 32 | **只保留紧凑语义**（`CheckBox.compact` 的 Padding/MinHeight），未逐行翻译 375 行 WinUI 控件模板 | 待办（§3.3，已登记 docs/10 §4.5） |

### 1.2 Features/GameInstall（9 文件）

| 文件 | 上游行 | 本地行 | 差异点 | 结论 |
|---|---:|---:|---|---|
| `GamePackageService.cs` | 181 | 184 | 逐字（两处 `[GeneratedRegex]`、四语言位标志、`predownload=` 判定） | 一致 |
| `GameInstallTaskStartedMessage.cs` | 13 | 18 | `GameInstallContext` 来自 `LunarWard.RPC.GameInstall`（上游 `Starward.RPC`） | 一致 |
| `GameInstallService.cs` | 417 | 417 | **内容不是本移植批次**：rpc-port 以 `LunarWard.RPC.GameInstall.GameInstallService` 同名落点覆盖（gRPC 服务端实现），与上游同名文件同构 | 已互换（§3.1） |
| `InstallGameDialog.xaml(.cs)` | 209 / 435 | 165 / 471 | `ContentDialog`→`StarwardContentDialog`；`Segmented`→`ListBox(Multiple)`；`TextBlock_IsTextTrimmedChanged`、`cu:AttachedDropShadow`、`ImageEx` 样式去掉；`StorageProvider` 选目录 | 平台等价 |
| `PreDownloadButton.xaml(.cs)` | 204 / 252 | 112 / 270 | `Segmented`→`ListBox`；`FontIcon`→`TextBlock.icon`；逻辑逐字 | 平台等价 |
| `PreDownloadDialog.xaml(.cs)` | 175 / 531 | 127 / 578 | 同上 | 平台等价 |

### 1.3 Features/GameSelector（6 文件）

| 文件 | 上游行 | 本地行 | 差异点 | 结论 |
|---|---:|---:|---|---|
| `GameBizDisplay.cs` | 15 | 16 | 一致 | 一致 |
| `GameBizIcon.cs` | 125 | 130 | `GB=1<<30`、`{TotalSize/GB:F2}GB`、`0→null`、`IsSelected→MaskOpacity` 逐字；资源路径 `ms-appx:///`→`avares://LunarWard/` | 一致（资源前缀不同） |
| `GameSelector.xaml` | 572 | 516 | `TeachingTip`→`Popup`；`Hyperlink`→`TextBlock`+指针事件；`Expander`/`Flyout`/`ItemsControl` 保留；删 `Interaction.Behaviors` 动画块 | 平台等价 |
| `GameSelector.xaml.cs` | 975 | 966 | ①`AutoSearchInstalledGames` 补回 **HYP 注册表回退**（`WineRegistry.ReadStringAsync`，`GamePrefixContext` 复用 prefix），改 `async`；②`XamlRoot.Changed`→`TopLevel.ScalingChanged`；③`SetWindowDragRectangles`→空 + 消息；④`Translation`→`TranslateTransform`；⑤`GetOpenPopupsForXamlRoot`→自持 `_currentFlyout.Hide()`；⑥`GetFileInformationByHandleEx(FileIdInfo)`→`stat` 的 `st_dev/st_ino` | 平台等价（HYP 已补齐） |
| `MainWindowDragRectAdaptToGameIconMessage.cs` | 17 | 17 | 一致 | 一致 |
| `NoAnimationImageExStyle.xaml` | 63 | — | WinUI `ImageEx` 专有样式，本仓库无 `ImageEx`；等价声明在 `scripts/check-alignment.py` 的 `EQUIVALENTS` | 不移植（等价声明） |

### 1.4 Features/GameSetting（7 文件）

| 文件 | 上游行 | 本地行 | 差异点 | 结论 |
|---|---:|---:|---|---|
| `GameSettingService.cs` | 321 | 346 | `Registry`→`WineRegistry`（全部方法 `async` + `Async` 后缀）；键名/JSON 结构/`\0` 口径/Clamp 区间逐字（17 处 `\0` 一一对应） | 平台等价 |
| `GraphicsSettings_Model_h2986158309.cs` | 91 | 91 | 逐字（record 字段名 = JSON 字段名） | 一致 |
| `GraphicsSettings_PCResolution_h431323223.cs` | 15 | 18 | 逐字 | 一致 |
| `GameSettingPage.xaml(.cs)` | 199 / 481 | 160 / 538 | `DisplayArea.GetFromWindowId`→`TopLevel.Screens.Primary`；`BitmapImage`→`new Bitmap(localPath)`；`UpdateHdrState()` 空实现（无 WinRT 高级颜色 API）；`[ObservableProperty]` 手写 | 平台等价 |
| `GenshinHDRLuminanceSettingDialog.xaml(.cs)` | 202 / 343 | 171 / 202 | Win2D（`CanvasSwapChainPanel`/`CanvasBitmap.LoadAsync`/`LinearTransferEffect`）整体降级为静态预览 + `MoraPreviewOpacity`；`Close()`→`CloseDialog()`；显示器信息简化为分辨率/缩放 | 平台等价（降级，见 docs/09 §十一） |

### 1.5 Features/GameAccount（5 文件）+ HoYoPlay（1 文件）

| 文件 | 上游行 | 本地行 | 差异点 | 结论 |
|---|---:|---:|---|---|
| `GameAccount.cs` | 24 | 26 | `= ""` / `= []` 可空初始化 | 一致 |
| `GameAccountUidStringConverter.cs` | 32 | 36 | `Microsoft.UI.Xaml.Data.IValueConverter`→`Avalonia.Data.Converters.IValueConverter`（签名多 `culture`） | 平台等价 |
| `GameAccountService.cs` | 163 | 153 | ①注册表读写 → `WineAccountService.Read/Write`（prefix 由 `GamePrefixContext` 或构造参数给出）；②`GetSuggestionUids` 返回 `long`（上游 `int`）；③SQL 逐字（`INSERT OR REPLACE`/`DELETE WHERE SHA256`/`SELECT DISTINCT Uid … AND Uid>0`） | 平台等价 |
| `GameAccountSwitcher.xaml(.cs)` | 152 / 256 | 114 / 245 | `AutoSuggestBox`→`AutoCompleteBox`；`InputScope(Number)`+`BeforeTextChanging`→`TextInput`+`e.Handled`；`ContentDialog`→`StarwardContentDialog`+`DialogService`；`FontIcon`→`TextBlock.icon` | 平台等价 |
| `HoYoPlayService.cs` | 378 | 399 | 仅 `GetGPUInfos()`：SetupAPI→`/sys/class/drm/card*`（`product_name`/`uevent` 的 `DRIVER=`/`/sys/module/<driver>/version`，兜底 AMD/Intel/NVIDIA GPU）；其余方法逐字 | 平台等价 |

---

## 2. 关键行为复核（任务点名的 5 项）

### 2.1 `config.ini` 读写

- **读取**（本簇）：`GameLauncherService.GetLocalGameVersionAsync` 用 `[GeneratedRegex("game_version=(.+)")]` 逐行匹配，与上游逐字；`GamePackageService.CheckPreDownloadFinishedAsync` 用 `predownload=(.+)`，同样逐字。
- **键映射**：`Features/GameLauncher/GameConfigIni.cs` 的 8 个 `[ConfigurationKeyName]`（`channel/sub_channel/cps/game_version/sdk_version/game_biz/uapc/downloading_mode`）与上游完全一致；单测 `GameConfigIniTests.PocoBindsAllUpstreamConfigurationKeys` 固定该契约。
- **写入**：上游唯一写入口是 `Starward.RPC/GameInstall/GameInstallService.cs:SetGameConfigIniAsync`（去掉含 `[General]` 的行 → `AddIniStream` → 设置键 → 输出 `"[General]\r\n"` + `key=value`）。本仓库对应职责由 `LunarWard.Games.GameConfigIniFile`（547 行，`ChannelValidator.ApplyChannel` 调用）承担，**语义差异**：

| 维度 | 上游 RPC 写入 | 本仓库 `GameConfigIniFile` |
|---|---|---|
| 段落处理 | 只保留非 `[General]` 行，写出时全部键塞进同一个 `[General]` | 保留其他段落并原样写回 |
| 键顺序 | `IConfigurationRoot.AsEnumerable()` 顺序（既有键在前、新键追加） | `CanonicalOrder` 固定顺序（`channel,cps,game_version,sub_channel,sdk_version,game_biz,uapc,downloading_mode`），未知键追加 |
| 行尾 | `AppendLine` → Windows 上 CRLF | 固定 `\n`（游戏原生格式） |
| 空/未设置键 | `config[key] = null` 会写入空值行 | 缺省键不写 |
| 写入原子性 | `File.WriteAllTextAsync` | `.tmp`+`fsync`+`rename`（`AtomicFile`） |

> 结论：读写口径「等价但非逐字节相同」。差异来自平台（Linux 行尾）与本地写入器更强的不丢字段保证；若验收要求与上游 **逐字节** 一致，需要把 `GameConfigIniFile.Generate` 的键序改为「配置文件原序 + 新键追加」并允许 CRLF——已列入 §3.4 待办（不阻塞，因为 `[General]` 段 game_version 与渠道三元组读取口径一致）。

### 2.2 启动链路（`GameLauncherService.StartGameAsync`）

| 环节 | 上游 | 本地 | 判定 |
|---|---|---|---|
| 参数拼装顺序 | `AppConfig.GetStartArgument` →（`EnableLoginAuthTicket`）`login_auth_ticket=` →（`GetUsePopupWindow`）`-popupwindow` →（`GetEnableDX12`）`-use-d3d12` →（hk4e）写 HDR 注册表 | 同左，逐行 | 一致 |
| 工作目录 | `Path.GetDirectoryName(exe)` | 同左 | 一致 |
| 命令行 | 单字符串 `Arguments`，交给 `CreateProcess`（Windows 自行解析引号） | `Arguments = "\"<exe>\" <arg>"` 单字符串交给 wine（**不按空格拆**） | 一致（修复过一版按空格拆的偏差） |
| 可执行文件 | 游戏 exe / 第三方工具（`runas` 提权） | 游戏 exe / 第三方工具；`.exe`/`.bat` → `wine`，其余原生直启 | 平台等价 |
| 环境注入 | Windows 由系统/显卡驱动承担 | `WINEPREFIX`（`GamePrefixContext.CurrentPrefix`）、`WINEARCH=win64`、`WINEDLLOVERRIDES=winemenubuilder.exe=d;mscoree,mshtml=`（与 `LaunchPlanFactory` Baseline 层一致）；ZZZ + DX12 追加 `DXVK_ENABLE_NVAPI=1`、`VKD3D_CONFIG=dxr`（`GameAdapter.BuildEnv`） | 平台等价（**本轮补齐**） |
| 进程探测 | `GetProcessesByName` + `SessionId == 当前会话` + 非挂起 | `Process.GetProcesses()` 同名（忽略大小写）+ 非挂起 | 平台等价 |
| 挂起判据 | 所有线程 `ThreadState.Wait` 且 `WaitReason.Suspended` | `/proc/<pid>/stat` 第 3 字段 `T` | 平台等价 |
| 时长统计 | `PlayTimeRecordService.StartProcessToLogAsync` | 不在此记录，由外壳 `Launch.PlayTimeService` 负责 | 已知差异（docs/09 §十二 #5） |
| 取消/失败 | `Win32Exception ERROR_CANCELLED` 只记日志；其它异常上抛 | 统一 `catch` 记日志返回 `null`（上游非取消异常会抛给调用方） | 轻微差异（更宽容），列 §3.4 |
| `cmd /c start` | `AppConfig.StartGameWithCMD` | 忽略（Linux 无 cmd） | 平台等价 |
| `GameState` 状态机 | `None→StartGame/InstallGame/UpdateGame/ResumeDownload/Installing/GameIsRunning/ComingSoon`，赋值点 5 处 | 逐处对应（`UpdateGameState`、`OnLoaded`、进程轮询、安装任务回调） | 一致 |

### 2.3 GameSelector 磁盘枚举与 HYP/图标缓存

- 磁盘枚举：`DirectoryInfo(installPath).EnumerateFiles("*", AllDirectories)` 求 `TotalSize`、多服重复文件去重逻辑与上游逐行一致（`serverCount > 1` 才计入 `_duplicateFiles`）。
- 硬链接/重复占用：上游 `Kernel32.GetFileInformationByHandleEx<FILE_ID_INFO>`（FileIdInfo 十六进制做 key）；本地 `stat` 的 `(st_dev, st_ino)` 做 key，`stat` 失败时退回 `(路径哈希, 长度)`（更宽容，上游会抛）。
- HYP：**已补齐** `HKEY_CURRENT_USER\Software\miHoYo\HYP\1_1\{biz}` / `Cognosphere\HYP\1_0\{biz}` 的 `GameInstallPath`（REG_SZ）回退，读到 Windows 盘符路径时换算到 `prefix/dosdevices/<drive>:`；命令改为 `AutoSearchInstalledGamesAsync`（wine 子进程不能阻塞 UI）。
- 图标缓存键：`AppConfig.CachedGameInfo`（JSON `List<GameInfo>`）与上游同一键；`HoYoPlayService.DownloadGameVersionPosterAsync` 的文件名规则逐字（`FileCache`/`FileHashHelper` 已对齐）。

### 2.4 GameAccount / GameAuthLogin 字段与路径

- `GameAccount`：`SHA256, GameBiz, Uid(long), Name, Value(byte[]), Time, IsSaved` 与上游逐字段一致；UID 三个注册表键（bh3 `GENERAL_DATA_V2_LastLoginUserId_h47158221` / hk4e `__LastUid___h2153286551` / hkrpg `App_LastUserID_h2841727341`）由 `WineAccountService.Read` 统一解析。
- `GameAccountService`：SQL 列序 `(SHA256, GameBiz, Uid, Name, Value, Time)` 与上游逐字；`GetSuggestionUids` 返回 `long`（上游 `int`），SQL 一致。
- `GameAuthLoginService`：`stoken`/`mid` 取自 `AppConfig.stoken`/`AppConfig.mid`（与上游同名属性），请求体 `{token:{token_type:1,token:stoken},refresh:true,mid}` 字段顺序与上游一致；成功后写回 `AppConfig.stoken` + `SaveConfiguration()`（仅当 token 变化）；设备指纹 `x-rpc-device_id`/`x-rpc-device_fp` 由本地复刻的 `UpdateDeviceFpAsync` 维护（3 天过期）。
- **cookie 路径**：本簇不做 cookie 持久化（上游登录态 cookie 也存在 `AppConfig`/WebView2 的 cookie 目录）；WebView 侧 cookie 目录由 `Frameworks/WebView`（`WebKitWebViewCore`）管理，属 task-2 范围，本簇无对应代码 → 无需对齐。

### 2.5 GamePackageService 清单解析 / 预下载判定

- `GetLocalGameVersionAsync`：`config.ini` 的 `game_version=(.+)`（Regex 单行，贪婪到行尾）。
- `CheckPreDownloadFinishedAsync`：`game_version` 与 `predownload` 两键都存在且 `predownload == game_version` 才算完成；该逻辑逐字照搬。
- `GetAudioLanguageAsync`/`SetAudioLanguageAsync`：四语言位标志 `Chinese=1, English=2, Japanese=4, Korean=8`，写回 `audio_language` 键的字符串形式；上游同款。
- 清单解析本身（`GamePackage`/`AudioPackage`/`DiffPackage` 下载地址拼接）在 `LunarWard.Core.HoYoPlay.GamePackage` 与 `GameInstallService`（RPC）里，与本文件无关。

---

## 3. 未对齐清单

### 3.1 已互换 / 属他人作用域

| 项 | 说明 |
|---|---|
| `GameInstallService.cs` | 本任务首版为进程内 `LunarWard.Installer` 适配；按 Lead 裁决**换手 rpc-port**（task-10），现落点是 `LunarWard.RPC.GameInstall.GameInstallService`（gRPC 服务端，与上游 `Starward.RPC` 同构）。`Features/GameInstall` 侧只保留 `GamePackageService` 与消息类，`GameLauncherSettingDialog` 的 388/445/579 三行由 rpc-port 接上游 gRPC 调用。 |
| `PlayTimeButton` / `CloudGameButton` / `DailyNoteButton` | 分别属 `Features/PlayTime`（task-12，已交付控件）、`Features/CloudGame`（task-8）、`Features/GameRecord/DailyNote`（task-5）。`GameLauncherPage.axaml` 中三处已注释并标 `TODO(跨任务)`，待对应簇落地后由 Lead 接线。 |
| `GameLauncherPage` 的 `AppBackground`/`BackgroundViewWindow` | 使用 `Features/Background`（task-13）与本簇协同，行为已对齐（`BackgroundService.ChangeCustomBackgroundFileAsync(IStorageFile)`）。 |

### 3.2 上游有、本地无（有意为之）

| 项 | 上游行为 | 本地 | 原因 |
|---|---|---|---|
| `NoAnimationImageExStyle.xaml` | `ImageEx` 无动画样式 | 无该文件 | Avalonia 无 `ImageEx`；`CachedImage` 自带缓存/占位，等价声明写进 `scripts/check-alignment.py` 的 `EQUIVALENTS` |
| `ProgressRing` / `AnimatedIcon` / `ThemeShadow` / `Segmented` / `NavigationView` / `FlipView` / `Pivot` / `ItemsRepeater` / `TeachingTip` | WinUI 控件 | `ProgressBar`/`TextBlock.icon`/`BoxShadow`/`ToggleButton`+`ListBox`/`ListBox`+`Carousel`/`Carousel`/`TabControl`/`ItemsControl`/`Popup` | 控件体系不同（docs/09 §十二） |
| Win2D HDR 预览（`CanvasSwapChainPanel`、`CanvasBitmap.LoadAsync`、`LinearTransferEffect`） | 真实亮度预览 + Mora 场景/UI 图层 | 静态 `Sign.png` + `Opacity` 随亮度变化的占位预览；场景区为灰底 + 说明文字 | Avalonia 无 Win2D；数值设置（300–2000 / 100–500 / 150–550）与写注册表逻辑完全保留 |
| 系统 HDR 能力检测（`DisplayInformation.AdvancedColorInfo`） | 检测显示器 HDR 支持/开启状态，显示两条 InfoBar | `HDRNotSupported`/`HDRNotEnabled` 恒 `false`（不显示警示条） | Linux 无 WinRT 高级颜色 API |

### 3.3 待办（登记，不阻塞验收）

| 项 | 影响 | 计划 |
|---|---|---|
| `CompactCheckBoxStyle.xaml` 只保留紧凑语义（375 → 32 行） | 视觉：未逐行复刻 WinUI CheckBox 模板（勾选框尺寸/状态动画） | `docs/10 §4.5` 已记；如需像素级一致，按下一条对齐任务处理 |
| `GameNoticeWindow` 无 `InputSiteSubclassProc` | 行为：点击窗口外不会自动关闭（上游会） | 需 Avalonia `Window.Deactivated` + 点击外部检测；登记 docs/09 §十二，优先级低 |
| `GameNoticeService.GetLastUid` 直查 SQL | 结构：与 task-5 的 `GameRecordService` 有重复 SQL | task-5 落地后换回 `GetLastSelectGameRecordRoleOrTheFirstOne` |
| `GameSelector` 硬链接判定的 fallback | 行为：`stat` 失败时按 `(路径哈希, 长度)` 去重（上游抛异常 → 尺寸为空） | 更宽容，保持；若要求严格一致可去掉 fallback |
| `GameLauncherService` 异常处理 | 行为：上游非取消异常上抛，本地统一吞掉记日志返回 `null` | 保持（Linux 启动失败场景更多，吞掉更利于 UI 恢复）；如有调用方依赖异常可改回 |
| `config.ini` 写入键序/行尾 | 行为：与上游 RPC 写入非逐字节一致（见 §2.1 表） | 若验收要求逐字节，改 `GameConfigIniFile.Generate` 保序 + CRLF 选项 |

---

## 4. 单测

新增目录 `tests/LunarWard.UnitTests/Features/GameLauncher/`（task-15 独占）：

| 文件 | 用例 | 覆盖点 |
|---|---|---|
| `GameConfigIniTests.cs` | 5 | POCO 的 8 个 `[ConfigurationKeyName]` 绑定；`Parse→Generate` 逐字节；幂等；未知键/其他段落保留；编辑后往返；行尾固定 `\n` |
| `GameLauncherServiceTests.cs` | 12（1×7 + 5） | `GetGameExeName` 的 7 个 biz→exe 映射（含 bilibili）；`BuildStartInfo` 的 wine 路径/单字符串命令行/工作目录/`WINEPREFIX`+`WINEARCH`；无 `wine` 配置时回退 `wine`；原生可执行直启；ZZZ-DX12 的 `DXVK_ENABLE_NVAPI`/`VKD3D_CONFIG` 且原神不加 |
| `GameSelectorModelTests.cs` | 6 | `GameBizIcon` 默认值（`MaskOpacity=1`、`TotalSize=0→null`）；`{TotalSize/GB:F2}GB`（1.00/1.50/5.00）；`IsSelected` 切换遮罩；`GameAccount.Uid` long 取值；`GamePostGroup.FromGameContent` 分组与 `Activity` 在前的排序 |
| `tests/LunarWard.UnitTests/WineRegistryTests.cs`（追加 3 个用例） | 3 | `ParseString`：REG_SZ 含空格路径；大小写不敏感 + 忽略其它类型；空值/本地化空值文案返回 `null` |

运行命令（树绿时）：

```bash
dotnet test tests/LunarWard.UnitTests/LunarWard.UnitTests.csproj --filter "FullyQualifiedName~GameLauncher"
```

当前状态：**树红，测试项目无法编译**（`tests/LunarWard.UnitTests` 引用 `LunarWard.csproj`）。阻塞项均属他人作用域：`Features/GameRecord/**`（WinUI 类型未换完）、`Features/Gacha/{ZZZGachaService,GachaLogPage}`、`Features/Codec/{ImageLoader,ImageSaver}`（依赖 `ImageInfo`）、`AppConfig.ServiceProvider.cs`（注册上述服务）。用 `-p:CustomAfterMicrosoftCommonTargets=/tmp/drop-others.targets` 排除后我的 6 个目录 **0 error / 0 AXN**，测试项目自身也 **0 error**（26 条错误全是上述级联）。

---

## 5. 验收状态

| 验收项 | 状态 |
|---|---|
| 上游 54 文件 → 本地落点 | 52 同名 + 1 等价声明（`NoAnimationImageExStyle.xaml`）+ 1 换手（`GameInstallService.cs` → `LunarWard.RPC`） |
| `python3 scripts/check-alignment.py --json` 中本簇 `missing` | 仅 `NoAnimationImageExStyle.xaml`（等价声明待 Lead 写入脚本 `EQUIVALENTS`） |
| `dotnet build src/LunarWard/LunarWard.csproj` | 我的 6 个目录 0 error / 0 AXN；整仓错误全在他人在途目录 |
| `dotnet test --filter "FullyQualifiedName~GameLauncher"` | 已写 23 个 GameLauncher/Selector 用例 + 3 个 `ParseString` 用例，Rider 语义检查 0 error；执行仍被公用的他人错误挡住（见下） |
| 当前验收阻塞（2026-10-26 17:35） | 全树约 30 error，全在他人在途文件：`Features/Setting/*`（`PageContext` 未 using、`SettingPage` 构造器变更被 `Views/Starward/StarwardShell.axaml.cs:350` 旧壳引用）、`Services/DialogService.cs`、`App.axaml.cs`（`NoPermissionWindow` ctor / `Log` 重载）、`Features/UrlProtocol/TestUrlProtocolWindow.axaml.cs(42,22)`、`Features/Update/{SetupService,UpdateWindow}`、`Features/GamepadControl/*`、`Features/Screenshot/ScreenCaptureInfoWindow`；我的 6 目录 + `WineRegistry.cs` 0 error / 0 AXN |
| 提交 | `984f317`（审计补齐：BuildStartInfo / HYP 回退 / WineRegistry.ReadStringAsync / docs14+docs09）、`a5e9f17`（他人落地后的编译回归修复：恢复三按钮、Avalonia 12 拖放/剪贴板/Launcher/TopLevel/FontIcon、RPC GameInstallContext）、`572121a`（`InternalsVisibleTo` + 测试类型修正） |
| 行为级差异 | 本文件 §1 矩阵逐文件登记；「上游有、本地无」2 处已补（启动环境注入、HYP 回退），其余见 §3 |

---

## 6. 本轮审计改动（已提交）

| 文件 | 改动 | 理由 |
|---|---|---|
| `src/LunarWard/Features/GameLauncher/GameLauncherService.cs` | 重写 `BuildStartInfo`：wine 二进制取 `GamePrefixContext.CurrentWineBinary`、单字符串命令行、注入 `WINEPREFIX/WINEARCH/WINEDLLOVERRIDES`、ZZZ-DX12 追加 DXVK/VKD3D 变量；`BuildStartInfo` 改 `public static`（可测缝）；调用点补 `gameId` 参数 | 审计发现原实现只注入 `WINEPREFIX`、按空格拆参数（会破坏带空格的参数）、忽略配置的 wine 二进制 |
| `src/LunarWard/Features/GameSelector/GameSelector.axaml.cs` | `AutoSearchInstalledGames`→`AutoSearchInstalledGamesAsync`，补 HYP 注册表回退（`ReadHypInstallPathAsync`，盘符路径换算 dosdevices）；类注释更新 | 补齐上游「上游有、本地无」的 HYP 分支 |
| `src/LunarWard/Features/GameLauncher/GameLauncherPage.axaml(.cs)` | 恢复上游 `PlayTimeButton`/`CloudGameButton`/`DailyNoteButton` 三处控件与绑定；拖放改 `DataTransfer`/`DataFormat.File`/`TryGetFiles`；复制背景改 `DataTransfer`+`Clipboard.SetDataAsync`；保存背景改 `TopLevel.Launcher.LaunchDirectoryInfoAsync`；四处对话框统一 `DialogService.ShowAsync(dialog, this)`；滚轮改 `PointerWheelEventArgs.Delta`；`DispatcherQueue`→`Dispatcher.UIThread.Post`；补 `Services`/`BackgroundService` 别名 | 此前的 "0 error" 是 filtered build 复用旧输出的假象；全树编译暴露 58 处 WinUI 残留 |
| `src/LunarWard/Services/WineRegistry.cs` | 新增 `ReadStringAsync`（REG_SZ）+ 纯函数 `ParseString` | Lead 批准（方案 A）：HYP `GameInstallPath` 读取需要 |
| `tests/LunarWard.UnitTests/Features/GameLauncher/*`、`tests/LunarWard.UnitTests/WineRegistryTests.cs` | 新增/追加 26 个用例 | task-15 单测要求 |
