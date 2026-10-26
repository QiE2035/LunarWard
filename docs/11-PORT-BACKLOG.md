# 11 — 移植清单（上游 src/Starward → 本仓库 src/LunarWard）

> 由 `python3 scripts/port-backlog.py --write` 生成，勿手改表格；人工结论写在每节标题下的「备注」里。

上游文件 **314** 个，本地已有同名落点 **22** 个，**缺 292** 个；本地独有文件 **228** 个（需并入 Features 或删除）。

判定标准：上游相对路径在本地存在（`.xaml` ↔ `.axaml`）。「有文件」只说明路径对齐，内容是否已按上游实现，需要逐节核对。

## 总览

| 分组 | 上游文件 | 已有 | 缺失 |
|---|---:|---:|---:|
| Features/GameRecord | 47 | 0 | 47 |
| Features/Gacha | 40 | 0 | 40 |
| Features/GameLauncher | 26 | 0 | 26 |
| Features/Setting | 25 | 19 | 6 |
| Features/Screenshot | 23 | 0 | 23 |
| Features/ViewHost | 20 | 0 | 20 |
| Helpers | 16 | 0 | 16 |
| Features/PlayTime | 14 | 0 | 14 |
| Features/Background | 13 | 0 | 13 |
| Converters | 11 | 0 | 11 |
| Features/GameInstall | 9 | 0 | 9 |
| Features/Codec | 8 | 0 | 8 |
| Features/Update | 8 | 0 | 8 |
| Features/GameSetting | 7 | 0 | 7 |
| Features/GameSelector | 6 | 0 | 6 |
| Controls | 5 | 0 | 5 |
| Features/GameAccount | 5 | 0 | 5 |
| Features/Overlay | 4 | 0 | 4 |
| Features/SelfQuery | 4 | 0 | 4 |
| Features/CloudGame | 3 | 0 | 3 |
| Features/GamepadControl | 3 | 0 | 3 |
| Features/UrlProtocol | 3 | 0 | 3 |
| Features/Database | 2 | 0 | 2 |
| Frameworks | 2 | 0 | 2 |
| App.xaml | 1 | 1 | 0 |
| App.xaml.cs | 1 | 1 | 0 |
| AppConfig.Common.cs | 1 | 0 | 1 |
| AppConfig.Configuration.cs | 1 | 0 | 1 |
| AppConfig.ServiceProvider.cs | 1 | 0 | 1 |
| AppConfig.Setting.cs | 1 | 0 | 1 |
| Features/GameFeatureConfig.cs | 1 | 0 | 1 |
| Features/HoYoPlay | 1 | 0 | 1 |
| Features/RPC | 1 | 0 | 1 |
| Program.cs | 1 | 1 | 0 |

## Features/GameRecord（0/47）

| 上游文件 | 本地落点 |
|---|---|
| `Features/GameRecord/AvatarRankHelper.cs` | ❌ 缺 |
| `Features/GameRecord/BBSWebBridge.xaml` | ❌ 缺 |
| `Features/GameRecord/BBSWebBridge.xaml.cs` | ❌ 缺 |
| `Features/GameRecord/BattleChronicleWindow.xaml` | ❌ 缺 |
| `Features/GameRecord/BattleChronicleWindow.xaml.cs` | ❌ 缺 |
| `Features/GameRecord/ColorToSolidColorBrushConverter.cs` | ❌ 缺 |
| `Features/GameRecord/DailyNote/DailyNoteButton.xaml` | ❌ 缺 |
| `Features/GameRecord/DailyNote/DailyNoteButton.xaml.cs` | ❌ 缺 |
| `Features/GameRecord/GameRecordPage.xaml` | ❌ 缺 |
| `Features/GameRecord/GameRecordPage.xaml.cs` | ❌ 缺 |
| `Features/GameRecord/GameRecordRoleChangedMessage.cs` | ❌ 缺 |
| `Features/GameRecord/GameRecordService.cs` | ❌ 缺 |
| `Features/GameRecord/GameRecordVerifyAccountMessage.cs` | ❌ 缺 |
| `Features/GameRecord/Genshin/ImaginariumTheaterPage.xaml` | ❌ 缺 |
| `Features/GameRecord/Genshin/ImaginariumTheaterPage.xaml.cs` | ❌ 缺 |
| `Features/GameRecord/Genshin/SpiralAbyssBattleAvatarsSelectConverter.cs` | ❌ 缺 |
| `Features/GameRecord/Genshin/SpiralAbyssPage.xaml` | ❌ 缺 |
| `Features/GameRecord/Genshin/SpiralAbyssPage.xaml.cs` | ❌ 缺 |
| `Features/GameRecord/Genshin/StygianOnslaughtPage.xaml` | ❌ 缺 |
| `Features/GameRecord/Genshin/StygianOnslaughtPage.xaml.cs` | ❌ 缺 |
| `Features/GameRecord/Genshin/TravelersDiaryPage.xaml` | ❌ 缺 |
| `Features/GameRecord/Genshin/TravelersDiaryPage.xaml.cs` | ❌ 缺 |
| `Features/GameRecord/LoginPage.xaml` | ❌ 缺 |
| `Features/GameRecord/LoginPage.xaml.cs` | ❌ 缺 |
| `Features/GameRecord/StarRail/ApocalypticShadowPage.xaml` | ❌ 缺 |
| `Features/GameRecord/StarRail/ApocalypticShadowPage.xaml.cs` | ❌ 缺 |
| `Features/GameRecord/StarRail/ChallengePeakPage.xaml` | ❌ 缺 |
| `Features/GameRecord/StarRail/ChallengePeakPage.xaml.cs` | ❌ 缺 |
| `Features/GameRecord/StarRail/ForgottenHallPage.xaml` | ❌ 缺 |
| `Features/GameRecord/StarRail/ForgottenHallPage.xaml.cs` | ❌ 缺 |
| `Features/GameRecord/StarRail/PureFictionPage.xaml` | ❌ 缺 |
| `Features/GameRecord/StarRail/PureFictionPage.xaml.cs` | ❌ 缺 |
| `Features/GameRecord/StarRail/SimulatedUniverseBuffBgConverter.cs` | ❌ 缺 |
| `Features/GameRecord/StarRail/SimulatedUniverseBuffIconConverter.cs` | ❌ 缺 |
| `Features/GameRecord/StarRail/SimulatedUniversePage.xaml` | ❌ 缺 |
| `Features/GameRecord/StarRail/SimulatedUniversePage.xaml.cs` | ❌ 缺 |
| `Features/GameRecord/StarRail/SimulatedUniverseRomanNumberConverter.cs` | ❌ 缺 |
| `Features/GameRecord/StarRail/SimulatedUniverseWorldIconConverter.cs` | ❌ 缺 |
| `Features/GameRecord/StarRail/TrailblazeCalendarPage.xaml` | ❌ 缺 |
| `Features/GameRecord/StarRail/TrailblazeCalendarPage.xaml.cs` | ❌ 缺 |
| `Features/GameRecord/ZZZ/DeadlyAssaultPage.xaml` | ❌ 缺 |
| `Features/GameRecord/ZZZ/DeadlyAssaultPage.xaml.cs` | ❌ 缺 |
| `Features/GameRecord/ZZZ/InterKnotMonthlyReportPage.xaml` | ❌ 缺 |
| `Features/GameRecord/ZZZ/InterKnotMonthlyReportPage.xaml.cs` | ❌ 缺 |
| `Features/GameRecord/ZZZ/ShiyuDefensePage.xaml` | ❌ 缺 |
| `Features/GameRecord/ZZZ/ShiyuDefensePage.xaml.cs` | ❌ 缺 |
| `Features/GameRecord/ZZZ/ZZZRarityToIconConverter.cs` | ❌ 缺 |

