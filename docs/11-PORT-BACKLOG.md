# 11 — 移植清单（上游 src/Starward → 本仓库 src/LunarWard）

> 由 `python3 scripts/port-backlog.py --write` 生成，勿手改表格；人工结论写在每节标题下的「备注」里。

上游文件 **314** 个，本地已有同名落点 **301** 个，**缺 13** 个；本地独有文件 **338** 个（需并入 Features 或删除）。

判定标准：上游相对路径在本地存在（`.xaml` ↔ `.axaml`）。「有文件」只说明路径对齐，内容是否已按上游实现，需要逐节核对。

## 总览

| 分组 | 上游文件 | 已有 | 缺失 |
|---|---:|---:|---:|
| Features/GameRecord | 47 | 47 | 0 |
| Features/Gacha | 40 | 40 | 0 |
| Features/GameLauncher | 26 | 26 | 0 |
| Features/Setting | 25 | 25 | 0 |
| Features/Screenshot | 23 | 19 | 4 |
| Features/ViewHost | 20 | 18 | 2 |
| Helpers | 16 | 14 | 2 |
| Features/PlayTime | 14 | 14 | 0 |
| Features/Background | 13 | 13 | 0 |
| Converters | 11 | 11 | 0 |
| Features/GameInstall | 9 | 9 | 0 |
| Features/Codec | 8 | 8 | 0 |
| Features/Update | 8 | 8 | 0 |
| Features/GameSetting | 7 | 7 | 0 |
| Features/GameSelector | 6 | 5 | 1 |
| Controls | 5 | 3 | 2 |
| Features/GameAccount | 5 | 5 | 0 |
| Features/Overlay | 4 | 2 | 2 |
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

## Features/Screenshot（19/23）

| 上游文件 | 本地落点 |
|---|---|
| `Features/Screenshot/ImageBatchConvertWindow.xaml` | ❌ 缺 |
| `Features/Screenshot/ImageBatchConvertWindow.xaml.cs` | ❌ 缺 |
| `Features/Screenshot/ImageViewWindow.xaml` | `Features/Screenshot/ImageViewWindow.axaml` |
| `Features/Screenshot/ImageViewWindow.xaml.cs` | `Features/Screenshot/ImageViewWindow.axaml.cs` |
| `Features/Screenshot/ImageViewWindow2.xaml` | `Features/Screenshot/ImageViewWindow2.axaml` |
| `Features/Screenshot/ImageViewWindow2.xaml.cs` | `Features/Screenshot/ImageViewWindow2.axaml.cs` |
| `Features/Screenshot/ScreenCaptureContext.cs` | `Features/Screenshot/ScreenCaptureContext.cs` |
| `Features/Screenshot/ScreenCaptureHelper.cs` | `Features/Screenshot/ScreenCaptureHelper.cs` |
| `Features/Screenshot/ScreenCaptureInfoWindow.xaml` | ❌ 缺 |
| `Features/Screenshot/ScreenCaptureInfoWindow.xaml.cs` | ❌ 缺 |
| `Features/Screenshot/ScreenCaptureItem.cs` | `Features/Screenshot/ScreenCaptureItem.cs` |
| `Features/Screenshot/ScreenCaptureService.cs` | `Features/Screenshot/ScreenCaptureService.cs` |
| `Features/Screenshot/ScreenshotFolder.cs` | `Features/Screenshot/ScreenshotFolder.cs` |
| `Features/Screenshot/ScreenshotFolderManageDialog.xaml` | `Features/Screenshot/ScreenshotFolderManageDialog.axaml` |
| `Features/Screenshot/ScreenshotFolderManageDialog.xaml.cs` | `Features/Screenshot/ScreenshotFolderManageDialog.axaml.cs` |
| `Features/Screenshot/ScreenshotHelper.cs` | `Features/Screenshot/ScreenshotHelper.cs` |
| `Features/Screenshot/ScreenshotItem.cs` | `Features/Screenshot/ScreenshotItem.cs` |
| `Features/Screenshot/ScreenshotPage.xaml` | `Features/Screenshot/ScreenshotPage.axaml` |
| `Features/Screenshot/ScreenshotPage.xaml.cs` | `Features/Screenshot/ScreenshotPage.axaml.cs` |
| `Features/Screenshot/ScreenshotPage2.xaml` | `Features/Screenshot/ScreenshotPage2.axaml` |
| `Features/Screenshot/ScreenshotPage2.xaml.cs` | `Features/Screenshot/ScreenshotPage2.axaml.cs` |
| `Features/Screenshot/ScrollViewerExtensions.cs` | `Features/Screenshot/ScrollViewerExtensions.cs` |
| `Features/Screenshot/ZoomFactorPercentConverter.cs` | `Features/Screenshot/ZoomFactorPercentConverter.cs` |

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

## Features/GameSelector（5/6）

