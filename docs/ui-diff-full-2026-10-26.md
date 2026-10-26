# Starward → LunarWard 全量差异审计（2026-10-26）

本文回答「全面对比 Starward 源码，找出**所有**差异」。与 `docs/ui-audit-2026-10-26.md`（代码质量）和
`docs/08-UI-PARITY.md`（逐页完成度）互补：那两份看**实现质量与进度**，本文看**功能面对齐度**。

## 0. 证据约定

- 上游路径前缀：`/home/qie2035/projects/csharp/Starward/src/Starward/`，正文只写相对部分，
  如 `Features/GameLauncher/GameLauncherPage.xaml:667`。
- 我方路径前缀：仓库根，如 `src/LunarWard.App/Views/Starward/StarwardShell.axaml:86`。
- 行号取自 2026-10-26 工作区（上游 HEAD = 上游仓库当前 checkout，我方 git `b259b95`）。
- 类别：
  - **A 功能缺失**——上游有、我们完全没有（按用户影响排序）；
  - **B 行为不一致**——都有，但语义/流程/入口不同；
  - **C 有意偏离**——平台原因（WinUI/WIC/DWM/注册表/WebView2/GameInput），只登记，不要求修。
- 「已知项」不重复展开（清单见 §14），仅在需要上下文时引用。

---

## 1. 摘要

**总计 64 条：A 25 条 / B 25 条 / C 14 条。**
优先级：**P0 5 条、P1 17 条、P2 42 条**。

三条断层决定整体观感，其余都是修补：

1. **游戏覆盖只有 2/4**。上游 11 份 `GameFeatureConfig`（bh3_cn/global、hk4e_cn/global/bilibili、
   hkrpg_cn/global/bilibili、nap_cn/global/bilibili）→ 我方 `GameCatalog.cs:32-42,74-107` 只有
   hk4e_cn/hk4e_global/nap_cn/nap_global 四条。星穹铁道、崩坏3 从导航到启动全链路不存在。
2. **没有「按游戏裁剪界面」的机制**。上游靠 `CurrentGameFeatureConfig.SupportedPages` 逐项控制 7 个
   导航项的可见性和文案；我方 `ShellViewModel.cs:303-330` 只有 `IsXxxSelected` 八个选中态，
   全部恒显示。于是「千星奇域颂愿」在绝区零下照样出现——这不是某一个页面的 bug，是缺了一层。
3. **平台独占子系统整体没有替代品**：内置截图（`ScreenCapture*` 全链路）、游戏内悬浮层
   （`OverlayWindow`）、云游戏、RPC（gRPC）、手柄控制、自更新（`SetupService`）。

### 最该补的 10 条

1. 星穹铁道 / 崩坏3 支持：`GameCatalog.cs` 4 → 11 条，含 hkrpg/nap 的 bilibili 服（A，L）。
2. 引入 `GameFeatureConfig` 等价物（每游戏 SupportedPages + 6 个能力开关），接进 `NavigationRail`（A，M）。
3. 导航与页面文案随游戏变化：抽卡 Wish/Warp/SignalSearch、工具箱 Hyperion/HoYoLAB（A，M）。
4. 内置截图链路（`ScreenCaptureService/Helper` + 游戏窗口捕获 + 文件命名/EXIF）（A，L）。
5. 米游社 / HoYoLAB 抽卡同步（`SyncFromMiyousheAsync`，绝区零专用）（A，M）。
6. 设置-截图页从「只读展示」恢复为可配（格式 PNG/AVIF/JPEG XL、质量、HDR→SDR、自动复制）（A，M）。
7. 图标字体真正打包：`Styles/Starward.axaml:78` 只写字体名，没引用随包的 `Assets/Font/*.ttf`（A，S）。
8. 退出时自动备份数据库（上游 30s 上限），目前只在设置页手动备份（A，S）。
9. 工具箱子页语义对齐：上游 3 张卡片（UIGF4 导入导出 / 绝区零抽卡物品图鉴 / 图片查看器）（B，S）。
10. 关于页补 3 条外链：捐赠 / 隐私政策 / 问题反馈；发布历史已由「打开发布页」部分覆盖（A，S）。

---

## 2. 域一：窗口与外壳

上游 `Features/ViewHost/MainWindow.xaml` 只有 35 行，几乎所有窗口行为在 `Frameworks/WindowEx.cs`
（216 行）+ `App.xaml.cs`。我方对应 `Views/Starward/MainWindow.axaml`（64 行）+ `.axaml.cs`。