## Features/Gacha（0/40）

| 上游文件 | 本地落点 |
|---|---|
| `Features/Gacha/CloudGameGachaWindow.xaml` | ❌ 缺 |
| `Features/Gacha/CloudGameGachaWindow.xaml.cs` | ❌ 缺 |
| `Features/Gacha/DeleteGachaLogDialog.xaml` | ❌ 缺 |
| `Features/Gacha/DeleteGachaLogDialog.xaml.cs` | ❌ 缺 |
| `Features/Gacha/GachaBanner.cs` | ❌ 缺 |
| `Features/Gacha/GachaLogExportFile.cs` | ❌ 缺 |
| `Features/Gacha/GachaLogItemEx.cs` | ❌ 缺 |
| `Features/Gacha/GachaLogPage.xaml` | ❌ 缺 |
| `Features/Gacha/GachaLogPage.xaml.cs` | ❌ 缺 |
| `Features/Gacha/GachaLogService.cs` | ❌ 缺 |
| `Features/Gacha/GachaLogUrl.cs` | ❌ 缺 |
| `Features/Gacha/GachaNoUp.cs` | ❌ 缺 |
| `Features/Gacha/GachaPityProgressBackgroundBrushConverter.cs` | ❌ 缺 |
| `Features/Gacha/GachaStatsCard.xaml` | ❌ 缺 |
| `Features/Gacha/GachaStatsCard.xaml.cs` | ❌ 缺 |
| `Features/Gacha/GachaTypeStats.cs` | ❌ 缺 |
| `Features/Gacha/GenshinBeyondGachaPage.xaml` | ❌ 缺 |
| `Features/Gacha/GenshinBeyondGachaPage.xaml.cs` | ❌ 缺 |
| `Features/Gacha/GenshinBeyondGachaService.cs` | ❌ 缺 |
| `Features/Gacha/GenshinGachaService.cs` | ❌ 缺 |
| `Features/Gacha/RarityToGIBgConverter.cs` | ❌ 缺 |
| `Features/Gacha/RarityToSRBgConverter.cs` | ❌ 缺 |
| `Features/Gacha/RarityToZZZIconConverter.cs` | ❌ 缺 |
| `Features/Gacha/StarRailGachaService.cs` | ❌ 缺 |
| `Features/Gacha/UIGF/GachaUidArchiveDisplay.cs` | ❌ 缺 |
| `Features/Gacha/UIGF/UIGF3File.cs` | ❌ 缺 |
| `Features/Gacha/UIGF/UIGF4File.cs` | ❌ 缺 |
| `Features/Gacha/UIGF/UIGF4GachaWindow.xaml` | ❌ 缺 |
| `Features/Gacha/UIGF/UIGF4GachaWindow.xaml.cs` | ❌ 缺 |
| `Features/Gacha/UIGF/UIGF4ImportException.cs` | ❌ 缺 |
| `Features/Gacha/UIGF/UIGFGachaService.cs` | ❌ 缺 |
| `Features/Gacha/UIGF/UIGFGenshinGachaItem.cs` | ❌ 缺 |
| `Features/Gacha/UpdateGachaLogMessage.cs` | ❌ 缺 |
| `Features/Gacha/ZZZGachaService.cs` | ❌ 缺 |
| `Features/Gacha/ZZZGachaStatsCard.xaml` | ❌ 缺 |
| `Features/Gacha/ZZZGachaStatsCard.xaml.cs` | ❌ 缺 |
| `Features/Gacha/ZZZGachaToolbox/IconInfo.cs` | ❌ 缺 |
| `Features/Gacha/ZZZGachaToolbox/ItemList.cs` | ❌ 缺 |
| `Features/Gacha/ZZZGachaToolbox/ZZZGachaInfoWindow.xaml` | ❌ 缺 |
| `Features/Gacha/ZZZGachaToolbox/ZZZGachaInfoWindow.xaml.cs` | ❌ 缺 |

## Features/GameLauncher（0/26）

