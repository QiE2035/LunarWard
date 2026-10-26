# 17 — 平台层与等价物清单（上游没有的 118 个文件）

> 生成方式：`python3 scripts/check-alignment.py` 只做「上游文件是否都在」的正向检查；本文件是反向账面——
> 逐层列出**本仓库有、上游没有**的文件，说明每一类为什么必须有、对应上游的哪套 API、以及差异登记在 `docs/09` 的哪一节。
>
> 判定原则（docs/10 §2.2）：只有「上游用 Windows 专属 API、Linux 无同名能力」才允许出现平台等价物；
> 每次替换必须集中在一处、用 `// 平台替换：` 注释、并登记到 docs/09。**能直接搬的上游文件一律不许改写。**

## 1. 平台层（Linux 必须的新实现，不是选择）

| 目录 | 文件数 | 上游对应 | 为什么必须有 | 差异登记 |
|---|---:|---|---|---|
| `src/LunarWard/Launch/` | 13 | 上游 `Features/GameLauncher` 里直接用 `Process.Start` + 注册表读写 | Wine/Proton 启动需要构造命令行、前缀环境、日志泵、外部程序启动、URL 协议转发 | docs/09 §三、§十三 |
| `src/LunarWard/Platform/` | 8 | 上游用 Win32（ShellExecute、Registry、`Process`） | XDG 路径、`.desktop` 自启动、进程树、Libc 调用、Linux 系统信息 | docs/09 §五 |
| `src/LunarWard/Wine/` | 12 | 无（Windows 原生运行） | Wine 前缀、组件（DXVK 等）安装、运行时解析、注册表读写 | docs/09 §四（本轮 Wine/DXVK 功能不开发，代码保留） |
| `src/LunarWard/Frameworks/WebView/` | 11 | `Microsoft.Web.WebView2` | WebKitGTK 4.1 + GTK3 XID 嵌入，含 WebView2 形状门面与两个自检 | docs/09 §十六 |
| `src/LunarWard/Frameworks/Dialogs/` | 2 | `ContentDialog` | Avalonia 无内置 ContentDialog，用 DialogHost 重实现同名 API | docs/09 §二十一 |
| `src/LunarWard/Frameworks/{FontIcon,Frame,NavigationView,Pivot}.cs` | 4 | WinUI 同名控件 | Avalonia 无 `NavigationView`/`Pivot`/`Frame`/`FontIcon`，按上游用法最小实现 | docs/09 §二十一 |
| `src/LunarWard/Frameworks/{ShellHelper,SingleInstance,StartupUi}.cs` | 3 | `ShellExecute` / `AppInstance` / 首启流程 | 打开链接、单实例、欢迎页与权限页的宿主钩子 | docs/09 §二十五 |
| `src/LunarWard/Services/` | 15 | 上游散在 `Helpers`/`Features` 的 Win32 版本 | 对话框服务、后台图库、视频背景、游戏扫描/卸载、Wine 注册表、抓包自检等平台能力 | docs/09 §二、§八、§十七 |
| `src/LunarWard/Helpers/ToastItem.cs` | 1 | 无（上游用 `InfoBar`） | Avalonia 轻提示的数据模型（`InAppToast` 是上游 `InfoBar` 等价物） | docs/09 §二 |
| `src/LunarWard/AppConfig.Setting.LinuxExtensions.cs` | 1 | 无 | 本地扩展设置（视频背景、公告开关等），不写进上游 Setting 表的结构 | docs/09 §十 |
| `src/LunarWard/SupportedPlatform.cs` | 1 | 无 | 平台能力探测的单一出口 | docs/09 §五 |

## 2. 渲染/媒体等价实现（Win2D、Codec、输入）

| 目录 | 文件 | 上游对应 | 做法 |
|---|---|---|---|
| `Controls/ColorRectChart.cs` | 1 | `ColorRectChart.xaml(.cs)`（Win2D `CanvasControl`） | Avalonia 自绘，保留上游数据与配色语义 |
| `Controls/PlayTimeBarChart.cs`、`PlayTimeCalendarHeatmap.cs` | 2 | 上游 `Features/PlayTime` 里的 Win2D 图表 | Avalonia 自绘，保留 hover/HoverCard/日历聚合语义 |
| `Features/Codec/PngCodec.cs`、`ColorPrimaries.cs` | 2 | 上游 `Starward.Codec`（C++/WinRT） | 托管 PNG 编解码 + HDR 元数据写入 |
| `Features/GamepadControl/{SdlGameController,X11Input,GamepadTypes}.cs` | 3 | 上游 `H.InputSimulator` / `Windows.Gaming.Input` | SDL3/X11 XTest 输入，保留上游 `GamepadController` 对外形状 |
| `Features/Screenshot/ScreenCaptureHelper.cs` 内的后端分支 | — | `Windows.Graphics.Capture` | `grim` → `spectacle` → `gnome-screenshot` → `import` 回退链，差异登记 docs/09 §二十六 |

## 3. 上游分离工程的进程内化

| 目录 | 文件数 | 上游 | 说明 |
|---|---:|---|---|
| `Features/GameInstall/Installer/` | 25 | `src/Starward.Setup`（Aprillz.MewUI 安装器）+ `Starward.Setup.Core` | 安装/预下载/差分/硬链接在进程内运行，`LunarWard.Setup.Core` 保留为独立工程（依赖与 OneOf 语义与上游相同）；docs/10 §8.2 |

## 4. 本地扩展（上游没有的功能，需逐条登记，不得静默增加）

| 文件 | 说明 | 是否与移植冲突 |
|---|---|---|
| `Features/GameLauncher/{BilibiliPluginDownloader,ChannelModel,ChannelValidator,GameCatalog,GameConfigIniFile,GamePrefixContext}.cs` | 国服渠道/插件与 `config.ini` 注册表语义 | 否（上游没有国服插件链路） |
| `Features/GameRecord/{GameRecordAssets,GameRecordConverters,ObservableSupport}.cs` | 战绩资源与转换器适配 | 否（等价物） |
| `Features/Gacha/GachaProgressToast.cs` | 抽卡进度提示 | 否（上游用 `InfoBar`） |
| `Features/Setting/{SettingLocalization,SettingLanguageOption,SettingShellActions}.cs` | 设置页的本地化与外壳动作 | 否（`SettingLanguageOption` 为语言下拉模型） |
| `Localization/AppStrings.cs` + 两个 resx | **待删除**：上游唯一字符串来源是 `LunarWard.Language` | 是（task-22 处理中） |

## 5. 验收口径

1. `python3 scripts/check-alignment.py` 的 **文件映射** 必须 `missing: []`（等价项单独计数）；**命名空间泄漏** 必须 0。
2. 本清单里的文件数发生增减时，必须同时改本文件与 `docs/09` 对应节；`scripts/check-alignment.py` 的 `EQUIVALENTS` 表只登记"上游文件 → 本地等价文件"的映射，不做反向统计。
3. 第 4 节「本地扩展」只允许减少（随功能对齐被上游实现取代），不允许悄悄增加。