| 差异 | 类别 | 上游证据 | 我们的现状 | 建议与工作量 |
|---|---|---|---|---|
| 标题栏应用图标 + 图标右键系统菜单 | A | `Features/ViewHost/MainWindow.xaml.cs:48` `IconShowOptions.ShowIconAndSystemMenu` | `MainWindow.axaml:38-56` 自绘标题栏只有最小化/关闭两枚按钮，无图标、无系统菜单 | 标题栏左侧加 16×16 图标 + 点击弹「关于/关闭」菜单（S） |
| 退出时自动备份数据库 | A | `MainWindow.xaml.cs:128-135` `DatabaseService.AutoBackupToAppDataLocal`，`Task.WhenAny(backup, Delay(30000))` 限 30s | `MainWindow.axaml.cs:373-408` 退出只 `Config.Save`；备份仅在设置-文件管理手动触发（`SettingFileManageViewModel.cs:129`） | 退出路径挂备份 + 超时（S） |
| 会话切换/锁屏通知 | C | `MainWindow.xaml.cs:54` `WTSRegisterSessionNotification(WindowHandle, 0)` | 无（X11/Wayland 无等价 API；锁屏时窗口保持原状） | 不修，登记 |
| 托盘菜单项与文案 | B | `Features/ViewHost/SystemTrayWindow.xaml:34,46` 用 `Lang.MainPage_Launcer` / `Lang.Common_Exit`，每项带图标 | `Services/TrayService.cs:29-36` 只有两项，字符串**硬编码中文**「显示主窗口」「退出」，无图标 | 走 AppStrings + 加图标（S） |
| 标题栏拖拽区判定 | B | `MainWindow.xaml.cs:54` `SetDragRectangles(new RectInt32(0,0,100000,(int)(48*UIScale)))`——整条 48px 带交给系统 | `MainWindow.axaml.cs:141-160` 手写 `OnWindowPointerPressed`：只认最上面 48px，且跳过已 `Handled` 的控件 | 逻辑等价但存在「控件吃掉点击→拖不动」的死区；建议改成透明 drag layer（S） |
| 窗口阴影与边框 | C | DWM 系统阴影 + `WindowEx.ChangeAccentColor`（`Frameworks/WindowEx.cs`） | `MainWindow.axaml:24` `Border#RootBorder` 1px `#1AFFFFFF` 描边，无投影 | 不修（X11 无系统阴影），可选加 BoxShadow（S） |

注：`IsMaximizable=false` / `IsResizable=false` / 1200×676 两边**一致**（上游 `MainWindow.xaml.cs:57-61,69`，
我方 `MainWindow.axaml:1-12` + `CenterInScreen`），不列为差异。

---

## 3. 域二：导航与页面

我方结构：`StarwardShell.axaml`（464 行）+ `NavigationRail.axaml`（142 行）+ `PageHost`；
上游：`MainView.xaml`（152 行）单文件 `NavigationView` + `Frame` + `GameSelector`。

| 差异 | 类别 | 上游证据 | 我们的现状 | 建议与工作量 |
|---|---|---|---|---|
| 按游戏裁剪页面可见性 | A | `Features/ViewHost/MainView.xaml.cs:131-149` `UpdateNavigationView()` 用 `CurrentGameFeatureConfig.SupportedPages.Contains(...)` 逐项设 `Visibility` | `ViewModels/Starward/ShellViewModel.cs:303-330` 只有 8 个 `IsXxxSelected`，无任何可见性过滤；`NavigationRail.axaml` 7 项恒显示 | 新增 `GameFeatureConfig` 表 + `bool IsGachaVisible/IsBeyondGachaVisible/...` 绑定 `IsVisible`（M） |
| 星穹铁道 / 崩坏3 | A | `Features/GameFeatureConfig.cs`（293 行）11 份配置；`GameSelector` 列出 bh3/hkrpg/nap 全服 | `src/LunarWard.Games/GameCatalog.cs:32-42` `All` = 4 条，`:42` `Kinds = [genshin, zzz]`；`LunarWard.Core/GameBiz.cs:25-32` 仍认 bh3/hkrpg，但启动层不用 | 见 P0-1（L） |
| 导航文案随游戏变化 | A | `MainView.xaml.cs:135-148` 抽卡取 `WishRecords`/`WarpRecords`/`SignalSearchRecords`；工具箱按国服/国际服取 `HyperionToolbox`/`HoYoLABToolbox` | `Localization/AppStrings.cs` 只有单值 `Shell.Gacha`、`Shell.Toolbox`；文案不随 `GameChanged` 变 | 文案键改成按 biz 取值（M） |
| 无游戏空态页 | B | `MainView.xaml.cs:147` `CurrentGameId is null → NavigateTo(typeof(BlankPage))` | `Views/Starward/PageContracts.cs:8-18` `ShellPage` 8 个值，无 `Blank`；无游戏时停在启动器页 | 加空态页或空态遮罩（S） |
| 导航项顺序 | B | `MainView.xaml:103-138` 顺序：工具箱(F133) → 账号记录(E8C7) → 千星奇域颂愿(E776) | `NavigationRail.axaml:56-129` 顺序：gacha → selfquery → genshin-beyond-gacha → **toolbox 最后** | 与上游对齐两位（S） |
| 设置入口形态 | C | `MainView.xaml.cs:61-63` `args.IsSettingsInvoked` → 用 `NavigationView` 内建设置按钮 | `NavigationRail.axaml:130` 底部独立 `Button`，`Grid RowDefinitions="52,*,Auto"` | 语义等价，不修 |
| 导航选中态手绘 | B | `NavigationView` 默认 3×16 强调条 + reveal 高亮 + 悬停动画 | `NavigationRail.axaml:15-44` 自绘 40×40、`CornerRadius=4`、3×16 条 + `StarSelectionBrush` | 已很接近；可补悬停渐显（S） |
| 页面切换遮罩过渡 | B | `MainView.xaml:24-30` `Border_OverlayMask` 带 `OpacityTransition` | `StarwardShell.axaml:50-52` `Border_OverlayMask` 仅绑 `Opacity`，无过渡 | 加 `Transitions`（S） |

