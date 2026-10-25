# 07 — UI 复刻 Starward（排版 / 样式 / 交互）

> 本文是 UI 层的唯一契约。任何"重新设计"的想法都先在这里改，再动 XAML。

## 0. 目标与边界

**目标**：LunarWard 的界面是 Starward 的复刻，不是"受 Starward 启发的另设计"。

- 窗口结构、元素位置、尺寸、圆角、颜色、字体层级、交互路径 **与 Starward 一致**。
- LunarWard 独有能力（wine 运行时、十层 env、外部程序、诊断、环境 shell、区服切
  换）**追加为 Starward 已有页面的新分区/新对话框**，不新造一套导航。
- 不复刻 Starward 的 Windows 专属实现（Mica、Acrylic 真模糊、AnimatedIcon、
  WebView2、云游戏）；用 Avalonia 的等价物或 Starward 自己的 Fallback 颜色。

**基准**：

| 来源 | 位置 | 用途 |
|---|---|---|
| Starward 源码 | `/home/qie2035/projects/csharp/Starward`（HEAD 2026-10-12） | 排版/尺寸/颜色的唯一真值 |
| 窗口常量 | `Features/ViewHost/MainWindow.xaml.cs`（1200×676、48 标题栏、不可缩放） | 外壳 |
| 主视图 | `Features/ViewHost/MainView.xaml` | 导航栏 + 背景 + Frame + 游戏选择器 |
| 启动页 | `Features/GameLauncher/GameLauncherPage.xaml` + `GameBannerAndPost` / `StartGameButton` / `PlayTimeButton` | 启动器 |
| 资源 | `App.xaml`（Accent `#CBAD8E`、`CustomAcrylicBrush #F52C2C2C`、Segoe Fluent Icons） | 视觉 token |
| 参考截图 | `/tmp/starward-shot-conv.png`（0.14 时代，1200×676 同比例）；0.18 源码 | 视觉比对 |

---

## 1. 窗口外壳

```
1200 × 676 固定，不可缩放/最大化（Starward 的行为）
┌────────────────────────────────────────────────────────────────┐
│ 48px 标题栏：拖拽区（整幅宽度）           ─  □  ✕（右上，46×32）│
├──────┬─────────────────────────────────────────────────────────┤
│ 56px │ 内容区（Frame 页面，透明，露出背景图）                   │
│ 导航 │                                                         │
│ 栏   │                                                         │
│      │                                                         │
└──────┴─────────────────────────────────────────────────────────┘
```

- 标题栏高度 48（Starward `TitleBarHeightOption.Tall`），内容从 y=0 开始绘制，
  被标题栏覆盖（背景图铺满整窗，含标题栏）。
- 窗口圆角 8（WinUI 11 风格）+ 1px `#1AFFFFFF` 描边；X11/Wayland 不支持透明时
  退化为直角（不报错）。
- 导航栏：56px 宽、`#F52C2C2C`（Starward CustomAcrylic 的 Fallback 色）填充，
  全高。顶部 52px 放当前游戏图标（40×40，8,10）；底部放设置按钮。
- 导航项：40×40、外边距 8,2、圆角 4、图标 16 且**在 40 宽内水平/垂直居中**
  （`HorizontalContentAlignment=Stretch` + 图标 `VerticalAlignment=Center`）；
  选中态 = `#14FFFFFF` 背景 + 左侧 3×16 的 `#CBAD8E` 圆角竖条；悬停 `#0AFFFFFF`；
  悬停 500ms 后在右侧弹出文字 Flyout。
- 图标：**只用 `Segoe MDL2 Assets` 码位**（`Segoe Fluent Icons` 在 Linux 不可分发；
  上游 Segoe Fluent 的 4 个新增码位在 MDL2 里缺失，用到时按 MDL2 近似字形取）。字号 16 / 20。

## 2. 启动器页（复刻重点）

像素布局（相对内容区左上角，内容区原点 = (56,0)，但背景全屏）：