| 上游文件 | 本地落点 |
|---|---|
| `Features/GameLauncher/CompactCheckBoxStyle.xaml` | ❌ 缺 |
| `Features/GameLauncher/DX12IntroDialog.xaml` | ❌ 缺 |
| `Features/GameLauncher/DX12IntroDialog.xaml.cs` | ❌ 缺 |
| `Features/GameLauncher/GameAnnouncementSettingChangedMessage.cs` | ❌ 缺 |
| `Features/GameLauncher/GameAuthLoginButton.xaml` | ❌ 缺 |
| `Features/GameLauncher/GameAuthLoginButton.xaml.cs` | ❌ 缺 |
| `Features/GameLauncher/GameAuthLoginService.cs` | ❌ 缺 |
| `Features/GameLauncher/GameBannerAndPost.xaml` | ❌ 缺 |
| `Features/GameLauncher/GameBannerAndPost.xaml.cs` | ❌ 缺 |
| `Features/GameLauncher/GameConfigIni.cs` | ❌ 缺 |
| `Features/GameLauncher/GameInstallPathChangedMessage.cs` | ❌ 缺 |
| `Features/GameLauncher/GameLauncherPage.xaml` | ❌ 缺 |
| `Features/GameLauncher/GameLauncherPage.xaml.cs` | ❌ 缺 |
| `Features/GameLauncher/GameLauncherService.cs` | ❌ 缺 |
| `Features/GameLauncher/GameLauncherSettingDialog.xaml` | ❌ 缺 |
| `Features/GameLauncher/GameLauncherSettingDialog.xaml.cs` | ❌ 缺 |
| `Features/GameLauncher/GameNoticeService.cs` | ❌ 缺 |
| `Features/GameLauncher/GameNoticeWindow.xaml` | ❌ 缺 |
| `Features/GameLauncher/GameNoticeWindow.xaml.cs` | ❌ 缺 |
| `Features/GameLauncher/GameNoticeWindowClosedMessage.cs` | ❌ 缺 |
| `Features/GameLauncher/GamePostGroup.cs` | ❌ 缺 |
| `Features/GameLauncher/GameStartedMessage.cs` | ❌ 缺 |
| `Features/GameLauncher/GameState.cs` | ❌ 缺 |
| `Features/GameLauncher/StartGameAction.cs` | ❌ 缺 |
| `Features/GameLauncher/StartGameButton.xaml` | ❌ 缺 |
| `Features/GameLauncher/StartGameButton.xaml.cs` | ❌ 缺 |

## Features/Setting（19/25）

| 上游文件 | 本地落点 |
|---|---|
| `Features/Setting/AboutSetting.xaml` | `Features/Setting/AboutSetting.axaml` |
| `Features/Setting/AboutSetting.xaml.cs` | `Features/Setting/AboutSetting.axaml.cs` |
| `Features/Setting/AdvancedSetting.xaml` | `Features/Setting/AdvancedSetting.axaml` |
| `Features/Setting/AdvancedSetting.xaml.cs` | `Features/Setting/AdvancedSetting.axaml.cs` |
| `Features/Setting/DownloadSetting.xaml` | `Features/Setting/DownloadSetting.axaml` |
| `Features/Setting/DownloadSetting.xaml.cs` | `Features/Setting/DownloadSetting.axaml.cs` |
| `Features/Setting/FileManageSetting.xaml` | `Features/Setting/FileManageSetting.axaml` |
| `Features/Setting/FileManageSetting.xaml.cs` | `Features/Setting/FileManageSetting.axaml.cs` |
| `Features/Setting/GamepadControlSetting.xaml` | ❌ 缺 |
| `Features/Setting/GamepadControlSetting.xaml.cs` | ❌ 缺 |
| `Features/Setting/GeneralSetting.xaml` | `Features/Setting/GeneralSetting.axaml` |
| `Features/Setting/GeneralSetting.xaml.cs` | `Features/Setting/GeneralSetting.axaml.cs` |
| `Features/Setting/HotkeyInput.xaml` | ❌ 缺 |
| `Features/Setting/HotkeyInput.xaml.cs` | ❌ 缺 |
| `Features/Setting/HotkeyManager.cs` | ❌ 缺 |
| `Features/Setting/HotkeySetting.xaml` | `Features/Setting/HotkeySetting.axaml` |
| `Features/Setting/HotkeySetting.xaml.cs` | `Features/Setting/HotkeySetting.axaml.cs` |
| `Features/Setting/LanguageChangedMessage.cs` | ❌ 缺 |
| `Features/Setting/ScreenshotSetting.xaml` | `Features/Setting/ScreenshotSetting.axaml` |
| `Features/Setting/ScreenshotSetting.xaml.cs` | `Features/Setting/ScreenshotSetting.axaml.cs` |
| `Features/Setting/SettingPage.xaml` | `Features/Setting/SettingPage.axaml` |
| `Features/Setting/SettingPage.xaml.cs` | `Features/Setting/SettingPage.axaml.cs` |
| `Features/Setting/ToolboxItem.cs` | `Features/Setting/ToolboxItem.cs` |
| `Features/Setting/ToolboxSetting.xaml` | `Features/Setting/ToolboxSetting.axaml` |
| `Features/Setting/ToolboxSetting.xaml.cs` | `Features/Setting/ToolboxSetting.axaml.cs` |

## Features/Screenshot（0/23）

| 上游文件 | 本地落点 |
|---|---|
| `Features/Screenshot/ImageBatchConvertWindow.xaml` | ❌ 缺 |
| `Features/Screenshot/ImageBatchConvertWindow.xaml.cs` | ❌ 缺 |
| `Features/Screenshot/ImageViewWindow.xaml` | ❌ 缺 |
| `Features/Screenshot/ImageViewWindow.xaml.cs` | ❌ 缺 |
| `Features/Screenshot/ImageViewWindow2.xaml` | ❌ 缺 |
| `Features/Screenshot/ImageViewWindow2.xaml.cs` | ❌ 缺 |
| `Features/Screenshot/ScreenCaptureContext.cs` | ❌ 缺 |
| `Features/Screenshot/ScreenCaptureHelper.cs` | ❌ 缺 |
| `Features/Screenshot/ScreenCaptureInfoWindow.xaml` | ❌ 缺 |
| `Features/Screenshot/ScreenCaptureInfoWindow.xaml.cs` | ❌ 缺 |
| `Features/Screenshot/ScreenCaptureItem.cs` | ❌ 缺 |
| `Features/Screenshot/ScreenCaptureService.cs` | ❌ 缺 |
| `Features/Screenshot/ScreenshotFolder.cs` | ❌ 缺 |
| `Features/Screenshot/ScreenshotFolderManageDialog.xaml` | ❌ 缺 |
| `Features/Screenshot/ScreenshotFolderManageDialog.xaml.cs` | ❌ 缺 |
| `Features/Screenshot/ScreenshotHelper.cs` | ❌ 缺 |
| `Features/Screenshot/ScreenshotItem.cs` | ❌ 缺 |
| `Features/Screenshot/ScreenshotPage.xaml` | ❌ 缺 |
| `Features/Screenshot/ScreenshotPage.xaml.cs` | ❌ 缺 |
| `Features/Screenshot/ScreenshotPage2.xaml` | ❌ 缺 |
| `Features/Screenshot/ScreenshotPage2.xaml.cs` | ❌ 缺 |
| `Features/Screenshot/ScrollViewerExtensions.cs` | ❌ 缺 |
| `Features/Screenshot/ZoomFactorPercentConverter.cs` | ❌ 缺 |