---

## 4. 域三：游戏启动页

上游 `Features/GameLauncher/GameLauncherPage.xaml` 684 行 + `StartGameButton.xaml` 412 行；
我方 `Pages/GameLauncherPage.axaml` 951 行 + `ViewModels/Starward/GameLauncherPageViewModel.cs`。

| 差异 | 类别 | 上游证据 | 我们的现状 | 建议与工作量 |
|---|---|---|---|---|
| 云游戏入口 | A | `Features/CloudGame/CloudGameButton.xaml` + `GameLauncherPage.xaml:667`；`GameFeatureConfig.SupportCloudGame` | 无云游戏按钮/服务；`AppStrings` 无对应键 | 本质是打开云游戏网页 + 会话 cookie，M 可做 |
| 游戏内悬浮层 | A | `Features/Overlay/OverlayWindow.xaml.cs:30-60` 置顶透明层跟随游戏 client rect、失焦即隐藏、`IsAlwaysOnTop`；由 `MainWindow.xaml.cs:247` 热键触发 | 无 `OverlayWindow`，无「叠在游戏上」的 UI | L；Wayland 下约束强，建议先做 X11 + gamescope（L） |
| 内置截图链路 | A | `Features/Screenshot/ScreenCaptureService.cs`、`ScreenCaptureHelper.cs`、`ScreenCaptureInfoWindow.xaml`；热键 `MainPage_GameScreenshot` | 只有游戏自身截图目录的**读取**（`LunarWard.Launch/ScreenshotService.cs`，注释明说「只做文件层面的发现与排序」） | L（与 §10 截图合并排期） |
| 「验证/修复游戏」入口位置 | B | 启动页内联：`LauncherPage_StartVerification`(:638)、`WouldYouLikeToVerifyTheFiles`(:641)、`LauncherPage_RepairGame`(:587) | 我方移到齿轮对话框 `GameLauncherSettingDialog.axaml:183` `RepairGame`；底层已实现（`LunarWard.Installer/GameInstallService.cs:362-365,750`） | 启动页补内联入口（S） |
| 右下角控件编组 | B | `GameLauncherPage.xaml:637-677`：账号切换 + 云游戏 + 授权登录 + 便笺 一字排开 | `GameLauncherPage.axaml:453` 便笺、`:764` 授权登录、`:292` 游玩时长；账号切换无独立控件（在 `GameLauncherPageViewModel.cs:1773-1842`） | 加独立账号切换按钮（M） |
| 游戏公告/资讯详情窗 | C | `Features/GameLauncher/GameNoticeWindow.xaml`（WebView2） | 走 `xdg-open` 系统浏览器 | 不修（已登记） |

一致项（不列差异）：公告卡三分类（活动/公告/资讯）、公告红点开关（`LauncherSetting.DisableNoticeRedDot`）、
DX12 开关显隐（`GameLauncherPageViewModel.cs:464` 复刻 `CheckDX12ConfigAsync`）、背景切换条
（图库/复制/另存为/暂停）、拖放换背景、预下载胶囊、游玩时长胶囊。
**「时区提示」在两边都没定位到**：`Lang.zh-CN.resx` 中 `LauncherPage_*` 无时区键，唯一相关文案在
抽卡 UIGF 里（`GachaImportAndExportWindow_TimeZoneOffsetWarning`）。见 §15。

---

## 5. 域四：游戏选择层

上游 `Features/GameSelector/GameSelector.xaml` 572 行；我方 `StarwardShell.axaml:86+` 内嵌遮罩。

| 差异 | 类别 | 上游证据 | 我们的现状 | 建议与工作量 |
|---|---|---|---|---|
| 首次使用引导 | A | `GameSelector.xaml:74-85` `TeachingTip#TeachTip_SelectGame`，动作按钮 `AutoSearchInstalledGamesCommand`，文案 `GameBizSelector_AutoSearch` | 无首次引导；`StarwardShell.axaml:258` 只有一行安装大小统计 | 加一次性 TeachingTip（S） |
| 卡片悬停交叉淡入 | B | `GameSelector.xaml` 卡片内 Logo/IconAndName 双 `Opacity` + 0.167s 动画 | `StarwardShell.axaml:88-94` 注释明写「只做遮罩淡入」 | 两图层交叉淡入（S） |
| 选择器占满整窗 + 拖拽时改窗口可拖区 | C | `Features/GameSelector/MainWindowDragRectAdaptToGameIconMessage.cs` 专门给「拖动游戏卡片改变窗口大小」发消息 | 选择器是固定窗口内的遮罩（`StarwardShell.axaml:86`），窗口不可缩放 | 不修（窗口固定尺寸） |

