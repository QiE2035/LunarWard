# 15 · 文案与上游 Lang 的对齐（i18n parity）

> 目标（见 `docs/10-ALIGNMENT.md` §4）：继续使用上游 `Starward.Language.Lang` 资源（`src/LunarWard.Language/Lang.resx` 及其附属程序集），
> 逐步替换本仓库的 `LunarWard.Localization.AppStrings`，最终删除 `AppStrings`。
> 本轮（task-16）先完成试点目录 `src/LunarWard/Features/Setting/`，并在本文记录映射表与后续迁移手册。

## 1. 两套资源现状

| 资源 | 文件 | 中性语言 | 其他语言 | 条目数 | 使用方式 |
| --- | --- | --- | --- | --- | --- |
| 上游体系 | `src/LunarWard.Language/Lang.resx` | **英文** | 11 个 `Lang.<culture>.resx`（de/it/ja/ko/ru/th/vi/zh-CN/zh-HK/zh-TW/en） | 879 | `{x:Static lang:Lang.Key}` / `Lang.Key` |
| 本仓库遗留 | `src/LunarWard/Localization/Resources.resx` | 中文 | `Resources.en-US.resx` | 1043 | `AppStrings` 索引器 / `{Binding Strings[...]}` |

结论：`Lang` 才是对齐上游的源；`AppStrings` 只是过渡桥，**不得新增条目**，迁移一处删一处。

## 2. 试点：Features/Setting

`Features/Setting/` 里原有 **225** 个 `AppStrings` 键的引用；本轮把其中 **96** 个键替换成上游 `Lang`（`101` 处 XAML 绑定、`26` 处代码引用）。

### 2.1 映射表