## Features/ViewHost（0/20）

| 上游文件 | 本地落点 |
|---|---|
| `Features/ViewHost/BH3GlobalGameServerChangedMessage.cs` | ❌ 缺 |
| `Features/ViewHost/BlankPage.xaml` | ❌ 缺 |
| `Features/ViewHost/BlankPage.xaml.cs` | ❌ 缺 |
| `Features/ViewHost/MainView.xaml` | ❌ 缺 |
| `Features/ViewHost/MainView.xaml.cs` | ❌ 缺 |
| `Features/ViewHost/MainViewNavigateMessage.cs` | ❌ 缺 |
| `Features/ViewHost/MainWindow.xaml` | ❌ 缺 |
| `Features/ViewHost/MainWindow.xaml.cs` | ❌ 缺 |
| `Features/ViewHost/MainWindowCloseDialog.xaml` | ❌ 缺 |
| `Features/ViewHost/MainWindowCloseDialog.xaml.cs` | ❌ 缺 |
| `Features/ViewHost/MainWindowCloseOption.cs` | ❌ 缺 |
| `Features/ViewHost/MainWindowStateChangedMessage.cs` | ❌ 缺 |
| `Features/ViewHost/NoPermissionWindow.xaml` | ❌ 缺 |
| `Features/ViewHost/NoPermissionWindow.xaml.cs` | ❌ 缺 |
| `Features/ViewHost/RemovableStorageDeviceChangedMessage.cs` | ❌ 缺 |
| `Features/ViewHost/SystemTrayWindow.xaml` | ❌ 缺 |
| `Features/ViewHost/SystemTrayWindow.xaml.cs` | ❌ 缺 |
| `Features/ViewHost/WelcomePageFinishedMessage.cs` | ❌ 缺 |
| `Features/ViewHost/WelcomeWindow.xaml` | ❌ 缺 |
| `Features/ViewHost/WelcomeWindow.xaml.cs` | ❌ 缺 |

## Helpers（0/16）

| 上游文件 | 本地落点 |
|---|---|
| `Helpers/AccentColorHelper.cs` | ❌ 缺 |
| `Helpers/BindConverter.cs` | ❌ 缺 |
| `Helpers/ClipboardHelper.cs` | ❌ 缺 |
| `Helpers/DriveHelper.cs` | ❌ 缺 |
| `Helpers/Enumeration/EnumItemsSource.cs` | ❌ 缺 |
| `Helpers/Enumeration/LocalizationKeyAttribute.cs` | ❌ 缺 |
| `Helpers/FileCache.cs` | ❌ 缺 |
| `Helpers/FileDialogHelper.cs` | ❌ 缺 |
| `Helpers/FileDialogNative.cs` | ❌ 缺 |
| `Helpers/FileHashHelper.cs` | ❌ 缺 |
| `Helpers/InAppToast.cs` | ❌ 缺 |
| `Helpers/PointerCursor.cs` | ❌ 缺 |
| `Helpers/SystemBackdropHelper.cs` | ❌ 缺 |
| `Helpers/SystemUIHelper.cs` | ❌ 缺 |
| `Helpers/TransparentBackdrop.cs` | ❌ 缺 |
| `Helpers/XamlRootExtension.cs` | ❌ 缺 |

## Features/PlayTime（0/14）

| 上游文件 | 本地落点 |
|---|---|
| `Features/PlayTime/Charts/BarChart.cs` | ❌ 缺 |
| `Features/PlayTime/Charts/CalendarHeatmap.cs` | ❌ 缺 |
| `Features/PlayTime/Charts/ChartHelpers.cs` | ❌ 缺 |
| `Features/PlayTime/Charts/ChartItems.cs` | ❌ 缺 |
| `Features/PlayTime/Charts/HoverCard.cs` | ❌ 缺 |
| `Features/PlayTime/PlayTimeButton.xaml` | ❌ 缺 |
| `Features/PlayTime/PlayTimeButton.xaml.cs` | ❌ 缺 |
| `Features/PlayTime/PlayTimeItem.cs` | ❌ 缺 |
| `Features/PlayTime/PlayTimeRecordService.cs` | ❌ 缺 |
| `Features/PlayTime/PlayTimeState.cs` | ❌ 缺 |
| `Features/PlayTime/PlayTimeStatsDialog.xaml` | ❌ 缺 |
| `Features/PlayTime/PlayTimeStatsDialog.xaml.cs` | ❌ 缺 |
| `Features/PlayTime/PlayTimeStatsService.cs` | ❌ 缺 |
| `Features/PlayTime/StatCardItem.cs` | ❌ 缺 |

## Features/Background（0/13）

| 上游文件 | 本地落点 |
|---|---|
| `Features/Background/AccentColorChangedMessage.cs` | ❌ 缺 |
| `Features/Background/AccentColorHelper.cs` | ❌ 缺 |
| `Features/Background/AppBackground.xaml` | ❌ 缺 |
| `Features/Background/AppBackground.xaml.cs` | ❌ 缺 |
| `Features/Background/AppBackgroundImageExStyle.xaml` | ❌ 缺 |
| `Features/Background/BackgroundChangedMessage.cs` | ❌ 缺 |
| `Features/Background/BackgroundFileItem.cs` | ❌ 缺 |
| `Features/Background/BackgroundService.cs` | ❌ 缺 |
| `Features/Background/BackgroundViewPage.xaml` | ❌ 缺 |
| `Features/Background/BackgroundViewPage.xaml.cs` | ❌ 缺 |
| `Features/Background/BackgroundViewWindow.xaml` | ❌ 缺 |
| `Features/Background/BackgroundViewWindow.xaml.cs` | ❌ 缺 |
| `Features/Background/VideoBgVolumeChangedMessage.cs` | ❌ 缺 |