| 元素 | 位置 / 尺寸 |
|---|---|
| 当前游戏图标 | 内容区左上 (8,10)，40×40，圆角 8；未固定图标条时右下角叠 20×20 区服角标；指针进入滑出图标条，点击打开全屏选择层 |
| 游戏图标条 | 顶部 60 高、Margin 56,0,0,0、Padding 6,0,12,0；40×40 图标横排、Spacing 0，每项右下角 20×20 区服角标 + `#60000000` 遮罩（当前/悬停 0）；收起时 `Translation -100`，只在 60px 高度内命中 |
| 游戏选择层 | 全屏 `#80000000`、Padding 68,80,0,0；**每款游戏一张卡**（192×108、`#40000000`、圆角 8、UniformToFill 缩略图），区服收在卡片内；点卡片才弹区服 Flyout（Bottom/Transient），单区服直接切换；悬停显示「选择游戏服务器」；点空白 / ESC / 右上关闭 |
| 已安装游戏 | 卡片下方 Expander（MinWidth 820、`#0DFFFFFF` 圆角 8）：占用大小 + 硬链接节省 + 自动查找 + 刷新，展开为每区服一行（图标/游戏名/区服角标/区服名/容量/路径） |
| 公告卡 | 左 60、底 40，380×304，圆角 8，`CustomAcrylic`；上半 Banner 176 高，下半 Pivot 124 高 |
| 公告卡 Banner | `UniformToFill`，圆角 8；悬停右下角显示 `1/3` 计数；**滚轮上一页/下一页**（上游 FlipView 行为）；点击打开链接 |
| 公告卡 Pivot | 表头 `活动 / 公告 / 资讯`，16px Bold，header 高 36，左右 margin 12，选中下划线 3px 圆角；右侧公告铃铛按钮（E789）带红点 |
| 公告列表 | 行高约 27，标题（14，`HyperlinkButton` 可点）左、日期（14，次要色）右；行距 8；超出滚动 |
| 时长胶囊 | 右下，底 40，高 44，圆角 22，`CustomOverlayAcrylic`；时钟图标 20 + `635h 16m`；点击弹出时长统计 |
| 时长统计弹窗 | `PlayTimeStatsDialog.xml` 1008 宽亚克力卡：6 张统计卡（总时长/每日平均/最长连续/最长单次/单日最长/上次启动，内边距 12,10，标题 12、数值 18、次要行 11），`最近 15 天 / 12 周 / 12 个月` 分段 + 右对齐区间总计时长，柱状图（高 132，纵轴 3 等分刻度、圆角柱、悬停提示），近一年日历热力图（52 周、周一起始、左侧周一/周日、底部月份） |
| 开始游戏 | 右下，底 40，最小宽 180、高 44，圆角 22，`CustomAcrylic`；文本居中，右侧 44×44 设置齿轮（E713） |
| 开始游戏状态 | 空闲「开始游戏」；运行中「游戏正在运行」+ 已运行时长；忙时左侧 ProgressRing 20 |
| 便笺按钮 | 右上角 40×40、圆角 8、`CustomOverlayAcrylic`；点击打开 `DailyNoteButton` 的 Flyout（刷新/角色行/原神或绝区零分区），不是跳页 |
| 账号切换 | 右下，开始按钮上方 100px，右侧 40px；头像 40×40 圆角 4 + 悬停列表 |
| 背景切换条 | 背景数 > 1 且悬停底部 60px 区时出现：可点圆点 + 图库/复制/另存为/播放按钮；滚轮切上一张/下一张 |
| 公告/Update Toast | 底部居中，宽 ≤600，距底 72 |

背景来源（`BackgroundCatalog`）：官方接口 `getAllGameBasicInfo` 的 `backgrounds`（图片 + `type=BACKGROUND_TYPE_VIDEO` 的官方动态背景，`ffmpeg` 直接流式解码 https）→ 游戏素材版本海报 → `~/.cache/lunarward/background` 下的本地图片/视频；切换条只在可选项 > 1 时出现。