| AppStrings 键 | 上游 Lang 键 | 中文值（本仓库） |
| --- | --- | --- |
| `Screenshot.CopyPath` | `ImageViewWindow2_CopyPath` | 复制路径 |
| `Screenshot.OpenDirectory` | `ScreenshotPage_OpenFolder` | 打开目录 |
| `Screenshot.Title` | `MainPage_GameScreenshot` | 游戏截图 |
| `Settings.About` | `SettingPage_About` | 关于 |
| `Settings.About.CheckUpdate` | `SettingPage_CheckForUpdate` | 检查更新 |
| `Settings.About.Donate` | `SettingPage_Donate` | 赞助开发者 |
| `Settings.About.Homepage` | `SettingPage_ProjectHomepage` | 项目主页 |
| `Settings.About.Issues` | `SettingPage_ReportIssue` | 报告问题 |
| `Settings.About.Links` | `AboutSettingPage_Links` | 相关链接 |
| `Settings.About.PreviewChannel` | `SettingPage_JoinPreviewReleaseChannel` | 加入预览版更新渠道 |
| `Settings.About.Privacy` | `SettingPage_PrivacyPolicy` | 隐私策略 |
| `Settings.About.Releases` | `SettingPage_ReleaseHistroy` | 更新记录 |
| `Settings.About.Version` | `SettingPage_CurrentVersion` | 当前版本 |
| `Settings.Advanced` | `SettingPage_Advanced` | 高级 |
| `Settings.Advanced.RegisterUrlProtocol` | `SettingPage_RegisterURLProtocol` | 注册 URL 协议 |
| `Settings.Advanced.RpcService` | `SettingPage_RPCService` | RPC 服务 |
| `Settings.Advanced.TestUrl` | `SettingPage_TestURLProtocol` | 测试 URL 协议 |
| `Settings.Advanced.UrlProtocol` | `SettingPage_URLProtocolExperimental` | URL 协议（实验中） |
| `Settings.Advanced.UrlProtocolDesc` | `SettingPage_URLProtocolDesc` | Linux 用 .desktop 的 MimeType=x-scheme-handler/lunarward 注册，不是注册表。支持 lunarward://open、launch、shell、gacha。 |
| `Settings.AppSettings` | `SettingPage_AppSettings` | 应用设置 |
| `Settings.Common.Cancel` | `SelectDirectoryPage_Cancel` | 取消 |
| `Settings.Common.ChangeFolder` | `Common_ChangeFolder` | 修改文件夹 |
| `Settings.Common.Confirm` | `SelectDirectoryPage_OK` | 确定 |
| `Settings.Common.CopyPath` | `ImageViewWindow2_CopyPath` | 复制路径 |
| `Settings.Common.Disabled` | `Common_Disabled` | 已禁用 |
| `Settings.Common.Enabled` | `Common_Enabled` | 已启用 |
| `Settings.Common.Locate` | `LauncherPage_Locate` | 定位 |
| `Settings.Common.Open` | `Common_Open` | 打开 |
| `Settings.ControllerEnhancement` | `SettingPage_ControllerEnhancement` | 手柄增强 |
| `Settings.Download` | `SettingPage_Download` | 下载 |
| `Settings.Download.HardLink` | `LauncherPage_HardLink` | 硬链接 |
| `Settings.Download.HardLinkDesc` | `InstallGameDialog_HardLinkDescription` | 创建硬链接，占用极小的额外存储空间，以安装多个不同区服的游戏客户端。 |
| `Settings.Download.InstallPath` | `DownloadSettingPage_DefaultGameInstallationPath` | 默认安装路径 |
| `Settings.Download.InstallPathDesc` | `DownloadSettingPage_DefaultGameInstallationPathDescription` | 游戏将被安装在不同的子文件夹中，修改默认路径不会移动已安装的游戏文件。 |
| `Settings.Download.SpeedLimit` | `DownloadSettingPage_SpeedLimit` | 速度限制 |
| `Settings.Download.SpeedLimitDesc` | `DownloadSettingPage_TheNumber0MeansNoSpeedLimit` | 数字 0 代表不限速 |
| `Settings.FileManagement` | `SettingPage_FileManagement` | 文件管理 |
| `Settings.Files.BackupDatabase` | `SettingPage_BackupDatabase` | 备份数据库 |
| `Settings.Files.Browser` | `FileSettingPage_Browser` | 浏览器 |
| `Settings.Files.Cache` | `FileSettingPage_Cache` | 缓存 |
| `Settings.Files.ClearCache` | `FileSettingPage_ClearCache` | 清理缓存 |
| `Settings.Files.ClearCacheTitle` | `FileSettingPage_ClearCache` | 清理缓存 |
| `Settings.Files.ClearLauncherBackground` | `FileManageSetting_CleanUpLauncherBackgroundFiles` | 清理启动器背景文件 |
| `Settings.Files.DataFolder` | `FileSettingPage_DataFolder` | 数据文件夹 |
| `Settings.Files.DataFolderImportant` | `FileSettingPage_DataFolderIsImportant` | 您的个人数据保存在下面的文件夹中，它很重要，请不要随意删除。 |
| `Settings.Files.DataHome` | `FileSettingPage_DataFolder` | 数据目录 |
| `Settings.Files.DeleteAllSettings` | `SettingPage_DeleteAllSettings` | 删除所有设置 |
| `Settings.Files.GameResource` | `FileSettingPage_GameResource` | 游戏资源 |
| `Settings.Files.Image` | `FileSettingPage_Image` | 图片 |
| `Settings.Files.LauncherBackground` | `FileManageSetting_LauncherBackground` | 启动器背景 |
| `Settings.Files.Log` | `SettingPage_Log` | 日志 |
| `Settings.Files.OpenLogFolder` | `SettingPage_OpenLogFolder` | 打开日志文件夹 |
| `Settings.Files.ReselectDataFolder` | `SettingPage_ReselectDataFolder` | 重新选择数据文件夹 |
| `Settings.Gamepad.BothThumbsticks` | `GamepadControlSetting_BothThumbsticksDesc` | 同时按下左右摇杆键，激活手柄模拟键鼠功能。 |
| `Settings.Gamepad.ButtonMapping` | `GamepadControlSetting_ButtonMapping` | 按键映射 |
| `Settings.Gamepad.EnterKeyText` | `GamepadControlSetting_EnterKeyText` | 输入按键文本 |
| `Settings.Gamepad.GameScreenshot` | `GamepadControlSetting_GameScreenshot` | 游戏截图 |
| `Settings.Gamepad.GuideButton` | `GamepadControlSetting_GuideButton` | 导航键 |
| `Settings.Gamepad.KeyboardKeys` | `GamepadControlSetting_KeyboardKeys` | 键盘按键 |
| `Settings.Gamepad.LongPressGuide` | `GamepadControlSetting_LongPressGuideButtonDesc` | 长按导航键，打开 Starward 主窗口。 |
| `Settings.Gamepad.MappingGuideDesc` | `GamepadControlSetting_ButtonMappingGuideDesc` | • 映射导航键时，Xbox Game Bar 的默认快捷操作将被禁用；其他程序（如 Steam 的大屏幕模式）也可能响应导航键，如有冲突需手动关闭相关功能。 |
| `Settings.Gamepad.MappingKeyboardDesc` | `GamepadControlSetting_ButtonMappingKeyboardDesc` | • 映射为键盘按键时，请输入键盘按键上的文字，多个按键之间用空格分隔。例如，将按键映射为“Ctrl + Alt + F1”组合键，应输入“Ctrl Alt F1”，小键盘按键需在文字前添加 Pad，例如 Pad8，Pad+ 。需要以管理员权限运行 Starward，才能在游戏中使用此功能。 |
| `Settings.Gamepad.MappingShareDesc` | `GamepadControlSetting_ButtonMappingShareDesc` | • 按下分享键仍会触发 Xbox Game Bar 的截图功能，暂时没有好的解决办法。 |
| `Settings.Gamepad.NoAction` | `GamepadControlSetting_NoAction` | 无操作 |
| `Settings.Gamepad.ShareButton` | `GamepadControlSetting_ShareButton` | 分享键 |
| `Settings.Gamepad.SimulateInput` | `GamepadControlSetting_SimulateInput` | 模拟输入 |
| `Settings.Gamepad.SimulateInputDesc` | `GamepadControlSetting_SimulateInputDesc` | 使用手柄模拟键鼠操作。Starward 需要管理员权限；LunarWard 没有等价的按键注入，因此开关保留位置并禁用。 |
| `Settings.Gamepad.XboxOnly` | `GamepadControlSetting_OnlySupportXboxControllerProtocol` | 仅适配 Xbox 协议手柄，不能保证其他类型手柄的使用体验。 |
| `Settings.General` | `SettingPage_General` | 通用 |
| `Settings.General.CloseWindowOption` | `ExperienceSettingPage_CloseWindowOption` | 关闭窗口选项 |
| `Settings.General.ExitCompletely` | `ExperienceSettingPage_ExitCompletely` | 完全退出 |
| `Settings.General.HelpTranslate` | `AppearanceSettingPage_HelpUsTranslateStarward` | 帮助我们翻译 |
| `Settings.General.MinimizeToTray` | `ExperienceSettingPage_MinimizeToSystemTray` | 最小化到系统托盘 |
| `Settings.General.RegistrySwitcher` | `SettingPage_RegistryBasedGameAccountSwitchingFeature` | 基于注册表的游戏账号切换功能 |
| `Settings.General.SystemEffectsIntro` | `SettingPage_ForABetterExperiencePleaseEnableTheFollowingFeaturesInSystemSettings` | 为了获得更好的体验，请在系统设置中启用以下功能： |
| `Settings.Hotkey.ClickToSet` | `HotkeyInput_ClickToSetShortcutKeys` | 点击设置快捷键 |
| `Settings.Hotkey.Editing` | `HotkeyInput_PressTheShortcutKeys` | 按下快捷键… |
| `Settings.Hotkey.GameScreenshot` | `GamepadControlSetting_GameScreenshot` | 游戏截图 |
| `Settings.Hotkey.OpenMainWindow` | `HotkeySetting_OpenMainWindow` | 打开主窗口 |
| `Settings.KeyboardShortcuts` | `SettingPage_KeyboardShortcuts` | 快捷键 |
| `Settings.Language` | `SettingPage_Language` | 语言 |
| `Settings.Screenshot` | `MainPage_GameScreenshot` | 截图 |
| `Settings.Screenshot.AutoCopy` | `ScreenshotSetting_AutoCopyScreenshotFile` | 自动复制截图文件 |
| `Settings.Screenshot.AutoSdr` | `ScreenshotSetting_AutoConvertToSDRImage` | 自动转换为 SDR 图片 |
| `Settings.Screenshot.BuiltinFolderDesc` | `ScreenshotSetting_TheBuiltInScreenshotFeatureWillSaveImagesToTheFollowingFolder` | 内置截图功能会将图片保存到以下文件夹。 |
| `Settings.Screenshot.ClearThumbnailCache` | `ScreenshotSetting_ClearThumbnailCache` | 清除缩略图缓存 |
| `Settings.Screenshot.ColorManagement` | `ScreenshotSetting_ColorManagement` | 色彩管理 |
| `Settings.Screenshot.FormatAndQuality` | `ScreenshotSetting_FormatAndQuality` | 格式和质量 |
| `Settings.Screenshot.GamepadSuggestion` | `ScreenshotSetting_ScreenshotWithGamepadSuggestion` | 手柄截图需要「手柄增强」页的按键映射；LunarWard 不支持手柄增强，请用 Steam Input 或桌面合成器的映射。 |
| `Settings.Screenshot.HdrNote` | `ScreenshotSetting_WhenCapturingHDRImagesSDRVersionWillAlsoBeSaved` | 如果截图为 HDR 图像，将同时保存 SDR 图片。 |
| `Settings.Screenshot.OtherOptions` | `ScreenshotSetting_OtherOptions` | 其他选项 |
| `Settings.Screenshot.Shortcut` | `ScreenshotSetting_ScreenshotShortcut` | 截图快捷键 |
| `Settings.Screenshot.TestCapture` | `ScreenshotSetting_TestScreenshot` | 测试截图 |
| `Settings.Toolbox` | `SettingPage_Toolbox` | 工具箱 |
| `Settings.Toolbox.GameSetting` | `LauncherPage_GameSetting` | 游戏设置 |
| `Settings.Toolbox.Screenshot` | `MainPage_GameScreenshot` | 游戏截图 |