## Converters（0/11）

| 上游文件 | 本地落点 |
|---|---|
| `Converters/BoolReversedConverter.cs` | ❌ 缺 |
| `Converters/BoolToVisibilityConverter.cs` | ❌ 缺 |
| `Converters/ByteLengthToStringConverter.cs` | ❌ 缺 |
| `Converters/CollectionToVisibilityConverter.cs` | ❌ 缺 |
| `Converters/ColorToSolidColorBrushConverter.cs` | ❌ 缺 |
| `Converters/Int32NotZeroToVisibilityConverter.cs` | ❌ 缺 |
| `Converters/ObjectToBoolConverter.cs` | ❌ 缺 |
| `Converters/ObjectToVisibilityConverter.cs` | ❌ 缺 |
| `Converters/TimeSpanToStringConverter.cs` | ❌ 缺 |
| `Converters/VisibilityToBoolConverter.cs` | ❌ 缺 |
| `Converters/WhiteSpaceStringToVisibilityConverter.cs` | ❌ 缺 |

## Features/GameInstall（0/9）

| 上游文件 | 本地落点 |
|---|---|
| `Features/GameInstall/GameInstallService.cs` | ❌ 缺 |
| `Features/GameInstall/GameInstallTaskStartedMessage.cs` | ❌ 缺 |
| `Features/GameInstall/GamePackageService.cs` | ❌ 缺 |
| `Features/GameInstall/InstallGameDialog.xaml` | ❌ 缺 |
| `Features/GameInstall/InstallGameDialog.xaml.cs` | ❌ 缺 |
| `Features/GameInstall/PreDownloadButton.xaml` | ❌ 缺 |
| `Features/GameInstall/PreDownloadButton.xaml.cs` | ❌ 缺 |
| `Features/GameInstall/PreDownloadDialog.xaml` | ❌ 缺 |
| `Features/GameInstall/PreDownloadDialog.xaml.cs` | ❌ 缺 |

## Features/Codec（0/8）

| 上游文件 | 本地落点 |
|---|---|
| `Features/Codec/HDR10GammaEffect.cs` | ❌ 缺 |
| `Features/Codec/ImageInfo.cs` | ❌ 缺 |
| `Features/Codec/ImageLoader.cs` | ❌ 缺 |
| `Features/Codec/ImageSaver.cs` | ❌ 缺 |
| `Features/Codec/ImageThumbnail.cs` | ❌ 缺 |
| `Features/Codec/SrgbGammaEffect.cs` | ❌ 缺 |
| `Features/Codec/UhdrGainmapEffect.cs` | ❌ 缺 |
| `Features/Codec/VP9Helper.cs` | ❌ 缺 |

## Features/Update（0/8）

| 上游文件 | 本地落点 |
|---|---|
| `Features/Update/LogUploadClient.cs` | ❌ 缺 |
| `Features/Update/LogUploadRequestBase.cs` | ❌ 缺 |
| `Features/Update/LogUploadService.cs` | ❌ 缺 |
| `Features/Update/SetupService.cs` | ❌ 缺 |
| `Features/Update/UpdateService.cs` | ❌ 缺 |
| `Features/Update/UpdateState.cs` | ❌ 缺 |
| `Features/Update/UpdateWindow.xaml` | ❌ 缺 |
| `Features/Update/UpdateWindow.xaml.cs` | ❌ 缺 |

## Features/GameSetting（0/7）

| 上游文件 | 本地落点 |
|---|---|
| `Features/GameSetting/GameSettingPage.xaml` | ❌ 缺 |
| `Features/GameSetting/GameSettingPage.xaml.cs` | ❌ 缺 |
| `Features/GameSetting/GameSettingService.cs` | ❌ 缺 |
| `Features/GameSetting/GenshinHDRLuminanceSettingDialog.xaml` | ❌ 缺 |
| `Features/GameSetting/GenshinHDRLuminanceSettingDialog.xaml.cs` | ❌ 缺 |
| `Features/GameSetting/GraphicsSettings_Model_h2986158309.cs` | ❌ 缺 |
| `Features/GameSetting/GraphicsSettings_PCResolution_h431323223.cs` | ❌ 缺 |

## Features/GameSelector（0/6）

| 上游文件 | 本地落点 |
|---|---|
| `Features/GameSelector/GameBizDisplay.cs` | ❌ 缺 |
| `Features/GameSelector/GameBizIcon.cs` | ❌ 缺 |
| `Features/GameSelector/GameSelector.xaml` | ❌ 缺 |
| `Features/GameSelector/GameSelector.xaml.cs` | ❌ 缺 |
| `Features/GameSelector/MainWindowDragRectAdaptToGameIconMessage.cs` | ❌ 缺 |
| `Features/GameSelector/NoAnimationImageExStyle.xaml` | ❌ 缺 |

## Controls（0/5）

| 上游文件 | 本地落点 |
|---|---|
| `Controls/CachedImage.cs` | ❌ 缺 |
| `Controls/ColorRectChart.xaml` | ❌ 缺 |
| `Controls/ColorRectChart.xaml.cs` | ❌ 缺 |
| `Controls/ColorfulTextBlock.xaml` | ❌ 缺 |
| `Controls/ColorfulTextBlock.xaml.cs` | ❌ 缺 |

## Features/GameAccount（0/5）

| 上游文件 | 本地落点 |
|---|---|
| `Features/GameAccount/GameAccount.cs` | ❌ 缺 |
| `Features/GameAccount/GameAccountService.cs` | ❌ 缺 |
| `Features/GameAccount/GameAccountSwitcher.xaml` | ❌ 缺 |
| `Features/GameAccount/GameAccountSwitcher.xaml.cs` | ❌ 缺 |
| `Features/GameAccount/GameAccountUidStringConverter.cs` | ❌ 缺 |

## Features/Overlay（0/4）

| 上游文件 | 本地落点 |
|---|---|
| `Features/Overlay/OverlayWindow.xaml` | ❌ 缺 |
| `Features/Overlay/OverlayWindow.xaml.cs` | ❌ 缺 |
| `Features/Overlay/RunningGame.cs` | ❌ 缺 |
| `Features/Overlay/RunningGameService.cs` | ❌ 缺 |