## 3. 视觉 token

| Token | 值 | 说明 |
|---|---|---|
| `StarAccent` | `#CBAD8E` | Starward 自定义强调色（暖沙） |
| `StarAccentForeground` | `#1A1A1A` | 强调色上的文字 |
| `TextFillColorPrimary` | `#FFFFFF` | |
| `TextFillColorSecondary` | `#C5FFFFFF` | |
| `TextFillColorTertiary` | `#87FFFFFF` | |
| `ControlOnImageFill` | `#0FFFFFFF` | 图片上的卡片底色 |
| `CustomAcrylic` | `#F52C2C2C` | Fallback；无模糊时用 `#E62C2C2C` |
| `CustomOverlayAcrylic` | `#B32C2C2C` | 胶囊/浮层 |
| `CardStroke` | `#14FFFFFF` | 1px 描边 |
| `SystemFillColorCaution` | `#FFB900` | 警告 |
| `SystemFillColorSuccess` | `#6CCB5F` | 成功 |
| 字体 | `Inter`（`Avalonia.Fonts.Inter`，OFL）→ `Noto Sans CJK SC` → `Microsoft YaHei UI` → `Noto Sans JP` → `Segoe UI` | 正文 14；次要 12–13；标题 20 Bold；Pivot 16 Bold。Linux 上不再依赖 wps 的 Segoe UI |
| 图标字体 | `Segoe MDL2 Assets`（`Program.cs` FontManagerOptions 兜底链末位） | 全仓字形只允许落在 MDL2 码位；缺字形时 Avalonia 会回退到系统字体，需在评审时目检 |
| 圆角 | 4 / 8 / 22 | 控件 / 卡片 / 胶囊 |

**原则**：星式控件用显式 `ControlTheme` 或局部样式实现，不改 Fluent 全局主题；
所有颜色进 `Styles/Starward.axaml` 的资源字典，页面不得写死颜色（临时占位除外）。

## 4. 交互规则（与 Starward 对齐）

1. **导航**：点击左栏项切换 `Frame` 页面，选中项左侧强调条；再次点击已选中项不动作。
   设置按钮固定在栏底，打开设置页（设置页自身左侧 260px 二级导航，见 Starward
   `SettingPage`）。
2. **游戏选择**：点击栏顶游戏图标 → 全屏层淡入（167ms）→ **点击卡片**选区服
   （单区服卡片直接切换；多区服弹 Bottom/Transient Flyout，条目为 24×24 区服角标 +
   名称 + 固定按钮 E718/E841）→ 切 profile + 背景 + 公告 + 页面数据全部重载；
   **悬停只做高亮，不自动弹 Flyout**（旧实现的闪烁即由此而来）；ESC 或点空白关闭。
3. **开始按钮**：文案/可用性由启动相位决定（沿用 `LaunchButtonState`）；悬停 300ms
   弹出游戏信息气泡（运行中显示 pid/时长）；忙碌时左侧转圈；运行中点击 = 停止。
4. **公告卡**：Banner 5s 自动轮播（Starward `GameBannerAndPost.xaml.cs` 的
   `DispatcherQueueTimer`），悬停暂停并显示 `1/N` 计数，移出恢复；Pivot 页签点击
   切换列表；公告铃铛打开公告窗口。（本行旧文写"无 timer"是错的，见 08 §2。）
5. **便笺**：启动器右上按钮打开 Flyout（Starward `DailyNoteButton`）：刷新、角色行、
   错误行，然后按游戏显示树脂/电量等分区；没有 cookie 时给"去工具箱保存"入口。
6. **背景**：默认使用当前游戏的官方背景图（HoYoPlay `getGames` 的
   `background`/`backgrounds`），本地自定义背景优先；悬停底部出现切换条；视频背景
   用 `ffmpeg` 子进程解码到 `WriteableBitmap`（已实现），启动游戏时暂停（SIGSTOP）、退出恢复。
