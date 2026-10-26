# 11 — 移植清单（上游 src/Starward → 本仓库 src/LunarWard）

> 由 `python3 scripts/port-backlog.py --write` 生成，勿手改表格；人工结论写在每节标题下的「备注」里。

上游文件 **314** 个，本地已有同名落点 **308** 个，**缺 6** 个；本地独有文件 **279** 个（需并入 Features 或删除）。

> 注：上表「缺」包含以**等价实现**承载的上游文件（WinUI/Win32 专有物，见`scripts/check-alignment.py` 的 `EQUIVALENTS`）；等价项在 `check-alignment.py` 里单独计数、不计为失败。文件级对齐以 `check-alignment.py` 的 `missing: []` 为准。

判定标准：上游相对路径在本地存在（`.xaml` ↔ `.axaml`）。「有文件」只说明路径对齐，内容是否已按上游实现，需要逐节核对。

## 总览

| 分组 | 上游文件 | 已有 | 缺失 |
|---|---:|---:|---:|
| Features/GameRecord | 47 | 47 | 0 |
| Features/Gacha | 40 | 40 | 0 |
| Features/GameLauncher | 26 | 26 | 0 |
| Features/Setting | 25 | 25 | 0 |
| Features/Screenshot | 23 | 23 | 0 |
| Features/ViewHost | 20 | 18 | 2 |
| Helpers | 16 | 14 | 2 |
| Features/PlayTime | 14 | 14 | 0 |
| Features/Background | 13 | 13 | 0 |
| Converters | 11 | 11 | 0 |
| Features/GameInstall | 9 | 9 | 0 |
| Features/Codec | 8 | 8 | 0 |
| Features/Update | 8 | 8 | 0 |
| Features/GameSetting | 7 | 7 | 0 |
| Features/GameSelector | 6 | 6 | 0 |
| Controls | 5 | 3 | 2 |
| Features/GameAccount | 5 | 5 | 0 |
| Features/Overlay | 4 | 4 | 0 |
| Features/SelfQuery | 4 | 4 | 0 |
| Features/CloudGame | 3 | 3 | 0 |
| Features/GamepadControl | 3 | 3 | 0 |
| Features/UrlProtocol | 3 | 3 | 0 |
| Features/Database | 2 | 2 | 0 |
| Frameworks | 2 | 2 | 0 |
| App.xaml | 1 | 1 | 0 |
| App.xaml.cs | 1 | 1 | 0 |
| AppConfig.Common.cs | 1 | 1 | 0 |
| AppConfig.Configuration.cs | 1 | 1 | 0 |
| AppConfig.ServiceProvider.cs | 1 | 1 | 0 |
| AppConfig.Setting.cs | 1 | 1 | 0 |
| Features/GameFeatureConfig.cs | 1 | 1 | 0 |
| Features/HoYoPlay | 1 | 1 | 0 |
| Features/RPC | 1 | 1 | 0 |
| Program.cs | 1 | 1 | 0 |

## Features/GameRecord（47/47）

路径已全部对齐（内容仍需逐文件核对）。

## Features/Gacha（40/40）

路径已全部对齐（内容仍需逐文件核对）。

## Features/GameLauncher（26/26）

路径已全部对齐（内容仍需逐文件核对）。

## Features/Setting（25/25）

路径已全部对齐（内容仍需逐文件核对）。

## Features/Screenshot（23/23）

路径已全部对齐（内容仍需逐文件核对）。

## Features/ViewHost（18/20）