## Features/SelfQuery（0/4）

| 上游文件 | 本地落点 |
|---|---|
| `Features/SelfQuery/SelfQueryPage.xaml` | ❌ 缺 |
| `Features/SelfQuery/SelfQueryPage.xaml.cs` | ❌ 缺 |
| `Features/SelfQuery/SelfQueryService.cs` | ❌ 缺 |
| `Features/SelfQuery/SelfQueryStatsNumberConverter.cs` | ❌ 缺 |

## Features/CloudGame（0/3）

| 上游文件 | 本地落点 |
|---|---|
| `Features/CloudGame/CloudGameButton.xaml` | ❌ 缺 |
| `Features/CloudGame/CloudGameButton.xaml.cs` | ❌ 缺 |
| `Features/CloudGame/CloudGameService.cs` | ❌ 缺 |

## Features/GamepadControl（0/3）

| 上游文件 | 本地落点 |
|---|---|
| `Features/GamepadControl/GamepadController.cs` | ❌ 缺 |
| `Features/GamepadControl/SimulateInputTipsWindow.xaml` | ❌ 缺 |
| `Features/GamepadControl/SimulateInputTipsWindow.xaml.cs` | ❌ 缺 |

## Features/UrlProtocol（0/3）

| 上游文件 | 本地落点 |
|---|---|
| `Features/UrlProtocol/TestUrlProtocolWindow.xaml` | ❌ 缺 |
| `Features/UrlProtocol/TestUrlProtocolWindow.xaml.cs` | ❌ 缺 |
| `Features/UrlProtocol/UrlProtocolService.cs` | ❌ 缺 |

## Features/Database（0/2）

| 上游文件 | 本地落点 |
|---|---|
| `Features/Database/DapperSqlMapper.cs` | ❌ 缺 |
| `Features/Database/DatabaseService.cs` | ❌ 缺 |

## Frameworks（0/2）

| 上游文件 | 本地落点 |
|---|---|
| `Frameworks/PageBase.cs` | ❌ 缺 |
| `Frameworks/WindowEx.cs` | ❌ 缺 |

## App.xaml（1/1）

路径已全部对齐（内容仍需逐文件核对）。

## App.xaml.cs（1/1）

路径已全部对齐（内容仍需逐文件核对）。

## AppConfig.Common.cs（0/1）

| 上游文件 | 本地落点 |
|---|---|
| `AppConfig.Common.cs` | ❌ 缺 |

## AppConfig.Configuration.cs（0/1）

| 上游文件 | 本地落点 |
|---|---|
| `AppConfig.Configuration.cs` | ❌ 缺 |

## AppConfig.ServiceProvider.cs（0/1）

| 上游文件 | 本地落点 |
|---|---|
| `AppConfig.ServiceProvider.cs` | ❌ 缺 |

## AppConfig.Setting.cs（0/1）

| 上游文件 | 本地落点 |
|---|---|
| `AppConfig.Setting.cs` | ❌ 缺 |

## Features/GameFeatureConfig.cs（0/1）

| 上游文件 | 本地落点 |
|---|---|
| `Features/GameFeatureConfig.cs` | ❌ 缺 |

## Features/HoYoPlay（0/1）

| 上游文件 | 本地落点 |
|---|---|
| `Features/HoYoPlay/HoYoPlayService.cs` | ❌ 缺 |

## Features/RPC（0/1）

| 上游文件 | 本地落点 |
|---|---|
| `Features/RPC/RpcService.cs` | ❌ 缺 |

## Program.cs（1/1）

路径已全部对齐（内容仍需逐文件核对）。

## 本地独有文件（上游无对应）

按 D1（严格移植）的判定：这些文件要么并入对应 Features 目录并改成上游同名文件，要么删除（若其职责由上游文件覆盖），要么登记为平台等价物。