7. **窗口关闭**：与上游一致，通用设置里三选一「每次关闭时询问 / 最小化到系统托盘 /
   完全退出」（`LauncherConfig.CloseWindowOption` = `Ask` / `Hide` / `Exit`，上游
   `MainWindowCloseOption` 的 0/1/2 同义）。选 Ask 时点关闭先弹
   `MainWindowCloseDialog`（标题 + 说明 + 两个 RadioButton + 确认/取消），确认后把选择
   写回配置，下次不再问；上游 `Features/ViewHost/MainWindow.xaml.cs:107-136` 同流程。
   托盘用 Avalonia `TrayIcon`（Linux 上走 StatusNotifier，KDE 默认支持、GNOME 需
   AppIndicator 扩展）；宿主不可用时「最小化到系统托盘」单选禁用并提示原因，不会静默
   隐藏窗口。判定逻辑纯函数化：`Services/CloseWindowDecision.cs`（单测覆盖）。
8. **动效**：页面切换 167ms 淡入；选择层 167ms 淡入；悬停背景 83ms；不做夸张动画。

## 5. 页面映射（LunarWard 功能 → Starward 页面）

| Starward 导航 | LunarWard 页 | 内容（✅ 已有能力） |
|---|---|---|
| 启动器 | `GameLauncherPage` | 公告三源 ✅、开始/停止 ✅、时长 ✅、背景 ✅、便笺 ✅、账号切换 ✅、URL 协议 ✅ |
| 游戏设置 | `GameSettingPage` | **追加**：wine 运行时选择 ✅、十层 env 表 ✅、外部程序（Auto/Manual/三态 env/PreWrites）✅、启动参数、区服（config.ini）✅、诊断 ✅、环境 shell ✅ |
| 游戏截图 | `ScreenshotPage` | `ScreenshotPage2` 磁贴墙（204×115、间距 12）+ 点击预览；`ImageViewWindow2` 已移植（48px 顶栏/缩放拖动/图库条/信息面板） |
| 抽卡记录 | `GachaPage` | `GachaLogPage` 44px 页头 + SplitView + `GachaStatsCard`；拉取/导出/UIGF 追加 |
| 工具箱 | `GameRecordPage`（容器已 1:1，深境螺旋/幻想真境剧诗/幽境危战/绳网月报/危局强袭战/式舆防卫战/旅行者札记已移植；战绩=米游社网页，用系统浏览器打开） | 248px 左栏 + 战绩菜单 + 子页挂载；登录用 cookie 拉角色（见 08 §1） |
| 账号记录 | `SelfQueryPage`（待恢复独立页） | Starward 主视图有独立导航项，不并入工具箱 |
| 设置 | `SettingPage` | 关于、通用（语言/主题/关闭行为）、下载、文件管理、截图、快捷键、高级（env 默认值/包装器/日志/更新通道）、工具箱 |
| 安装/更新 | `InstallGameDialog` | 移植 `LunarWard.Installer`，M8 收尾 |

**追加原则**：LunarWard 的专属设置放在「游戏设置」页底部新区块，标题格式与
Starward 一致（20px）+ 说明（14px，`TextFillColorSecondary`）；不新增顶层导航项。

## 6. 实现契约（Avalonia）

```
src/LunarWard.App/
├── Styles/Starward.axaml              # token + 控件主题（Lead 独占）
├── Controls/
│   ├── StarwardIcon.cs                # 可选，图标常量
│   ├── NavigationRail.axaml           # 56px 导航栏（Lead）
│   ├── GameSelectorOverlay.axaml      # 全屏游戏选择层（Lead）
│   └── ToastHost.axaml                # InAppToast 等价物（Lead）
├── Services/DialogService.cs          # 打开 ContentDialog：防重入 / ESC / 焦点
├── Views/Starward/
│   ├── StarwardShell.axaml            # MainView 等价物（Lead）
│   ├── MainWindow.axaml               # 1200×676 外壳 + 标题栏（Lead）
│   ├── Controls/StarwardContentDialog.axaml(.cs)  # ContentDialog 容器（共用）
│   └── Pages/
│       ├── GameLauncherPage.axaml     # Lead
│       ├── GameSettingPage.axaml      # T1
│       ├── SettingPage.axaml          # T2（含二级子页）
│       ├── GachaPage.axaml            # T3
│       ├── ScreenshotPage.axaml       # T4
│       └── ToolboxPage.axaml          # T4（便笺 + 账号）
└── ViewModels/Starward/
    ├── ShellViewModel.cs              # Lead
    ├── PageContext.cs                 # Lead（契约）
    └── …PageViewModel.cs              # 各页负责人
```