| 上游文件 | 本地落点 |
|---|---|
| `Features/ViewHost/BH3GlobalGameServerChangedMessage.cs` | `Features/ViewHost/BH3GlobalGameServerChangedMessage.cs` |
| `Features/ViewHost/BlankPage.xaml` | `Features/ViewHost/BlankPage.axaml` |
| `Features/ViewHost/BlankPage.xaml.cs` | `Features/ViewHost/BlankPage.axaml.cs` |
| `Features/ViewHost/MainView.xaml` | `Features/ViewHost/MainView.axaml` |
| `Features/ViewHost/MainView.xaml.cs` | `Features/ViewHost/MainView.axaml.cs` |
| `Features/ViewHost/MainViewNavigateMessage.cs` | `Features/ViewHost/MainViewNavigateMessage.cs` |
| `Features/ViewHost/MainWindow.xaml` | `Features/ViewHost/MainWindow.axaml` |
| `Features/ViewHost/MainWindow.xaml.cs` | `Features/ViewHost/MainWindow.axaml.cs` |
| `Features/ViewHost/MainWindowCloseDialog.xaml` | `Features/ViewHost/MainWindowCloseDialog.axaml` |
| `Features/ViewHost/MainWindowCloseDialog.xaml.cs` | `Features/ViewHost/MainWindowCloseDialog.axaml.cs` |
| `Features/ViewHost/MainWindowCloseOption.cs` | `Features/ViewHost/MainWindowCloseOption.cs` |
| `Features/ViewHost/MainWindowStateChangedMessage.cs` | `Features/ViewHost/MainWindowStateChangedMessage.cs` |
| `Features/ViewHost/NoPermissionWindow.xaml` | `Features/ViewHost/NoPermissionWindow.axaml` |
| `Features/ViewHost/NoPermissionWindow.xaml.cs` | `Features/ViewHost/NoPermissionWindow.axaml.cs` |
| `Features/ViewHost/RemovableStorageDeviceChangedMessage.cs` | `Features/ViewHost/RemovableStorageDeviceChangedMessage.cs` |
| `Features/ViewHost/SystemTrayWindow.xaml` | ❌ 缺 |
| `Features/ViewHost/SystemTrayWindow.xaml.cs` | ❌ 缺 |
| `Features/ViewHost/WelcomePageFinishedMessage.cs` | `Features/ViewHost/WelcomePageFinishedMessage.cs` |
| `Features/ViewHost/WelcomeWindow.xaml` | `Features/ViewHost/WelcomeWindow.axaml` |
| `Features/ViewHost/WelcomeWindow.xaml.cs` | `Features/ViewHost/WelcomeWindow.axaml.cs` |

## Helpers（14/16）

| 上游文件 | 本地落点 |
|---|---|
| `Helpers/AccentColorHelper.cs` | ❌ 缺 |
| `Helpers/BindConverter.cs` | `Helpers/BindConverter.cs` |
| `Helpers/ClipboardHelper.cs` | `Helpers/ClipboardHelper.cs` |
| `Helpers/DriveHelper.cs` | `Helpers/DriveHelper.cs` |
| `Helpers/Enumeration/EnumItemsSource.cs` | `Helpers/Enumeration/EnumItemsSource.cs` |
| `Helpers/Enumeration/LocalizationKeyAttribute.cs` | `Helpers/Enumeration/LocalizationKeyAttribute.cs` |
| `Helpers/FileCache.cs` | `Helpers/FileCache.cs` |
| `Helpers/FileDialogHelper.cs` | `Helpers/FileDialogHelper.cs` |
| `Helpers/FileDialogNative.cs` | ❌ 缺 |
| `Helpers/FileHashHelper.cs` | `Helpers/FileHashHelper.cs` |
| `Helpers/InAppToast.cs` | `Helpers/InAppToast.cs` |
| `Helpers/PointerCursor.cs` | `Helpers/PointerCursor.cs` |
| `Helpers/SystemBackdropHelper.cs` | `Helpers/SystemBackdropHelper.cs` |
| `Helpers/SystemUIHelper.cs` | `Helpers/SystemUIHelper.cs` |
| `Helpers/TransparentBackdrop.cs` | `Helpers/TransparentBackdrop.cs` |
| `Helpers/XamlRootExtension.cs` | `Helpers/XamlRootExtension.cs` |

## Features/PlayTime（14/14）

路径已全部对齐（内容仍需逐文件核对）。

## Features/Background（13/13）

路径已全部对齐（内容仍需逐文件核对）。

## Converters（11/11）

路径已全部对齐（内容仍需逐文件核对）。

## Features/GameInstall（9/9）

路径已全部对齐（内容仍需逐文件核对）。

## Features/Codec（8/8）

路径已全部对齐（内容仍需逐文件核对）。