- `App.axaml`
- `App.axaml.cs`
- `AppServices.cs`
- `Controls/ColorRectChart.cs`
- `Controls/GachaStatsCard.axaml`
- `Controls/GachaStatsCard.axaml.cs`
- `Controls/PlayTimeBarChart.cs`
- `Controls/PlayTimeCalendarHeatmap.cs`
- `Controls/ZZZGachaStatsCard.axaml`
- `Controls/ZZZGachaStatsCard.axaml.cs`
- `Features/Setting/AboutSetting.axaml`
- `Features/Setting/AboutSetting.axaml.cs`
- `Features/Setting/AdvancedSetting.axaml`
- `Features/Setting/AdvancedSetting.axaml.cs`
- `Features/Setting/DownloadSetting.axaml`
- `Features/Setting/DownloadSetting.axaml.cs`
- `Features/Setting/FileManageSetting.axaml`
- `Features/Setting/FileManageSetting.axaml.cs`
- `Features/Setting/GamepadSetting.axaml`
- `Features/Setting/GamepadSetting.axaml.cs`
- `Features/Setting/GeneralSetting.axaml`
- `Features/Setting/GeneralSetting.axaml.cs`
- `Features/Setting/HotkeySetting.axaml`
- `Features/Setting/HotkeySetting.axaml.cs`
- `Features/Setting/ScreenshotSetting.axaml`
- `Features/Setting/ScreenshotSetting.axaml.cs`
- `Features/Setting/SettingLanguageOption.cs`
- `Features/Setting/SettingPage.axaml`
- `Features/Setting/SettingPage.axaml.cs`
- `Features/Setting/SettingPageBase.cs`
- `Features/Setting/ToolboxSetting.axaml`
- `Features/Setting/ToolboxSetting.axaml.cs`
- `Frameworks/WebView/CoreWebView2Compat.cs`
- `Frameworks/WebView/GtkRuntime.cs`
- `Frameworks/WebView/Interop/NativeMethods.cs`
- `Frameworks/WebView/VirtualHostMapping.cs`
- `Frameworks/WebView/WebKitWebViewCore.cs`
- `Frameworks/WebView/WebView.cs`
- `Frameworks/WebView/WebViewAvailability.cs`
- `Localization/AppStrings.cs`
- `Services/AnnouncementService.cs`
- `Services/AppVersion.cs`
- `Services/BackgroundCatalog.cs`
- `Services/BackgroundService.cs`
- `Services/CaptureMode.cs`
- `Services/CloseWindowDecision.cs`
- `Services/DialogReentrancyGate.cs`
- `Services/DialogService.cs`
- `Services/GachaIconCache.cs`
- `Services/GameAssets.cs`
- `Services/GameScanner.cs`
- `Services/GameSettingRegistry.cs`
- `Services/GameUninstaller.cs`
- `Services/InAppToast.cs`
- `Services/InstallerLogAdapter.cs`
- `Services/LauncherService.cs`
- `Services/ReleaseChecker.cs`
- `Services/SingleInstance.cs`
- `Services/StartupCommands.cs`
- `Services/StarwardAssets.cs`
- `Services/ToolboxDemo.cs`
- `Services/TrayService.cs`
- `Services/VideoBackgroundPlayer.cs`
- `Services/WineRegistry.cs`
- `Styles/Starward.axaml`
- `SupportedPlatform.cs`
- `ViewModels/AccountViewModel.cs`
- `ViewModels/DailyNoteViewModel.cs`
- `ViewModels/DiagnosticViewModel.cs`
- `ViewModels/EnvironmentShellViewModel.cs`
- `ViewModels/ProfileItemViewModel.cs`
- `ViewModels/ScreenshotViewModel.cs`
- `ViewModels/SettingsViewModel.cs`
- `ViewModels/Starward/ApocalypticShadowPageViewModel.cs`
- `ViewModels/Starward/ChallengePeakPageViewModel.cs`
- `ViewModels/Starward/DailyNoteButtonViewModel.cs`
- `ViewModels/Starward/DeadlyAssaultPageViewModel.cs`
- `ViewModels/Starward/DeleteGachaLogDialogViewModel.cs`
- `ViewModels/Starward/ForgottenHallPageViewModel.cs`
- `ViewModels/Starward/GachaPageViewModel.cs`
- `ViewModels/Starward/GachaPoolCardViewModel.cs`
- `ViewModels/Starward/GachaStatsViewModels.cs`
- `ViewModels/Starward/GameAuthLoginViewModel.cs`
- `ViewModels/Starward/GameChoiceViewModel.cs`
- `ViewModels/Starward/GameLauncherPageViewModel.cs`
- `ViewModels/Starward/GameSettingOptions.cs`
- `ViewModels/Starward/GameSettingPageViewModel.cs`
- `ViewModels/Starward/GameSettingProgramViewModel.cs`
- `ViewModels/Starward/GenshinBeyondGachaPageViewModel.cs`
- `ViewModels/Starward/ImageViewWindow2State.cs`
- `ViewModels/Starward/ImageViewWindow2ViewModel.cs`
- `ViewModels/Starward/ImaginariumTheaterPageViewModel.cs`
- `ViewModels/Starward/InterKnotMonthlyReportPageViewModel.cs`
- `ViewModels/Starward/PredownloadButtonState.cs`
- `ViewModels/Starward/PureFictionPageViewModel.cs`
- `ViewModels/Starward/ScreenshotFolderItem.cs`
- `ViewModels/Starward/ScreenshotFolderManageDialogViewModel.cs`
- `ViewModels/Starward/ScreenshotMonthIndex.cs`
- `ViewModels/Starward/ScreenshotPageViewModel.cs`
- `ViewModels/Starward/SelfQueryPageViewModel.cs`
- `ViewModels/Starward/SettingPageViewModel.cs`
- `ViewModels/Starward/SettingShellActions.cs`
- `ViewModels/Starward/Settings/SettingAboutViewModel.cs`
- `ViewModels/Starward/Settings/SettingAdvancedViewModel.cs`
- `ViewModels/Starward/Settings/SettingDownloadViewModel.cs`
- `ViewModels/Starward/Settings/SettingFileManageViewModel.cs`
- `ViewModels/Starward/Settings/SettingGamepadViewModel.cs`
- `ViewModels/Starward/Settings/SettingGeneralViewModel.cs`
- `ViewModels/Starward/Settings/SettingHotkeyViewModel.cs`
- `ViewModels/Starward/Settings/SettingLanguageOption.cs`
- `ViewModels/Starward/Settings/SettingScreenshotViewModel.cs`
- `ViewModels/Starward/Settings/SettingToolboxViewModel.cs`
- `ViewModels/Starward/ShellViewModel.cs`
- `ViewModels/Starward/ShiyuDefensePageViewModel.cs`
- `ViewModels/Starward/SimulatedUniversePageViewModel.cs`
- `ViewModels/Starward/SpiralAbyssPageViewModel.cs`
- `ViewModels/Starward/StarwardPageViewModel.cs`
- `ViewModels/Starward/StygianOnslaughtPageViewModel.cs`
- `ViewModels/Starward/ToolboxPageViewModel.cs`
- `ViewModels/Starward/TrailblazeCalendarPageViewModel.cs`
- `ViewModels/Starward/TravelersDiaryPageViewModel.cs`
- `ViewModels/Starward/Uigf4GachaWindowViewModel.cs`
- `Views/AccountWindow.axaml`
- `Views/AccountWindow.axaml.cs`
- `Views/DailyNoteWindow.axaml`
- `Views/DailyNoteWindow.axaml.cs`
- `Views/DiagnosticWindow.axaml`
- `Views/DiagnosticWindow.axaml.cs`
- `Views/SettingsWindow.axaml`
- `Views/SettingsWindow.axaml.cs`
- `Views/Starward/BackgroundGalleryItem.cs`
- `Views/Starward/BackgroundViewDialog.axaml`
- `Views/Starward/BackgroundViewDialog.axaml.cs`
- `Views/Starward/Controls/ColorRectChart.cs`
- `Views/Starward/Controls/StarwardContentDialog.axaml`
- `Views/Starward/Controls/StarwardContentDialog.axaml.cs`
- `Views/Starward/DX12IntroDialog.axaml`
- `Views/Starward/DX12IntroDialog.axaml.cs`
- `Views/Starward/DeleteGachaLogDialog.axaml`
- `Views/Starward/DeleteGachaLogDialog.axaml.cs`
- `Views/Starward/GameLauncherSettingDialog.axaml`
- `Views/Starward/GameLauncherSettingDialog.axaml.cs`
- `Views/Starward/ImageViewWindow2.axaml`
- `Views/Starward/ImageViewWindow2.axaml.cs`
- `Views/Starward/InstallGameDialog.axaml`
- `Views/Starward/InstallGameDialog.axaml.cs`
- `Views/Starward/Legacy/ShellWindow.axaml`
- `Views/Starward/Legacy/ShellWindow.axaml.cs`
- `Views/Starward/MainWindow.axaml`
- `Views/Starward/MainWindow.axaml.cs`
- `Views/Starward/MainWindowCloseDialog.axaml`
- `Views/Starward/MainWindowCloseDialog.axaml.cs`
- `Views/Starward/NavigationRail.axaml`
- `Views/Starward/NavigationRail.axaml.cs`
- `Views/Starward/PageContracts.cs`
- `Views/Starward/Pages/GachaPage.axaml`
- `Views/Starward/Pages/GachaPage.axaml.cs`
- `Views/Starward/Pages/GameLauncherPage.axaml`
- `Views/Starward/Pages/GameLauncherPage.axaml.cs`
- `Views/Starward/Pages/GameSettingPage.axaml`
- `Views/Starward/Pages/GameSettingPage.axaml.cs`
- `Views/Starward/Pages/GenshinBeyondGachaPage.axaml`
- `Views/Starward/Pages/GenshinBeyondGachaPage.axaml.cs`
- `Views/Starward/Pages/Record/ApocalypticShadowPage.axaml`
- `Views/Starward/Pages/Record/ApocalypticShadowPage.axaml.cs`
- `Views/Starward/Pages/Record/ChallengePeakPage.axaml`
- `Views/Starward/Pages/Record/ChallengePeakPage.axaml.cs`
- `Views/Starward/Pages/Record/DeadlyAssaultPage.axaml`
- `Views/Starward/Pages/Record/DeadlyAssaultPage.axaml.cs`
- `Views/Starward/Pages/Record/ForgottenHallPage.axaml`
- `Views/Starward/Pages/Record/ForgottenHallPage.axaml.cs`
- `Views/Starward/Pages/Record/ImaginariumTheaterPage.axaml`
- `Views/Starward/Pages/Record/ImaginariumTheaterPage.axaml.cs`
- `Views/Starward/Pages/Record/InterKnotMonthlyReportPage.axaml`
- `Views/Starward/Pages/Record/InterKnotMonthlyReportPage.axaml.cs`
- `Views/Starward/Pages/Record/PureFictionPage.axaml`
- `Views/Starward/Pages/Record/PureFictionPage.axaml.cs`
- `Views/Starward/Pages/Record/ShiyuDefensePage.axaml`
- `Views/Starward/Pages/Record/ShiyuDefensePage.axaml.cs`
- `Views/Starward/Pages/Record/SimulatedUniversePage.axaml`
- `Views/Starward/Pages/Record/SimulatedUniversePage.axaml.cs`
- `Views/Starward/Pages/Record/SpiralAbyssPage.axaml`
- `Views/Starward/Pages/Record/SpiralAbyssPage.axaml.cs`
- `Views/Starward/Pages/Record/StygianOnslaughtPage.axaml`
- `Views/Starward/Pages/Record/StygianOnslaughtPage.axaml.cs`
- `Views/Starward/Pages/Record/TrailblazeCalendarPage.axaml`
- `Views/Starward/Pages/Record/TrailblazeCalendarPage.axaml.cs`
- `Views/Starward/Pages/Record/TravelersDiaryPage.axaml`
- `Views/Starward/Pages/Record/TravelersDiaryPage.axaml.cs`
- `Views/Starward/Pages/ScreenshotPage.axaml`
- `Views/Starward/Pages/ScreenshotPage.axaml.cs`
- `Views/Starward/Pages/SelfQueryPage.axaml`
- `Views/Starward/Pages/SelfQueryPage.axaml.cs`
- `Views/Starward/Pages/SettingPage.axaml`
- `Views/Starward/Pages/SettingPage.axaml.cs`
- `Views/Starward/Pages/ToolboxPage.axaml`
- `Views/Starward/Pages/ToolboxPage.axaml.cs`
- `Views/Starward/PlayTimeStatsDialog.axaml`
- `Views/Starward/PlayTimeStatsDialog.axaml.cs`
- `Views/Starward/PreDownloadDialog.axaml`
- `Views/Starward/PreDownloadDialog.axaml.cs`
- `Views/Starward/ScreenshotFolderManageDialog.axaml`
- `Views/Starward/ScreenshotFolderManageDialog.axaml.cs`
- `Views/Starward/Settings/SettingAboutView.axaml`
- `Views/Starward/Settings/SettingAboutView.axaml.cs`
- `Views/Starward/Settings/SettingAdvancedView.axaml`
- `Views/Starward/Settings/SettingAdvancedView.axaml.cs`
- `Views/Starward/Settings/SettingDownloadView.axaml`
- `Views/Starward/Settings/SettingDownloadView.axaml.cs`
- `Views/Starward/Settings/SettingFileManageView.axaml`
- `Views/Starward/Settings/SettingFileManageView.axaml.cs`
- `Views/Starward/Settings/SettingGamepadView.axaml`
- `Views/Starward/Settings/SettingGamepadView.axaml.cs`
- `Views/Starward/Settings/SettingGeneralView.axaml`
- `Views/Starward/Settings/SettingGeneralView.axaml.cs`
- `Views/Starward/Settings/SettingHotkeyView.axaml`
- `Views/Starward/Settings/SettingHotkeyView.axaml.cs`
- `Views/Starward/Settings/SettingScreenshotView.axaml`
- `Views/Starward/Settings/SettingScreenshotView.axaml.cs`
- `Views/Starward/Settings/SettingSectionView.cs`
- `Views/Starward/Settings/SettingToolboxView.axaml`
- `Views/Starward/Settings/SettingToolboxView.axaml.cs`
- `Views/Starward/StarwardPage.cs`
- `Views/Starward/StarwardShell.axaml`
- `Views/Starward/StarwardShell.axaml.cs`
- `Views/Starward/UiTypography.cs`
- `Views/Starward/Uigf4GachaWindow.axaml`
- `Views/Starward/Uigf4GachaWindow.axaml.cs`