**契约**：

```csharp
public enum ShellPage { Launcher, GameSetting, Screenshot, Gacha, Toolbox, Settings }

public sealed class PageContext
{
    public required AppServices Services { get; init; }
    public required ShellViewModel Shell { get; init; }
    public required ProfileItemViewModel Profile { get; init; }
    public required LogService Log { get; init; }
}

// 每个页面：UserControl，公开 ctor(PageContext)，自己设置 DataContext。
public partial class GachaPage : UserControl
{
    public GachaPage(PageContext context) { InitializeComponent(); DataContext = new GachaPageViewModel(context); }
}
```

- 页面切换由 `ShellViewModel.Navigate(ShellPage)` 统一做；页面内跳转调用
  `context.Shell.Navigate(...)`。
- 弹窗走 `await DialogService.ShowAsync(dialog, owner)`（`Services/DialogService.cs`）：
  在 `owner` 的 `dh:DialogHost` 里打开，防重入、ESC 关闭、焦点归还都由它负责；不再
  `new Window()` 造独立对话框窗口。Toast 走 `context.Shell.Toast(...)`。
- 页面不得引用旧窗口（`Views/SettingsWindow` 等）；旧窗口在对应页面验收通过后删除。
- 页面内样式优先局部 `<UserControl.Styles>`，颜色引用 `Starward.axaml` 的资源键。

### 6.1 ContentDialog 容器（`Views/Starward/Controls/StarwardContentDialog.axaml`）

WinUI 的 `ContentDialog` 是**窗口内浮层**，不是独立窗口；Avalonia 没有内建等价物，
由 `DialogHost.Avalonia` 的 `dh:DialogHost`（`MainWindow.axaml` 最外层，`OverlayBackground`
= `#66000000` 对应 `ContentDialogSmokeFill`）承载卡片，卡片本体是本项目自己实现的容器：

| 部位 | 规格（对齐 WinUI 默认模板 + 上游各 dialog 的显式覆盖） |
|---|---|
| 圆角 / 边框 | `CornerRadius=8`，1px `StarCardStrokeBrush`，`BoxShadow="0 16 48 0 #99000000"` |
| 背景 | `StarAcrylicBrush`（`#F52C2C2C`，上游 `CustomAcrylicBrush`） |
| 标题行 | 48px，`FontSize=20`、`SemiBold`，`Margin="24,0,56,0"`；`Title` 为空时整行收起 |
| 关闭按钮 | 右上 40×40、圆角 4、`Segoe MDL2 E711`，hover 底色 `StarControlFillHoverBrush` |
| 内容区 | `Content` + `ContentPadding`（默认 0，各 dialog 自己 `Padding=24`） |
| 按钮栏 | 右对齐、`Spacing=8`、主按钮 `Classes=accent`、次按钮与「关闭」用默认样式 |
| 尺寸 | `MinWidth=320` / `MaxWidth=548` 默认；宽弹窗自己覆盖（settings 800、install 560、playtime 1008…） |
| 关闭方式 | ESC、右上 X、底部「关闭」三路都走 `CloseRequested` → `DialogService` 关会话 |

用法（所有弹窗都是「继承容器 + 只写内容」）：

```xml
<controls:StarwardContentDialog xmlns:controls="using:LunarWard.App.Views.Starward.Controls"
                                x:Class="LunarWard.App.Views.Starward.PreDownloadDialog"
                                MaxWidth="400">
  <Border Width="400" Padding="24">…</Border>
</controls:StarwardContentDialog>
```