### 2.2 未映射键（保留 AppStrings，待上游补键或确认为 Linux 特有）

共 **133** 个键。按性质分两类：

**（a）Linux / LunarWard 特有**（本仓库新增的说明、占位、self-check 等，上游没有对应文案，最终应作为 LunarWard 自有键迁入 Lang 或单独资源）：

`Settings.About.GpuDriver`, `Settings.About.GpuPciId`, `Settings.About.Kernel`, `Settings.About.ReplicaNote`, `Settings.About.Session`, `Settings.About.Tagline`, `Settings.About.Thanks`, `Settings.About.Upstream`, `Settings.Advanced.RpcDesc`, `Settings.Advanced.RpcNotRunning`, `Settings.Advanced.RpcUnavailableNote`, `Settings.Advanced.SelfCheck`, `Settings.Advanced.SelfCheckDesc`, `Settings.Advanced.UrlProtocolDesc`, `Settings.Download.ComponentCount`, `Settings.Download.ComponentSource`, `Settings.Download.ComponentSourceDesc`, `Settings.Download.Concurrency`, `Settings.Download.ConcurrencyValue`, `Settings.Download.IndexFailed`, `Settings.Download.IndexState`, `Settings.Download.IndexUpdated`, `Settings.Download.NeverUpdated`, `Settings.Download.ReadOnlyNote`, `Settings.Download.Retries`, `Settings.Download.RetriesValue`, `Settings.Download.Timeout`, `Settings.Download.TimeoutValue`, `Settings.Download.UpdateIndex`, `Settings.Download.Updating`, `Settings.Files.BackingUp`, `Settings.Files.ReselectUnavailable`, `Settings.Gamepad.Desc`, `Settings.Gamepad.KeptForParity`, `Settings.Gamepad.LinuxSection`, `Settings.Gamepad.SimulateInputDesc`, `Settings.Gamepad.SteamInput`, `Settings.Gamepad.Unavailable`, `Settings.General.Announcements`, `Settings.General.AnnouncementsDesc`, `Settings.General.Autostart`, `Settings.General.AutostartHint`, `Settings.General.CloseDesc`, `Settings.General.LaunchBehaviorDesc`, `Settings.General.RegistryUnavailable`, `Settings.General.SystemEffectsUnavailable`, `Settings.General.Theme`, `Settings.General.ThemeDesc`, `Settings.General.ThemeValue`, `Settings.General.TrayHint`, `Settings.General.VideoBackground`, `Settings.General.VideoBackgroundDesc`, `Settings.Hotkey.CancelEdit`, `Settings.Hotkey.Delete`, `Settings.Hotkey.DesktopEntry`, `Settings.Hotkey.DesktopEntryContent`, `Settings.Hotkey.DesktopEntryMissing`, `Settings.Hotkey.HotkeyUnavailable`, `Settings.Hotkey.LinuxSection`, `Settings.Hotkey.LinuxWay`, `Settings.Hotkey.OpenDesktopEntry`, `Settings.Hotkey.SavedNotGlobal`, `Settings.Hotkey.SingleKeyTips`, `Settings.Screenshot.FormatUnavailable`, `Settings.Screenshot.GamepadSuggestion`, `Settings.Toolbox.GameSettingDesc`, `Settings.Toolbox.ShellDesc`