## Features/Update（8/8）

路径已全部对齐（内容仍需逐文件核对）。

## Features/GameSetting（7/7）

路径已全部对齐（内容仍需逐文件核对）。

## Features/GameSelector（6/6）

路径已全部对齐（内容仍需逐文件核对）。

## Controls（3/5）

| 上游文件 | 本地落点 |
|---|---|
| `Controls/CachedImage.cs` | `Controls/CachedImage.cs` |
| `Controls/ColorRectChart.xaml` | ❌ 缺 |
| `Controls/ColorRectChart.xaml.cs` | ❌ 缺 |
| `Controls/ColorfulTextBlock.xaml` | `Controls/ColorfulTextBlock.axaml` |
| `Controls/ColorfulTextBlock.xaml.cs` | `Controls/ColorfulTextBlock.axaml.cs` |

## Features/GameAccount（5/5）

路径已全部对齐（内容仍需逐文件核对）。

## Features/Overlay（4/4）

路径已全部对齐（内容仍需逐文件核对）。

## Features/SelfQuery（4/4）

路径已全部对齐（内容仍需逐文件核对）。

## Features/CloudGame（3/3）

路径已全部对齐（内容仍需逐文件核对）。

## Features/GamepadControl（3/3）

路径已全部对齐（内容仍需逐文件核对）。

## Features/UrlProtocol（3/3）

路径已全部对齐（内容仍需逐文件核对）。

## Features/Database（2/2）

路径已全部对齐（内容仍需逐文件核对）。

## Frameworks（2/2）

路径已全部对齐（内容仍需逐文件核对）。

## App.xaml（1/1）

路径已全部对齐（内容仍需逐文件核对）。

## App.xaml.cs（1/1）

路径已全部对齐（内容仍需逐文件核对）。

## AppConfig.Common.cs（1/1）

路径已全部对齐（内容仍需逐文件核对）。

## AppConfig.Configuration.cs（1/1）

路径已全部对齐（内容仍需逐文件核对）。

## AppConfig.ServiceProvider.cs（1/1）

路径已全部对齐（内容仍需逐文件核对）。

## AppConfig.Setting.cs（1/1）

路径已全部对齐（内容仍需逐文件核对）。

## Features/GameFeatureConfig.cs（1/1）

路径已全部对齐（内容仍需逐文件核对）。

## Features/HoYoPlay（1/1）

路径已全部对齐（内容仍需逐文件核对）。

## Features/RPC（1/1）

路径已全部对齐（内容仍需逐文件核对）。

## Program.cs（1/1）

路径已全部对齐（内容仍需逐文件核对）。

## 本地独有文件（上游无对应）

按 D1（严格移植）的判定：这些文件要么并入对应 Features 目录并改成上游同名文件，要么删除（若其职责由上游文件覆盖），要么登记为平台等价物。