```csharp
var dialog = new PreDownloadDialog(services, gameBiz, launcherId, installPath); // : StarwardContentDialog
dialog.CloseButtonText = AppStrings.Instance["Common.Close"];                   // 需要时设按钮文案
dialog.PrimaryButtonText = AppStrings.Instance["PreDownloadDialog.Start"];
dialog.IsPrimaryButtonEnabled = false;                                          // WinUI IsPrimaryButtonEnabled
dialog.PrimaryButtonClicked += (_, _) => DoWork();
_ = DialogService.ShowAsync(dialog, this);                                      // this = 页面/窗口（用来找宿主）
// 关闭后读结果：dialog.Request / dialog.Result / dialog.Confirmed
```

- 需要「确认/取消」语义的（如 `MainWindowCloseDialog`）：`PrimaryButtonText` +
  `CloseButtonText` 即可，`CloseButtonClicked` / `CloseRequested` 都表示取消路径。
- 自检截图（`--launcher-setting-dialog` 等）：打开浮层后 `CaptureMode.Capture(owner)`
  渲染主窗口，浮层与暗化一起进 PNG（不再有 `.dialog.png` 的独立窗口截图）。
- 关闭询问浮层的键盘自检：`--screenshot /tmp/esc.png --close-window-dialog --dialog-esc-test`
  会在打开后合成一次 ESC，打印 `DIALOG_ESC_CLOSED True`（表示 ESC → 会话关闭 → 浮层收起）。

## 7. 验收

1. `dotnet build LunarWard.slnx` 0 warning / 0 error。
2. `dotnet run --project src/LunarWard.App -- --screenshot artifacts/screens/sw-<page>.png --page <name> --profile genshin-cn`
   逐页出图；`--screenshot` 支持 `--page launcher|game-setting|screenshot|gacha|toolbox|settings`，
   设置页可用 `--settings-section <about|general|download|files|screenshot|hotkey|gamepad|advanced|toolbox>`
   直接落到二级子页。
3. 每页与 Starward 参考截图并排比对：窗口结构、元素位置/尺寸、字体层级、圆角、
   颜色、悬停/选中态逐项打勾；差异记入本文 §8。
4. 旧顶栏 UI 删除后，`--screenshot` 默认落到启动器页，README 截图更新。
5. 现有测试保持全绿（当前 348：290 单测 + 31 安装器 + 27 集成）。

## 8. 已知偏差（如实记录）

| 项 | Starward | LunarWard | 原因 |
|---|---|---|---|
| Acrylic 真模糊 | WinUI AcrylicBrush | 半透明纯色（Starward Fallback 色） | Avalonia/Linux 无跨桌面模糊 API |
| 圆角窗口 | WinUI 11 默认 | 尽力（透明+圆角 Border），不支持时直角 | 合成器差异 |
| 标题栏图标/系统菜单 | WinUI 图标+系统菜单 | 不显示图标 | 无等价 API |
| 动画 | WinUI 组合动画 | Avalonia Transition 近似 | 实现差异 |
| 游戏选择器 | 单图标 + 全屏层（HEAD） | 同构，但服务器来源是本地 profile | 数据模型不同 |
| 弹窗关闭按钮 | 上游只画右上 X（`E711`），无底部按钮栏 | X + 底部「关闭」并列 | 用户反馈「没有关闭方式」；X 保留，底部按钮给键盘/触屏可达性 |
| 弹窗暗化 | `ContentDialogSmokeFill` 半透明黑 | `DialogHost.OverlayBackground=#66000000` | Avalonia 用 popup 遮罩实现同语义 |
| 宽弹窗（时长统计 1008） | 独立 640 高窗口 | 窗口内浮层 548 高，内容区可用滚轮 | 主窗口固定 676 高，浮层不能超出；上游同尺寸无法 1:1 塞进来 |