一致项：区服 flyout（`Placement=Bottom`）、pin 图标 E718/E841、已安装游戏条
（`GameIconsHost`）、自动搜索（`ShellViewModel.cs:449`）、刷新（`:492`）、安装大小统计（`:258`）。
上游**也没有**搜索框（`grep Search` 仅命中 `AutoSearch`），「搜索」这一项两边同样缺失。

---

## 6. 域五：设置

上游 9 个子页合计 1402 行 XAML；我方 `Views/Starward/Settings/` 9 个 View 合计 1844 行。
分组与顺序**完全一致**（关于/通用/下载/文件管理/截图/快捷键/手柄/高级 + 分隔线 + 工具箱）。

| 差异 | 类别 | 上游证据 | 我们的现状 | 建议与工作量 |
|---|---|---|---|---|
| 界面语言 12 种 | A | `Starward.Language/Lang.resx` 883 键 × 12 语言；`Lang.Designer.cs` 310KB | `Localization/AppStrings.cs` 2 种（zh-CN/en-US），各 1039 键 | 见 §13 专节（L） |
| 截图设置全部可配项 | A | `Features/Setting/ScreenshotSetting.xaml:113-130` AVIF/JPEG XL/无损/高/中 分段控件；`:151-153` HDR→SDR 同时保存；`AutoCopyScreenshotFile`、`ColorManagement`、`ClearThumbnailCache`、`TestScreenshot` | `SettingScreenshotViewModel.cs:14-19` 明确注释：「不改写游戏截图…格式由游戏决定，也没有 WIC 转码。因此这一页只读展示 + 打开目录」 | 先把「自动复制 / 清除缩略图」做成真实开关（M） |
| 快捷键-游戏截图 | A | `HotkeySetting.xaml` + `MainPage_GameScreenshot`、`SettingPage_KeyboardShortcuts` | `SettingHotkeyView.axaml` 只有「打开主窗口」+ Linux 桌面快捷方式；`Settings.Hotkey.HotkeyUnavailable` | 依赖 §4 内置截图（L） |
| 手柄控制 | A | `Features/Setting/GamepadControlSetting.xaml`（207 行）：6 键映射 + 长按 Guide + 双摇杆 + `GameInputRedist` 检测；`Features/GamepadControl/` | `SettingGamepadView.axaml` 全为只读说明：`Gamepad.KeptForParity` / `Gamepad.Unavailable` / `Gamepad.SteamInput` | 建议不做原生；把 Steam Input 指引做成可复制的配置片段（S） |
| 高级-RPC 服务 | A | `Features/Setting/AdvancedSetting.xaml` 运行/停止/测试 + `Features/RPC/RpcService.cs`（gRPC，`MainView.xaml.cs:80` 下发环境变量） | `SettingAdvancedView.axaml` `Advanced.RpcUnavailableNote`；`Services/StartupCommands.cs:9-18` 注释「上游走 Windows 命名管道，未移植」 | 明确不做（写进 C 类说明）或改 gRPC over UDS（L） |
| 关于页缺失外链 | A | `AboutSetting.xaml`（135 行）：`SettingPage_Donate`、`SettingPage_PrivacyPolicy`、`SettingPage_ReportIssue`、`SettingPage_ReleaseHistroy` | `SettingAboutView.axaml` 有 `About.CheckUpdate/OpenRelease/PreviewChannel`，缺捐赠/隐私/反馈三项 | 补 3 个链接（S） |
| 硬链接开关 | B | `DownloadSetting.xaml` `LauncherPage_HardLink` + `InstallGameDialog_HardLinkDescription` | `SettingDownloadView.axaml` `Settings.Download.HardLinkUnavailable`——但安装器其实实现了硬链接（`LunarWard.Installer/HardLink.cs`） | 开关接真实能力（S） |
| 重选数据目录 | B | `FileManageSetting.xaml:57` `SettingPage_ReselectDataFolder` | `Settings.Files.ReselectUnavailable` 只读行 | 可做：改 `Paths` 后重启（M） |
| 工具箱子页语义 | B | `Features/Setting/ToolboxSetting.xaml.cs:28-45` 三张卡片：UIGF4 导入导出 / 绝区零抽卡物品图鉴 / 图片查看器 | `SettingToolboxViewModel.cs:23-43` 四张：游戏设置 / 启动诊断 / 环境 shell / 截图 | 上游 3 项中 2 项我方有独立入口，可换成链接（S） |
| 系统动画/透明效果 | C | `GeneralSetting.xaml` `AppearanceSettingPage_AnimationEffects` / `TransparencyEffects` + 系统设置跳转 | `Settings.General.SystemEffectsUnavailable` | 不修 |
| 右侧内容区留白 | C | `SettingPage.xaml:102` `Frame Margin="24,24,0,0"` | `SettingPage.axaml:170-172` `Margin="24,24,24,24"`（注释说明为补偿底部压边） | 不修 |

一致项：关闭窗口行为三态、最小化到托盘、数据目录/缓存/日志统计、清缓存、备份数据库、删除全部设置、
清理启动器背景文件、语言切换、账号注册表切换开关（键存在但见 §10）。