**（b）上游可能有、当前 Lang 缺键或措辞不同**（需要对照上游中文文案新增 Lang 键，不能机翻对齐）：

`Main.Key`, `Main.Source`, `Main.Value`, `Screenshot.Directory`, `Screenshot.DirectoryMissing`, `Settings.About.CheckFailed`, `Settings.About.Checking`, `Settings.About.Docs`, `Settings.About.DonateUnavailable`, `Settings.About.Homepage`, `Settings.About.License`, `Settings.About.LinksUnavailableNote`, `Settings.About.OpenRelease`, `Settings.About.PrivacyUnavailable`, `Settings.About.UpToDate`, `Settings.About.UpdateAvailable`, `Settings.Advanced.EnvDefaults`, `Settings.Advanced.EnvDefaultsDesc`, `Settings.Advanced.EnvUnavailable`, `Settings.Advanced.LogLevel`, `Settings.Advanced.LogLevelDesc`, `Settings.Advanced.LogLevelValue`, `Settings.Advanced.TestResult`, `Settings.Advanced.UrlNotRegistered`, `Settings.Advanced.UrlRegistered`, `Settings.Common.Copied`, `Settings.Common.OpenFailed`, `Settings.Download.HardLinkUnavailable`, `Settings.Download.InstallPathUnavailable`, `Settings.Download.ReadOnly`, `Settings.Download.SpeedLimitUnavailable`, `Settings.Files.BackupDatabaseHint`, `Settings.Files.BackupDone`, `Settings.Files.BackupFailed`, `Settings.Files.BackupUnavailable`, `Settings.Files.CacheHome`, `Settings.Files.CacheSize`, `Settings.Files.ClearCacheBody`, `Settings.Files.ClearCacheDone`, `Settings.Files.Clearing`, `Settings.Files.ConfigFile`, `Settings.Files.ConfigHome`, `Settings.Files.DeleteAllUnavailable`, `Settings.Files.LogDir`, `Settings.Files.OpenConfigDir`, `Settings.Files.SectionPaths`, `Settings.General.AskEveryTime`, `Settings.General.HelpTranslateUnavailable`, `Settings.General.LanguageChanged`, `Settings.General.LaunchBehavior`, `Settings.General.LaunchBehaviorValue`, `Settings.General.PendingEffect`, `Settings.Screenshot.CaptureButtonsUnavailable`, `Settings.Screenshot.Count`, `Settings.Screenshot.Empty`, `Settings.Screenshot.FeatureDesc`, `Settings.Screenshot.GameDecides`, `Settings.Screenshot.NoChangeFolder`, `Settings.Screenshot.NoGameDirShort`, `Settings.Screenshot.NoGameDirectory`, `Settings.Toolbox.Diagnose`, `Settings.Toolbox.DiagnoseDesc`, `Settings.Toolbox.NeedProfile`, `Settings.Toolbox.NoReport`, `Settings.Toolbox.ScreenshotDesc`, `Settings.Toolbox.Shell`

