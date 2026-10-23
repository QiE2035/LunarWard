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
- 导航项：40×40，圆角 4，图标 16；选中态 = `#14FFFFFF` 背景 + 左侧 3×16 的
  `#CBAD8E` 圆角竖条；悬停 `#0AFFFFFF`；悬停 500ms 后在右侧弹出文字 Flyout。
- 图标：Segoe Fluent Icons（本机由 `Segoe MDL2 Assets` 兜底），字号 16 / 20。

## 2. 启动器页（复刻重点）

像素布局（相对内容区左上角，内容区原点 = (56,0)，但背景全屏）：

| 元素 | 位置 / 尺寸 |
|---|---|
| 当前游戏图标 | 导航栏内 (8,10)，40×40，圆角 8；点击打开全屏选择层 |
| 游戏选择层 | 全屏 `#80000000`；卡片 192×108（背景缩略图 + Logo），悬停显示服务器/区服按钮；ESC / 点空白关闭 |
| 公告卡 | 左 60、底 40，380×304，圆角 8，`CustomAcrylic`；上半 Banner 176 高，下半 Pivot 124 高 |
| 公告卡 Banner | `UniformToFill`，圆角 8；悬停右下角显示 `1/3` 计数；点击打开链接 |
| 公告卡 Pivot | 表头 `活动 / 公告 / 资讯`，16px Bold，header 高 36，左右 margin 12，选中下划线 3px 圆角；右侧公告铃铛按钮（E789）带红点 |
| 公告列表 | 行高约 27，标题（14）左、日期（14，次要色）右；行距 8；超出滚动 |
| 时长胶囊 | 右下，底 40，高 44，圆角 22，`CustomOverlayAcrylic`；时钟图标 20 + `635h 16m` |
| 开始游戏 | 右下，底 40，最小宽 180、高 44，圆角 22，`CustomAcrylic`；文本居中，右侧 44×44 设置齿轮（E713） |
| 开始游戏状态 | 空闲「开始游戏」；运行中「游戏正在运行」+ 已运行时长；忙时左侧 ProgressRing 20 |
| 便笺按钮 | 右上角 40×40、圆角 8、`CustomOverlayAcrylic`；点击打开 `DailyNoteButton` 的 Flyout（刷新/角色行/原神或绝区零分区），不是跳页 |
| 账号切换 | 右下，开始按钮上方 100px，右侧 40px；头像 40×40 圆角 4 + 悬停列表 |
| 背景切换条 | 悬停底部 60px 高渐变区时出现：PipsPager 圆点 + 图库/复制/另存为/播放按钮 |
| 公告/Update Toast | 底部居中，宽 ≤600，距底 72 |

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
| 字体 | `Segoe UI`（本机 wps 包） | 正文 14；次要 12–13；标题 20 Bold；Pivot 16 Bold |
| 图标字体 | `Segoe Fluent Icons, Segoe MDL2 Assets` | 缺失时退到 PathIcon 场景不使用 |
| 圆角 | 4 / 8 / 22 | 控件 / 卡片 / 胶囊 |

**原则**：星式控件用显式 `ControlTheme` 或局部样式实现，不改 Fluent 全局主题；
所有颜色进 `Styles/Starward.axaml` 的资源字典，页面不得写死颜色（临时占位除外）。

## 4. 交互规则（与 Starward 对齐）

1. **导航**：点击左栏项切换 `Frame` 页面，选中项左侧强调条；再次点击已选中项不动作。
   设置按钮固定在栏底，打开设置页（设置页自身左侧 260px 二级导航，见 Starward
   `SettingPage`）。
2. **游戏选择**：点击栏顶游戏图标 → 全屏层淡入（167ms）→ 选卡片 → 选服务器 →
   切 profile + 背景 + 公告 + 页面数据全部重载；ESC 或点空白关闭。
3. **开始按钮**：文案/可用性由启动相位决定（沿用 `LaunchButtonState`）；悬停 300ms
   弹出游戏信息气泡（运行中显示 pid/时长）；忙碌时左侧转圈；运行中点击 = 停止。
4. **公告卡**：Banner 5s 自动轮播（Starward `GameBannerAndPost.xaml.cs` 的
   `DispatcherQueueTimer`），悬停暂停并显示 `1/N` 计数，移出恢复；Pivot 页签点击
   切换列表；公告铃铛打开公告窗口。（本行旧文写"无 timer"是错的，见 08 §2。）
5. **便笺**：启动器右上按钮打开 Flyout（Starward `DailyNoteButton`）：刷新、角色行、
   错误行，然后按游戏显示树脂/电量等分区；没有 cookie 时给"去工具箱保存"入口。
6. **背景**：默认使用当前游戏的官方背景图（HoYoPlay `getGames` 的
   `background`/`backgrounds`），本地自定义背景优先；悬停底部出现切换条；视频背景
   用 LibVLCSharp（已实现），启动游戏时暂停、退出恢复。
7. **窗口关闭**：Starward 可配置"隐藏到托盘/退出"；LunarWard 首期直接退出（不伪造
   托盘），但关闭按钮行为集中在 Shell 一处，后续可追加。
8. **动效**：页面切换 167ms 淡入；选择层 167ms 淡入；悬停背景 83ms；不做夸张动画。

## 5. 页面映射（LunarWard 功能 → Starward 页面）

| Starward 导航 | LunarWard 页 | 内容（✅ 已有能力） |
|---|---|---|
| 启动器 | `GameLauncherPage` | 公告三源 ✅、开始/停止 ✅、时长 ✅、背景 ✅、便笺 ✅、账号切换 ✅、URL 协议 ✅ |
| 游戏设置 | `GameSettingPage` | **追加**：wine 运行时选择 ✅、十层 env 表 ✅、外部程序（Auto/Manual/三态 env/PreWrites）✅、启动参数、区服（config.ini）✅、诊断 ✅、环境 shell ✅ |
| 游戏截图 | `ScreenshotPage` | `ScreenshotPage2` 磁贴墙（204×115、间距 12）+ 点击预览；`ImageViewWindow2` 待移植 |
| 抽卡记录 | `GachaPage` | `GachaLogPage` 44px 页头 + SplitView + `GachaStatsCard`；拉取/导出/UIGF 追加 |
| 工具箱 | `GameRecordPage`（容器已 1:1，战绩子页待移植） | 248px 左栏 + 战绩菜单 + 空白 Frame；登录用 cookie 拉角色（见 08 §1） |
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
│   ├── DialogHost.axaml               # ContentDialog 等价物（Lead）
│   └── ToastHost.axaml                # InAppToast 等价物（Lead）
├── Views/Starward/
│   ├── StarwardShell.axaml            # MainView 等价物（Lead）
│   ├── MainWindow.axaml               # 1200×676 外壳 + 标题栏（Lead）
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
- 弹窗走 `context.Shell.Dialogs.ShowAsync(...)`；Toast 走 `context.Shell.Toast(...)`。
- 页面不得引用旧窗口（`Views/SettingsWindow` 等）；旧窗口在对应页面验收通过后删除。
- 页面内样式优先局部 `<UserControl.Styles>`，颜色引用 `Starward.axaml` 的资源键。

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