---

## 7. 域六：游戏设置对话框（齿轮）

上游 `Features/GameLauncher/GameLauncherSettingDialog.xaml`（四页 `NavigationView` + `FlipView`）；
我方 `GameLauncherSettingDialog.axaml`（`ListBox` + `ContentControl`）。

| 差异 | 类别 | 上游证据 | 我们的现状 | 建议与工作量 |
|---|---|---|---|---|
| 游戏界面语言四选一 | B | `GameLauncherSettingDialog.xaml:339-354` `Segmented_SelectLanguage`（中/英/日/韩） | `GameLauncherSettingDialog.axaml:237` 只有 `SelectAudioLanguage`（语音语言），无语音/界面语言之分 | 补界面语言分段控件（S） |
| 分页容器与横向滑动 | C | `:128-144` `NavigationView` + `FlipView`，四页可左右滑 | `:55-61` `ListBox` 四行 + 单 `ContentControl` | 不修 |

一致项：基本信息（安装目录/大小/定位/卸载+二次确认/网络驱动器警告/重定位/修复/显示公告/红点开关/
用 CMD 启动/避免非法工具提示）、启动参数（命令行 + 更多信息外链 + 启动后动作三选）、自定义背景
（含视频）、游戏包体双 tab（`LatestPackagesTab` / `PrePackagesTab`）。

---

## 8. 域七：安装 / 更新 / 预下载 / 卸载

| 差异 | 类别 | 上游证据 | 我们的现状 | 建议与工作量 |
|---|---|---|---|---|
| 自更新 | A | `Features/Update/SetupService.cs:38-41` 拉 `ReleaseInfoDetail`（含 `InstallType`/架构/预览通道）→ 下载安装包替换自身 | `Services/ReleaseChecker.cs:37-49` 只查 `releases/latest` 并**打开浏览器**；注释明写「Linux 上走发行包，不替换自身」 | 保持 C 类策略，但把「有新版」做成主界面可见红点（S） |
| 更新日志上传 | A | `Features/Update/LogUploadService.cs` + `MainView.xaml.cs:81` 启动即上传 | 无（只有 `LunarWard.Installer/InstallerLogger.cs` 本地日志） | 可做「一键打包日志」（M） |
| 更新内容展示窗 | C | `UpdateWindow.xaml:287` WebView2 + `StackPanel_Loading`(:228)/`StackPanel_Error`(:244)/`Button_Retry`(:256) | 打开 GitHub release 页 | 不修 |
| 分卷 / 断点续传 / 差分 | B（我方更强） | `Features/GameInstall/GamePackageService.cs` + `GameInstallService.cs`，整包下载 + 校验 | `LunarWard.Installer/` 30 个文件：`DiffPatcher`、`SophonManifest`、`FileSliceStream`、`TokenBucketRateLimiter`、`ParentProcessWatcher` | 无需对齐，反向领先 |
| 预下载 / 卸载流程 | B | `PreDownloadButton.xaml`、`PreDownloadDialog.xaml`；卸载入口在齿轮对话框内 | `PreDownloadDialog.axaml`、`GameUninstaller.cs` 齐备，但卸载确认后的「保留用户数据」选项语义待逐字核对 | S |

一致项：预下载胶囊与对话框、分卷与校验（我方另有差分）、卸载二次确认。

---

## 9. 域八：抽卡记录

上游 `Features/Gacha/` 40 个文件；我方 `Pages/GachaPage.axaml`（502 行）+ `GachaPageViewModel.cs`。

| 差异 | 类别 | 上游证据 | 我们的现状 | 建议与工作量 |
|---|---|---|---|---|
| 米游社 / HoYoLAB 同步 | A | `GachaLogPage.xaml:64-86` `MenuFlyoutItem_SyncFromMiyoushe(All)` / `SyncFromHoYoLAB(All)` + `CloudGame`；`.xaml.cs` `SyncFromMiyousheAsync` 仅绝区零，走 `GameRecordService.GetGameRoles` + `zzzGachaService.GetGachaLogByGameRecordAsync` 带进度与取消 | `GachaPageViewModel.cs` 只有 `FetchCommand` / `RelocateCommand`（读本地日志），无米游社/HoYoLAB 通道 | 绝区零先做（M） |
| 云游戏抽卡记录 | A | `Features/Gacha/CloudGameGachaWindow.xaml` | 无（云游戏整体缺失） | 随 §4 云游戏一并（M） |
| 绝区零抽卡物品图鉴 | A | `Features/Gacha/ZZZGachaToolbox/ZZZGachaInfoWindow.xaml` + `ItemList.cs` + `IconInfo.cs` | 只有 `GachaIconCache` 回填图标（`GachaPageViewModel.cs:672-710`），卡片 28×28 小图；无独立图鉴窗 | M |
| 抽卡 URL 输入方式 | B | `GachaLogPage.xaml:84` `MenuFlyoutItem_InputUrl`（正常菜单项） | `GachaPage.axaml:479` `Button_InputUrlHost` 是隐藏的 0×0 宿主，靠 `StartupCommands` 注入 | 变成可见的「手动输入 URL」（S） |