## 3. 语言切换的刷新机制

上游 `SettingPage.xaml.cs` 在收到 `LanguageChangedMessage` 时调用 `this.Bindings.Update()` 重算 `x:Bind`。Avalonia 的 `{x:Static}` 不会自更新，因此本仓库的等价做法是：

1. `SettingPageBase` 构造时 `Lang.Culture = CultureInfo.GetCultureInfo(config.launcher.language)`（`SyncLanguage()`），保证子页一创建就是配置语言；
2. `GeneralSetting.ApplyLanguage` 切换时更新 `Lang.Culture`、写 config，并 `WeakReferenceMessenger.Default.Send(new LanguageChangedMessage())`；
3. `SettingPage` 收到消息后刷新左侧导航/页头文案，并 `_sections.Clear()` 后重建当前子页（静态绑定只能靠重建实例刷新）。

## 4. 其余目录的进度

统计口径：`Strings[...]`（XAML 绑定 + 代码索引器）剩余引用数 / 已用 `Lang` 引用数。

| 目录 | 剩余 AppStrings 引用 | 已用 Lang | 状态 |
| --- | --- | --- | --- |
| `Features/Setting` | 148 | 228 | 🟡 试点中 |
| `Features/Screenshot` | 3 | 2 | ⏳ 未迁移 |
| `Features/Gacha` | 0 | 310 | ✅ 已迁移 |
| `Features/GameRecord` | 0 | 301 | ✅ 已迁移 |
| `Features/GameLauncher` | 0 | 157 | ✅ 已迁移 |
| `Features/GameSetting` | 0 | 63 | ✅ 已迁移 |
| `Features/ViewHost` | 0 | 60 | ✅ 已迁移 |
| `Features/GameInstall` | 0 | 57 | ✅ 已迁移 |
| `Features/Update` | 0 | 43 | ✅ 已迁移 |
| `Features/Background` | 0 | 27 | ✅ 已迁移 |
| `Features/GamepadControl` | 0 | 24 | ✅ 已迁移 |
| `Features/PlayTime` | 0 | 22 | ✅ 已迁移 |
| `Features/SelfQuery` | 0 | 19 | ✅ 已迁移 |
| `Features/GameAccount` | 0 | 18 | ✅ 已迁移 |
| `Features/GameSelector` | 0 | 16 | ✅ 已迁移 |
| `Features/CloudGame` | 0 | 9 | ✅ 已迁移 |