| 上游文件 | 本地落点 |
|---|---|
| `Features/GameSelector/GameBizDisplay.cs` | `Features/GameSelector/GameBizDisplay.cs` |
| `Features/GameSelector/GameBizIcon.cs` | `Features/GameSelector/GameBizIcon.cs` |
| `Features/GameSelector/GameSelector.xaml` | `Features/GameSelector/GameSelector.axaml` |
| `Features/GameSelector/GameSelector.xaml.cs` | `Features/GameSelector/GameSelector.axaml.cs` |
| `Features/GameSelector/MainWindowDragRectAdaptToGameIconMessage.cs` | `Features/GameSelector/MainWindowDragRectAdaptToGameIconMessage.cs` |
| `Features/GameSelector/NoAnimationImageExStyle.xaml` | ❌ 缺 |

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

## Features/Overlay（2/4）

| 上游文件 | 本地落点 |
|---|---|
| `Features/Overlay/OverlayWindow.xaml` | ❌ 缺 |
| `Features/Overlay/OverlayWindow.xaml.cs` | ❌ 缺 |
| `Features/Overlay/RunningGame.cs` | `Features/Overlay/RunningGame.cs` |
| `Features/Overlay/RunningGameService.cs` | `Features/Overlay/RunningGameService.cs` |

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
- `AppServices.cs`
- `Controls/ColorRectChart.cs`
- `Controls/ColorfulTextBlock.axaml`
- `Controls/ColorfulTextBlock.axaml.cs`
- `Controls/GachaStatsCard.axaml`
- `Controls/GachaStatsCard.axaml.cs`
- `Controls/PlayTimeBarChart.cs`
- `Controls/PlayTimeCalendarHeatmap.cs`
- `Controls/ZZZGachaStatsCard.axaml`
- `Controls/ZZZGachaStatsCard.axaml.cs`
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
- `Features/GameInstall/PreDownloadButton.axaml`
- `Features/GameInstall/PreDownloadButton.axaml.cs`
- `Features/GameInstall/PreDownloadDialog.axaml`
- `Features/GameInstall/PreDownloadDialog.axaml.cs`
- `Features/GameLauncher/CompactCheckBoxStyle.axaml`
- `Features/GameLauncher/DX12IntroDialog.axaml`
- `Features/GameLauncher/DX12IntroDialog.axaml.cs`
- `Features/GameLauncher/GameAuthLoginButton.axaml`
- `Features/GameLauncher/GameAuthLoginButton.axaml.cs`
- `Features/GameLauncher/GameBannerAndPost.axaml`
- `Features/GameLauncher/GameBannerAndPost.axaml.cs`
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
- `Features/GameSetting/GameSettingPage.axaml`
- `Features/GameSetting/GameSettingPage.axaml.cs`
- `Features/GameSetting/GenshinHDRLuminanceSettingDialog.axaml`
- `Features/GameSetting/GenshinHDRLuminanceSettingDialog.axaml.cs`
- `Features/GamepadControl/GamepadTypes.cs`
- `Features/GamepadControl/SdlGameController.cs`
- `Features/GamepadControl/SimulateInputTipsWindow.axaml`
- `Features/GamepadControl/SimulateInputTipsWindow.axaml.cs`
- `Features/GamepadControl/X11Input.cs`
- `Features/PlayTime/PlayTimeButton.axaml`
- `Features/PlayTime/PlayTimeButton.axaml.cs`
- `Features/PlayTime/PlayTimeStatsDialog.axaml`
- `Features/PlayTime/PlayTimeStatsDialog.axaml.cs`
- `Features/Screenshot/ImageViewWindow.axaml`
- `Features/Screenshot/ImageViewWindow.axaml.cs`
- `Features/Screenshot/ImageViewWindow2.axaml`
- `Features/Screenshot/ImageViewWindow2.axaml.cs`
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
- `Features/Setting/SettingPage.axaml`
- `Features/Setting/SettingPage.axaml.cs`
- `Features/Setting/SettingPageBase.cs`
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
- `Frameworks/FontIcon.cs`
- `Frameworks/Frame.cs`
- `Frameworks/NavigationView.cs`
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
- `Views/Starward/Pages/ToolboxPage.axaml`
- `Views/Starward/Pages/ToolboxPage.axaml.cs`
- `Views/Starward/PlayTimeStatsDialog.axaml`
- `Views/Starward/PlayTimeStatsDialog.axaml.cs`
- `Views/Starward/PreDownloadDialog.axaml`
- `Views/Starward/PreDownloadDialog.axaml.cs`
- `Views/Starward/ScreenshotFolderManageDialog.axaml`
- `Views/Starward/ScreenshotFolderManageDialog.axaml.cs`
- `Views/Starward/StarwardPage.cs`
- `Views/Starward/StarwardShell.axaml`
- `Views/Starward/StarwardShell.axaml.cs`
- `Views/Starward/UiTypography.cs`
- `Views/Starward/Uigf4GachaWindow.axaml`
- `Views/Starward/Uigf4GachaWindow.axaml.cs`