- `App.axaml`
- `App.axaml.cs`
- `AppConfig.Setting.LinuxExtensions.cs`
- `Controls/ColorRectChart.cs`
- `Controls/ColorfulTextBlock.axaml`
- `Controls/ColorfulTextBlock.axaml.cs`
- `Controls/PlayTimeBarChart.cs`
- `Controls/PlayTimeCalendarHeatmap.cs`
- `Features/Background/AppBackground.axaml`
- `Features/Background/AppBackground.axaml.cs`
- `Features/Background/AppBackgroundImageExStyle.axaml`
- `Features/Background/BackgroundViewPage.axaml`
- `Features/Background/BackgroundViewPage.axaml.cs`
- `Features/Background/BackgroundViewWindow.axaml`
- `Features/Background/BackgroundViewWindow.axaml.cs`
- `Features/CloudGame/CloudGameButton.axaml`
- `Features/CloudGame/CloudGameButton.axaml.cs`
- `Features/Codec/ColorPrimaries.cs`
- `Features/Codec/PngCodec.cs`
- `Features/Database/DatabaseFilePermissions.cs`
- `Features/Gacha/CloudGameGachaWindow.axaml`
- `Features/Gacha/CloudGameGachaWindow.axaml.cs`
- `Features/Gacha/DeleteGachaLogDialog.axaml`
- `Features/Gacha/DeleteGachaLogDialog.axaml.cs`
- `Features/Gacha/GachaLogPage.axaml`
- `Features/Gacha/GachaLogPage.axaml.cs`
- `Features/Gacha/GachaProgressToast.cs`
- `Features/Gacha/GachaStatsCard.axaml`
- `Features/Gacha/GachaStatsCard.axaml.cs`
- `Features/Gacha/GenshinBeyondGachaPage.axaml`
- `Features/Gacha/GenshinBeyondGachaPage.axaml.cs`
- `Features/Gacha/UIGF/UIGF4GachaWindow.axaml`
- `Features/Gacha/UIGF/UIGF4GachaWindow.axaml.cs`
- `Features/Gacha/ZZZGachaStatsCard.axaml`
- `Features/Gacha/ZZZGachaStatsCard.axaml.cs`
- `Features/Gacha/ZZZGachaToolbox/ZZZGachaInfoWindow.axaml`
- `Features/Gacha/ZZZGachaToolbox/ZZZGachaInfoWindow.axaml.cs`
- `Features/GameAccount/GameAccountSwitcher.axaml`
- `Features/GameAccount/GameAccountSwitcher.axaml.cs`
- `Features/GameInstall/InstallGameDialog.axaml`
- `Features/GameInstall/InstallGameDialog.axaml.cs`
- `Features/GameInstall/Installer/ArchiveExtractor.cs`
- `Features/GameInstall/Installer/DiffMap.cs`
- `Features/GameInstall/Installer/DiffPatcher.cs`
- `Features/GameInstall/Installer/DriveHelper.cs`
- `Features/GameInstall/Installer/FileCombinedStream.cs`
- `Features/GameInstall/Installer/FileSliceStream.cs`
- `Features/GameInstall/Installer/GameInstallContext.cs`
- `Features/GameInstall/Installer/GameInstallController.cs`
- `Features/GameInstall/Installer/GameInstallDownloadMode.cs`
- `Features/GameInstall/Installer/GameInstallFile.cs`
- `Features/GameInstall/Installer/GameInstallHelper.cs`
- `Features/GameInstall/Installer/GameInstallOperation.cs`
- `Features/GameInstall/Installer/GameInstallService.cs`
- `Features/GameInstall/Installer/GameInstallState.cs`
- `Features/GameInstall/Installer/GamePackageService.cs`
- `Features/GameInstall/Installer/HardLink.cs`
- `Features/GameInstall/Installer/InstallMessages.cs`
- `Features/GameInstall/Installer/InstallerHttpClientFactory.cs`
- `Features/GameInstall/Installer/InstallerLogger.cs`
- `Features/GameInstall/Installer/ParentProcessWatcher.cs`
- `Features/GameInstall/Installer/PkgVersionItem.cs`
- `Features/GameInstall/Installer/PredownloadFile.cs`
- `Features/GameInstall/Installer/ProtoReader.cs`
- `Features/GameInstall/Installer/SophonManifest.cs`
- `Features/GameInstall/Installer/TokenBucketRateLimiter.cs`
- `Features/GameInstall/PreDownloadButton.axaml`
- `Features/GameInstall/PreDownloadButton.axaml.cs`
- `Features/GameInstall/PreDownloadDialog.axaml`
- `Features/GameInstall/PreDownloadDialog.axaml.cs`
- `Features/GameLauncher/AssemblyInfo.cs`
- `Features/GameLauncher/BilibiliPluginDownloader.cs`
- `Features/GameLauncher/ChannelModel.cs`
- `Features/GameLauncher/ChannelValidator.cs`
- `Features/GameLauncher/CompactCheckBoxStyle.axaml`
- `Features/GameLauncher/DX12IntroDialog.axaml`
- `Features/GameLauncher/DX12IntroDialog.axaml.cs`
- `Features/GameLauncher/GameAuthLoginButton.axaml`
- `Features/GameLauncher/GameAuthLoginButton.axaml.cs`
- `Features/GameLauncher/GameBannerAndPost.axaml`
- `Features/GameLauncher/GameBannerAndPost.axaml.cs`
- `Features/GameLauncher/GameCatalog.cs`
- `Features/GameLauncher/GameConfigIniFile.cs`
- `Features/GameLauncher/GameLauncherPage.axaml`
- `Features/GameLauncher/GameLauncherPage.axaml.cs`
- `Features/GameLauncher/GameLauncherSettingDialog.axaml`
- `Features/GameLauncher/GameLauncherSettingDialog.axaml.cs`
- `Features/GameLauncher/GameNoticeWindow.axaml`
- `Features/GameLauncher/GameNoticeWindow.axaml.cs`
- `Features/GameLauncher/GamePrefixContext.cs`
- `Features/GameLauncher/StartGameButton.axaml`
- `Features/GameLauncher/StartGameButton.axaml.cs`
- `Features/GameRecord/BBSWebBridge.axaml`
- `Features/GameRecord/BBSWebBridge.axaml.cs`
- `Features/GameRecord/BattleChronicleWindow.axaml`
- `Features/GameRecord/BattleChronicleWindow.axaml.cs`
- `Features/GameRecord/DailyNote/DailyNoteButton.axaml`
- `Features/GameRecord/DailyNote/DailyNoteButton.axaml.cs`
- `Features/GameRecord/GameRecordAssets.cs`
- `Features/GameRecord/GameRecordConverters.cs`
- `Features/GameRecord/GameRecordPage.axaml`
- `Features/GameRecord/GameRecordPage.axaml.cs`
- `Features/GameRecord/Genshin/ImaginariumTheaterPage.axaml`
- `Features/GameRecord/Genshin/ImaginariumTheaterPage.axaml.cs`
- `Features/GameRecord/Genshin/SpiralAbyssPage.axaml`
- `Features/GameRecord/Genshin/SpiralAbyssPage.axaml.cs`
- `Features/GameRecord/Genshin/StygianOnslaughtPage.axaml`
- `Features/GameRecord/Genshin/StygianOnslaughtPage.axaml.cs`
- `Features/GameRecord/Genshin/TravelersDiaryPage.axaml`
- `Features/GameRecord/Genshin/TravelersDiaryPage.axaml.cs`
- `Features/GameRecord/LoginPage.axaml`
- `Features/GameRecord/LoginPage.axaml.cs`
- `Features/GameRecord/ObservableSupport.cs`
- `Features/GameRecord/StarRail/ApocalypticShadowPage.axaml`
- `Features/GameRecord/StarRail/ApocalypticShadowPage.axaml.cs`
- `Features/GameRecord/StarRail/ChallengePeakPage.axaml`
- `Features/GameRecord/StarRail/ChallengePeakPage.axaml.cs`
- `Features/GameRecord/StarRail/ForgottenHallPage.axaml`
- `Features/GameRecord/StarRail/ForgottenHallPage.axaml.cs`
- `Features/GameRecord/StarRail/PureFictionPage.axaml`
- `Features/GameRecord/StarRail/PureFictionPage.axaml.cs`
- `Features/GameRecord/StarRail/SimulatedUniversePage.axaml`
- `Features/GameRecord/StarRail/SimulatedUniversePage.axaml.cs`
- `Features/GameRecord/StarRail/TrailblazeCalendarPage.axaml`
- `Features/GameRecord/StarRail/TrailblazeCalendarPage.axaml.cs`
- `Features/GameRecord/ZZZ/DeadlyAssaultPage.axaml`
- `Features/GameRecord/ZZZ/DeadlyAssaultPage.axaml.cs`
- `Features/GameRecord/ZZZ/InterKnotMonthlyReportPage.axaml`
- `Features/GameRecord/ZZZ/InterKnotMonthlyReportPage.axaml.cs`
- `Features/GameRecord/ZZZ/ShiyuDefensePage.axaml`
- `Features/GameRecord/ZZZ/ShiyuDefensePage.axaml.cs`
- `Features/GameSelector/GameSelector.axaml`
- `Features/GameSelector/GameSelector.axaml.cs`
- `Features/GameSelector/NoAnimationImageExStyle.axaml`
- `Features/GameSetting/GameSettingPage.axaml`
- `Features/GameSetting/GameSettingPage.axaml.cs`
- `Features/GameSetting/GenshinHDRLuminanceSettingDialog.axaml`
- `Features/GameSetting/GenshinHDRLuminanceSettingDialog.axaml.cs`
- `Features/GamepadControl/GamepadTypes.cs`
- `Features/GamepadControl/SdlGameController.cs`
- `Features/GamepadControl/SimulateInputTipsWindow.axaml`
- `Features/GamepadControl/SimulateInputTipsWindow.axaml.cs`
- `Features/GamepadControl/X11Input.cs`
- `Features/Overlay/OverlayWindow.axaml`
- `Features/Overlay/OverlayWindow.axaml.cs`
- `Features/PlayTime/PlayTimeButton.axaml`
- `Features/PlayTime/PlayTimeButton.axaml.cs`
- `Features/PlayTime/PlayTimeStatsDialog.axaml`
- `Features/PlayTime/PlayTimeStatsDialog.axaml.cs`
- `Features/Screenshot/ImageBatchConvertWindow.axaml`
- `Features/Screenshot/ImageBatchConvertWindow.axaml.cs`
- `Features/Screenshot/ImageViewWindow.axaml`
- `Features/Screenshot/ImageViewWindow.axaml.cs`
- `Features/Screenshot/ImageViewWindow2.axaml`
- `Features/Screenshot/ImageViewWindow2.axaml.cs`
- `Features/Screenshot/ScreenCaptureInfoWindow.axaml`
- `Features/Screenshot/ScreenCaptureInfoWindow.axaml.cs`
- `Features/Screenshot/ScreenshotFolderManageDialog.axaml`
- `Features/Screenshot/ScreenshotFolderManageDialog.axaml.cs`
- `Features/Screenshot/ScreenshotPage.axaml`
- `Features/Screenshot/ScreenshotPage.axaml.cs`
- `Features/Screenshot/ScreenshotPage2.axaml`
- `Features/Screenshot/ScreenshotPage2.axaml.cs`
- `Features/SelfQuery/SelfQueryPage.axaml`
- `Features/SelfQuery/SelfQueryPage.axaml.cs`
- `Features/Setting/AboutSetting.axaml`
- `Features/Setting/AboutSetting.axaml.cs`
- `Features/Setting/AdvancedSetting.axaml`
- `Features/Setting/AdvancedSetting.axaml.cs`
- `Features/Setting/DownloadSetting.axaml`
- `Features/Setting/DownloadSetting.axaml.cs`
- `Features/Setting/FileManageSetting.axaml`
- `Features/Setting/FileManageSetting.axaml.cs`
- `Features/Setting/GamepadControlSetting.axaml`
- `Features/Setting/GamepadControlSetting.axaml.cs`
- `Features/Setting/GeneralSetting.axaml`
- `Features/Setting/GeneralSetting.axaml.cs`
- `Features/Setting/HotkeyInput.axaml`
- `Features/Setting/HotkeyInput.axaml.cs`
- `Features/Setting/HotkeySetting.axaml`
- `Features/Setting/HotkeySetting.axaml.cs`
- `Features/Setting/ScreenshotSetting.axaml`
- `Features/Setting/ScreenshotSetting.axaml.cs`
- `Features/Setting/SettingLanguageOption.cs`
- `Features/Setting/SettingLocalization.cs`
- `Features/Setting/SettingPage.axaml`
- `Features/Setting/SettingPage.axaml.cs`
- `Features/Setting/SettingShellActions.cs`
- `Features/Setting/ToolboxSetting.axaml`
- `Features/Setting/ToolboxSetting.axaml.cs`
- `Features/Update/UpdateWindow.axaml`
- `Features/Update/UpdateWindow.axaml.cs`
- `Features/UrlProtocol/TestUrlProtocolWindow.axaml`
- `Features/UrlProtocol/TestUrlProtocolWindow.axaml.cs`
- `Features/ViewHost/BlankPage.axaml`
- `Features/ViewHost/BlankPage.axaml.cs`
- `Features/ViewHost/MainView.axaml`
- `Features/ViewHost/MainView.axaml.cs`
- `Features/ViewHost/MainWindow.axaml`
- `Features/ViewHost/MainWindow.axaml.cs`
- `Features/ViewHost/MainWindowCloseDialog.axaml`
- `Features/ViewHost/MainWindowCloseDialog.axaml.cs`
- `Features/ViewHost/NoPermissionWindow.axaml`
- `Features/ViewHost/NoPermissionWindow.axaml.cs`
- `Features/ViewHost/SystemTrayWindow.cs`
- `Features/ViewHost/WelcomeWindow.axaml`
- `Features/ViewHost/WelcomeWindow.axaml.cs`
- `Frameworks/Dialogs/StarwardContentDialog.axaml`
- `Frameworks/Dialogs/StarwardContentDialog.axaml.cs`
- `Frameworks/FontIcon.cs`
- `Frameworks/Frame.cs`
- `Frameworks/NavigationView.cs`
- `Frameworks/Pivot.cs`
- `Frameworks/ShellHelper.cs`
- `Frameworks/SingleInstance.cs`
- `Frameworks/StartupUi.cs`
- `Frameworks/WebView/CoreWebView2Compat.cs`
- `Frameworks/WebView/GtkRuntime.cs`
- `Frameworks/WebView/Interop/NativeMethods.cs`
- `Frameworks/WebView/SelfTest.Avalonia/Program.cs`
- `Frameworks/WebView/SelfTest.Avalonia/XProbe.axaml`
- `Frameworks/WebView/SelfTest.Avalonia/XProbe.axaml.cs`
- `Frameworks/WebView/SelfTest/Program.cs`
- `Frameworks/WebView/VirtualHostMapping.cs`
- `Frameworks/WebView/WebKitWebViewCore.cs`
- `Frameworks/WebView/WebView.cs`
- `Frameworks/WebView/WebViewAvailability.cs`
- `Helpers/ToastItem.cs`
- `Launch/DailyNoteService.cs`
- `Launch/ExternalProgram.cs`
- `Launch/GameLogPump.cs`
- `Launch/LaunchEngine.cs`
- `Launch/LaunchError.cs`
- `Launch/LaunchPlan.cs`
- `Launch/LaunchPlanFactory.cs`
- `Launch/LaunchReport.cs`
- `Launch/LauncherConfig.cs`
- `Launch/ProfileConfig.cs`
- `Launch/ScreenshotService.cs`
- `Launch/ToolboxSettings.cs`
- `Launch/UrlProtocol.cs`
- `Localization/AppStrings.cs`
- `Platform/AutostartEntry.cs`
- `Platform/FileSystem.cs`
- `Platform/LinuxProcessTree.cs`
- `Platform/LinuxSystemInfo.cs`
- `Platform/ProcessController.cs`
- `Platform/ProcessLauncher.cs`
- `Platform/Unix/Libc.cs`
- `Platform/XdgPaths.cs`
- `Services/AppVersion.cs`
- `Services/BackgroundCatalog.cs`
- `Services/BackgroundService.cs`
- `Services/CaptureMode.cs`
- `Services/CloseWindowDecision.cs`
- `Services/DialogReentrancyGate.cs`
- `Services/DialogService.cs`
- `Services/GameAssets.cs`
- `Services/GameScanner.cs`
- `Services/GameSettingRegistry.cs`
- `Services/GameUninstaller.cs`
- `Services/ReleaseChecker.cs`
- `Services/StarwardAssets.cs`
- `Services/VideoBackgroundPlayer.cs`
- `Services/WineRegistry.cs`
- `Styles/Starward.axaml`
- `SupportedPlatform.cs`
- `Wine/ComponentInstaller.cs`
- `Wine/ComponentRepository.cs`
- `Wine/DxvkManager.cs`
- `Wine/GameAdapter.cs`
- `Wine/KeywordReplacer.cs`
- `Wine/PrefixManager.cs`
- `Wine/RunnerResolver.cs`
- `Wine/RuntimeModel.cs`
- `Wine/RuntimeResolver.cs`
- `Wine/RuntimeSettings.cs`
- `Wine/WineAccountService.cs`
- `Wine/WineRegistryInstallPathProvider.cs`