一致项：UIGF v4.4/4.2 导入导出（`Uigf4GachaWindow.axaml`）、UIGF3 旧档导入（`GachaLegacyImport.cs`）、
SRGF（`GachaExport.cs` / `GachaLogStore.cs`）、多 UID（`GachaPageViewModel.cs:203` `HasUids`、`:662` `SelectUid`）、
抽卡语言切换、缓存目录打开、按时间删除、统计卡片、`GachaNoUp` 保底表（`LunarWard.Core/Gacha/GachaNoUp.cs`）。

---

## 10. 域九：便笺 / 工具箱 / 截图 / 账号 / 战绩

| 差异 | 类别 | 上游证据 | 我们的现状 | 建议与工作量 |
|---|---|---|---|---|
| 便笺只覆盖 2 个游戏 | A | `Features/GameRecord/DailyNote/DailyNoteButton.xaml:98,149,377` 四套 `Grid_BH3`/`Grid_Genshin`/`Grid_StarRail`/`Grid_ZZZ`；`GameRecordService.cs` `GetBH3DailyNoteAsync` | `ViewModels/Starward/DailyNoteButtonViewModel.cs` 只有 `GenshinDailyNoteExtra` / `ZzzDailyNoteExtra` | 加星铁开拓力/实训/历战余响（M） |
| 便笺呈现形态 | B | 启动页内 `sfgd:DailyNoteButton` 单飞出一层 | 启动页飞出 **+** 独立 `Views/DailyNoteWindow.axaml` 窗口 | 双入口保留，注意状态同步（S） |
| 战绩/工具箱用系统浏览器 | C | `Features/GameRecord/BattleChronicleWindow.xaml`（WebView2 + `BBSWebBridge.xaml.cs`） | `ToolboxPageViewModel` 走 `xdg-open` | 不修 |
| 米游社 BBS 内嵌桥 | A | `Features/GameRecord/BBSWebBridge.xaml.cs`（484 行，含 cookie/时区注入） | 无 | L，且依赖 WebKitGTK 内嵌，建议不做 |
| 截图批量转换 / EXIF / 缩放 | A | `Features/Screenshot/ImageBatchConvertWindow.xaml`、`ImageViewWindow2.xaml`、`Features/Codec/`（8 文件：HDR10/SDR gamma、增益图、VP9） | `Views/Starward/ImageViewWindow2.axaml` 单图查看；无批量、无 HDR/ICC、无 AVIF/JPEG XL 编码 | L（先做批量删除/导出） |
| 账号切换设置项未接线 | B | `GeneralSetting.xaml` `SettingPage_RegistryBasedGameAccountSwitchingFeature` | 键存在（`Settings.General.RegistrySwitcher`），但 `GameLauncherPageViewModel.cs:1776` 注释「LunarWard 没有这条设置」，读的是 `Config.Launcher.EnableGameAccountSwitcher` | 设置项写回配置（S） |
| 游玩时长图表 | B | `Features/PlayTime/Charts/` 5 个自绘（`BarChart`、`CalendarHeatmap`、`HoverCard`…） | `LunarWard.Launch/PlayTimeStatsCalculator.cs` + `PlayTimeStatsDialog.axaml`（无日历热力图/悬停卡） | 补热力图（M） |

一致项：工具箱 14 个菜单项（原神 5 / 星铁 6 / 绝区零 3，含 `ChallengePeak`＝上游 `AnomalyArbitration`）——
`ToolboxPageViewModel.cs:484-510`；13 个战绩子页 1:1 移植；`NavigationViewItem_UpdateDeviceInfo`
（更新设备指纹）我方有（`ToolboxPageViewModel` 指纹更新）。

---

## 11. 域十：基础设施

| 差异 | 类别 | 上游证据 | 我们的现状 | 建议与工作量 |
|---|---|---|---|---|
| 多语言 | A | 独立项目 `src/Starward.Language/`：12 份 `.resx` + `Lang.Designer.cs`；`Starward.csproj:69` 项目引用 | `Localization/AppStrings.cs` 单文件双字典；Core 侧仍留 13 语言 `CoreLang.*.resx` | 见 §13 |
| 响应缓存 | B | `Helpers/FileCache.cs:15-20` FusionCache（`ZiggyCreatures.Caching.Fusion`）+ XxHash64 + 2 次重试；`GameRecordService`/`HoYoPlayService` 用 `IMemoryCache`（`AppConfig.ServiceProvider.cs:53`） | 无 `IMemoryCache`、无 FusionCache；只有 `GachaIconCache`、`StarwardAssets` 等专项目缓存 | 抽一层 `ICache`，先解决跨游戏串号（M，与 P0-5 相关） |
| 数据访问 | B | `Dapper`（`Starward.csproj:37`） | `LunarWard.Launch/Database/` 手写 SQLite（`LunarWardDatabase.cs`、`PlayTimeStore.cs`、`GachaLogStore.cs`） | 不迁移，补仓储层测试（S） |
| 配置存储 | C | `AppConfig.Configuration.cs`（registry + ini + json 三层） | 单一 JSON（`ProfileConfig` + `Config.Save`） | 不修 |
| 单实例 | C | `App.xaml.cs:86-91` `AppInstance` + `RedirectActivationToAsync`；退出 `UnregisterKey()` | `Services/SingleInstance.cs` Unix domain socket `$TMPDIR/lunarward-{user}.sock`，协议一行 `activate` | 不修；补「socket 残留」自愈（S） |
| 依赖栈 | C | 12 个 Windows-only 包：WindowsAppSDK、Win2D、ComputeSharp、Vanara.PInvoke ×4、H.NotifyIcon、H.InputSimulator、Starward.Codec/GameInput/NativeLib/Win2D | Avalonia 栈 + `Polly`、`ZstdSharp.Port`、`DialogHost.Avalonia`、`Iciclecreek.Avalonia.Terminal`、`Starward.Assets`（同名内容包） | 登记 |
| 日志 | B | Serilog + `LogUploadService`（上报） | Serilog 文件日志 + `InstallerLogger` | 见 §8 |