> 说明：多数目录“剩余 0”是因为它们从未接过 `AppStrings`（直接用的 `Lang`），并非本轮工作。真正混用 `AppStrings` 的只剩 `Features/Setting`（试点）与 `Features/Screenshot`（少量）。

## 5. 继续迁移的操作手册

1. **按目录推进**，一个目录一个提交；改完必须 `dotnet build` + 相关页面截图。
2. XAML：`{Binding Strings[Settings.X]}` → `{x:Static lang:Lang.<Key>}`，并在根元素加 `xmlns:lang="using:LunarWard.Language"`。
3. 代码：`Strings["Settings.X"]` → `Lang.<Key>`，`using LunarWard.Language;`。
4. **占位符必须一致**：`string.Format` 的参数个数要逐一核对（本轮已用脚本过滤掉占位符数量不一致的候选）。
5. 语言切换：目录若有静态文案，照 §3 接 `LanguageChangedMessage` 或重建视图。
6. 测试：`AppStringsResxTests` / `AppStringsTests` 在过渡期继续保证遗留资源自洽；全部迁移完成后改为断言 `Lang` 的中性（英文）与各语言条目键集合一致。

## 6. 已知差异与风险

- `Lang` 是上游完整体系（英文为中性语言 + 11 个语言附属程序集，879 条），`Lang.Culture` 由配置语言驱动；本仓库原来的 `AppStrings` 只有中英两套且值是自己翻译的，迁移后**文案会切换成上游原文**，个别措辞与旧版不同属预期。
- 部分映射是“同义不同词”（例如 `Settings.About.Version` → `SettingPage_CurrentVersion` 带冒号），视觉上可能与旧版差一个标点，属可接受的贴近上游。
- `Settings.Advanced.*` 里与 `lunarward://` 协议相关的文案，上游是 Windows 注册表方案，语义只在“可用的 URL 协议”这一层对齐，具体句子仍是 Linux 方案描述。