一致项：`Starward.Assets` 内容包 0.6.5（图标/区服 logo/字体同源）、MiniExcel、`Serilog.Sinks.File`、
CDN 域名、`UrlProtocol`（`LunarWard.Launch/UrlProtocol.cs`）。

---

## 12. 域十一：视觉细节

| 差异 | 类别 | 上游证据 | 我们的现状 | 建议与工作量 |
|---|---|---|---|---|
| 图标字体真正打包 | A | `App.xaml:66-70` `ms-appx:///Assets/Font/Segoe Fluent Icons.ttf#Segoe Fluent Icons` 作为资源引用，全应用默认字体 | `Styles/Starward.axaml:78` `StarFluentIcons`＝字符串 `"Segoe Fluent Icons, Segoe MDL2 Assets"`，**未用 `avares://` 引用随包的 `Assets/Font/*.ttf`**；`Program.cs:249` `DefaultFamilyName = "Segoe UI"` | 目标机器未装字体时全部 glyph 变豆腐块；改成 `avares://LunarWard.App/Assets/Font/...`（S，收益高） |
| 主题色（accent） | B | `AppConfig.Setting.cs:63-67` `AccentColor` + `Helpers/AccentColorHelper.cs`（跟随系统 / 自定义） | `Styles/Starward.axaml` 固定 `StarAccentBrush`，无设置项 | 加「强调色」下拉（M） |
| 亚克力 / Mica | C | `Helpers/SystemBackdropHelper.cs`、`TransparentBackdrop.cs` | 静态画刷 + 背景图/视频 | 不修 |
| 动画 | B | `CommunityToolkit.WinUI.Animations` + 多处 `OpacityTransition`/隐式动画 | 少量 `Style` 过渡（`StarwardShell.axaml:88-94` 等） | 按页面逐步加（S/页） |
| 中文回退字体栈 | B | `App.xaml` 默认字体 + 系统中文回退 | `Styles/Starward.axaml:77` `"Segoe UI, Microsoft YaHei UI, Noto Sans CJK SC, Source Han Sans SC, sans-serif"` | Linux 上落到 Noto CJK，可接受；建议把 `Inter` 放队首并显式列出（S） |

一致项（已登记）：8px 圆角、版本亮点海报、右侧暗角、标题栏遮挡、区服 logo、开始按钮文案。

---

## 13. i18n 方案对比（专节）

### 13.1 事实

| 维度 | 上游 | 我方 |
|---|---|---|
| 载体 | 独立项目 `src/Starward.Language`：`Lang.resx` + 11 份语言 resx + `Lang.Designer.cs`（310KB） | `src/LunarWard.App/Localization/AppStrings.cs`（2181 行）单文件双 `Dictionary<string,string>` |
| 键数 | **883**（`grep -c '<data name=' Lang.resx`） | **1039**（zh-CN/EnUs 各 1039，键集完全相等） |
| 语言数 | **12**：base + de-DE / es-ES / it-IT / ja-JP / ko-KR / ru-RU / th-TH / vi-VN / zh-CN / zh-HK / zh-TW | **2**：zh-CN / en-US（`AppStrings.cs:2135` `SupportedLanguages`） |
| 强类型 | 是（`public static string Xxx`，XAML 用 `{x:Bind lang:Lang.Key}`，`Program.cs:1` `global using Starward.Language;`） | 否（索引器 `Strings["Key"]`，`:2142`） |
| 缺键行为 | 走 resx 回退链（语言 → 中性 → 键名） | 返回键名本身（`:16-18` 注释：漏翻译立刻显形） |
| 热切换 | 需重启（`LanguageChangedMessage` 广播） | 原地刷新（`INotifyPropertyChanged`） |
| 另一套机制 | Core 另有 `CoreLang.*.resx` **13 语言** | 同左：`src/LunarWard.Core/Localization/CoreLang.*.resx` 13 语言，与上游同源 |

### 13.2 结论

**不建议为「多语言」迁移到 resx；建议保持 C# 字典，但补三件事。**

理由：

1. **迁移不解决真正的问题**。真正的缺口是「只有 2 份翻译」，不是「用什么格式存」。上游有 12 种，
   收益来自翻译资源本身（人力/外包），把 1039 键从字典搬到 resx 只是换容器。
2. **resx 的强类型收益在 Avalonia 下打折**。上游 `{x:Bind lang:Lang.Key}` 依赖 WinUI 编译期绑定；
   Avalonia 侧我们已经在用 `{Binding [Key], Source={x:Static loc:AppStrings.Instance}}`，强类型只能
   换来「键名拼错编译期报错」，而当前「缺键返回键名」已经在运行时把问题暴露在同一屏。
3. **resx 的隐性成本更高**：satellite assembly、`Lang.Designer.cs` 310KB 的生成物、每次改文案触发
   重新生成、XAML 里无法做 `string.Format` 的参数化（上游得靠 `x:Bind` + 代码拼）。字典方案可以
   直接放 `{0}` 占位（我们已有 `Settings.Common.OpenFailed` 等多处 `string.Format`）。

迁移成本（若仍要迁）：键导出/比对脚本 0.5 人日 + Designer 生成与 csproj 改造 1 人日 + XAML 绑定批量改写
1.5 人日 + 回归 1 人日 ≈ **4–5 人日**，且**不产生任何新语言**。

收益：键名拼写错误在编译期暴露；与 Core 层 `CoreLang` 机制统一；外部译者可用 Poedit/ResX Manager。

### 13.3 建议落地（成本低、收益直接）

- **S1**：加一个键一致性测试（`zh`/`en` 键集相等）。当前天然相等（1039==1039），加测试防止回归。
- **S2**：加导出脚本 `scripts/export-lang.{sh,ps1}` → `.resx` 或 `.po`，让外部译者不必改 C#。
- **S3**：语言回退链：`zh-HK`/`zh-TW` → `zh-CN`，`de/es/it/...` → `en-US`（现在只有精确匹配两值）。
- **S4**（可选）：把 `AppStrings` 拆成 `AppStrings.zh-CN.cs` / `AppStrings.en-US.cs` 两个 partial，
  消除 2181 行单文件（纯工程可读性）。

**一句话**：i18n 不是「格式选错」，是「语言没翻译」；把容器换成 resx 是 4–5 人日的零收益重构，
把钱花在翻译和 S1–S3 上。

---

## 14. 已知但不在本轮范围

以下条目已修或已登记，本文不重复计分（引用处仅作上下文）：

1. 右侧暗角 / 切换条命中 / 标题栏遮挡 / 开始按钮文字 / 窗口圆角。
2. VLC → ffmpeg 背景视频。
3. 区服 logo 改用 `Starward.Assets` 内容包。
4. 版本亮点海报层、设置页工具箱裁切、包体版本字段。
5. `GameChanged` 双触发、固定图标持久化（`GameIconsPinned` 只写不读）、资讯串游戏、工具箱串星铁。
6. i18n 用 C# 字典（→ 已在 §13 单列，结论：不迁移）。
7. `docs/ui-audit-2026-10-26.md` 的 P0 7 条 / P1 9 条 / P2 5 条（代码质量，非功能差异）。
8. 战绩/公告用系统浏览器打开（`xdg-open`）——C 类，已登记。
9. 安装器下载/超时用例偶发不稳定（`docs/08-UI-PARITY.md` §6）。

---

## 15. 未能验证 / 存疑

- **「时区提示」**：用户提到启动页有时区提示。上游 `Lang.zh-CN.resx` 的 `LauncherPage_*` 段无时区键，
  我方 `AppStrings` 也无 `Launcher.*Timezone`；唯一相关是抽卡 UIGF 的时区偏移提示
  （`GachaImportAndExportWindow_TimeZoneOffsetWarning`）。**可能是对上游旧版本或另一处的记忆**，
  未计入差异条数。
- **`AppConfig.ShowNoviceGacha` / `ShowChronicledWish`**（`AppConfig.Setting.cs:45-55`）：我方有
  `GachaNoUp` 保底数据，但未确认抽卡页是否有「显示新手池/集录祈愿」开关，未计分。
- **`Settings.General.PendingEffect`**：疑似「该设置下次启动生效」提示，未逐字核对语义。
- 上游 `Frame` 的页面缓存策略（`NavigationCacheMode`）未展开核对，未计分。

---

## 16. 与既有文档的关系

- `docs/08-UI-PARITY.md`：逐页完成度（🟡/✅），关注「做到哪一步」。
- `docs/ui-audit-2026-10-26.md`：代码质量缺陷（P0-1…P0-7），关注「实现对不对」。
- **本文**：功能面「有没有 / 一样不一样」，关注「上游有而我们缺什么」。
- `docs/09-LINUX-DIVERGENCES.md`：本文所有 C 类的技术背景（Wine/Wayland/无 WinUI）。

三者不重叠，可同时作为验收基准：完成一页 = 完成度 ✅ + 无 P0 + 本文对应行归零。
