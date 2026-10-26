# 09 — Linux 与上游的差异清单（不可移植项与替代方案）

> Starward 是 Windows 应用；LunarWard 的目标是「Linux 上能用、UI 与上游一致」。
> 本文把**所有主动与上游不一致的地方**集中登记：为什么不做、做了什么替代、在
> `08-UI-PARITY.md` 的哪一行。原则：能移植的必须移植；不能移植的显式说明，
> 不画假按钮、不假装成功。

## 一、没有 Linux 等价物（保留结构，禁用并说明）

| 能力 | 上游做法 | LunarWard | 理由 / 替代 |
|---|---|---|---|
| 卸载游戏 | `UninstallGame` 调 Windows 卸载器 | 按钮保留、禁用并给原因 | Linux 无注册表卸载器；删除目录由用户自理 |
| 命令行启动游戏 | 写 `.cmd`/快捷方式 | `lunarward startgame --biz` | 见 README「命令行」 |
| 渠道 SDK 分组（腾讯/哔哩哔哩） | 注册表读取渠道分组 | 只保留官方区服 | 渠道 SDK 不提供 Linux 客户端 |
| 背景音量 / 系统媒体控制 | WinRT `MediaPlayer` 音量 | 视频静音播放 | 没有跨桌面音量总线 |
| HDR / 自动 HDR | Windows 图形设置 | 不做 | 与 Wine/HDR 输出无关，由显示栈决定 |
| 内嵌更新器 | MSIX 更新 | 「检查更新」打开 releases 页 | Linux 走发行包（deb/AppImage） |
| 全局热键注册 | `RegisterHotKey` | 显示快捷键但禁用 | X11/Wayland 全局热键需 portal，未接 |
| Discord RPC | 命名管道 | 「RPC 服务」段保留、禁用 | 需要额外 IPC 客户端，非首期目标 |
| 崩溃上报 | WER + 自建服务 | 只写本地日志 / launch-report | 不向外发送数据 |
| 手柄增强（DS4Windows 式） | ViGEm 驱动 | 保留入口、禁用 | 需要内核驱动 |

## 二、素材缺失（占位结构 + 注释，不画假图）

| 位置 | 上游素材 | LunarWard |
|---|---|---|
| 卡池统计卡底图 | `Assets/Gacha/*.png` | 纯色圆角底（parity 抽卡页行） |
| 空态插画（抽卡/账号记录/千星奇域） | 上游插画 | 星形/图标占位 + 文案 |
| 千星奇域远程图标 | 远程 PNG | 文字首字占位 |
| 应用图标 | `Assets/AppIcon.ico` | 生成的 `lunarward.ico`（占位） |

- **截图页月份标注（2026-10-25，`f9f5622`）**：上游用 WinUI `AnnotatedScrollBar`
  标月份分组，Avalonia 无对应控件，改为滚动时浮出的胶囊。逻辑抽成
  `ScreenshotMonthIndex`（10 个单测），并已用截图验证：种子 60 张跨 9 个月时，
  滚动 0 → 胶囊显示「2026-10」，滚动 1600 → 显示「2026-05」，1.1s 后自动隐藏。
  出图方式：`--screenshot --page screenshot --screenshot-seed --scroll-y 1600
  --scroll-target WallScroller`（`--scroll-target` 按可视树里的控件名精确滚动）。

## 三、框架层替代（WinUI 控件 → Avalonia 等价物）

| 上游 | LunarWard | 说明 |
|---|---|---|
| `ContentDialog`（WinUI 窗口内浮层：标题行 48 / 按钮栏 / 右上 X） | `dh:DialogHost`（`DialogHost.Avalonia 0.12.3`）+ 自研容器 `Views/Starward/Controls/StarwardContentDialog.axaml` | Avalonia 无内建 `ContentDialog`。容器复刻默认模板：圆角 8、1px 描边、`StarAcrylicBrush`、48px 标题行（20px SemiBold）、40×40 `E711` 关闭、右对齐按钮栏、`MinWidth 320` / `MaxWidth 548`（各弹窗按上游显式覆盖 400–1008） |
| `ContentDialogSmokeFill`（浮层后的暗化） | `DialogHost.OverlayBackground="#66000000"`（`MainWindow.axaml`） | 同一个 popup 层里画遮罩；判据：同像素 RGB 由 (250,238,230) 降到 (194,185,179) |
| `AcrylicBrush` 真模糊 | 半透明纯色 `#F52C2C2C` | 与 §一 的模糊条目同因（Linux 无跨桌面模糊 API） |
| `ContentDialog.ShowAsync()` 的异步结果 | `DialogService.ShowAsync(dialog, owner)` + 弹窗公开 `Request` / `Result` / `Confirmed` | 防重入、ESC、焦点归还集中在 `Services/DialogService.cs`；`DialogReentrancyGate`、`CloseWindowDecision` 是纯逻辑（有单测） |
| 弹窗作为独立 `Window`（历史实现） | 全部收敛成主窗口内浮层 | 顺带消灭「独立窗口在 KWin 里带系统边框」的问题（主窗口 `WindowDecorations=None`，浮层无边框） |

- **窗口无边框（2026-10-25，`fix/ui-dialogs`）**：主窗口 `WindowDecorations="None"` +
  自绘标题栏，KWin 不再画服务端边框；对话框不再是独立窗口，也不再声明
  `WindowDecorations` / `TransparencyLevelHint`。
- **弹窗内容高度上限**：主窗口固定 676 高，原独立窗口里 1008×564 的
  `PlayTimeStatsDialog` 塞不进浮层，内容区改为在固定高度内滚动（列宽仍 1008）。

- **「游戏设置 → 游戏包体」的版本号语义（2026-10-26，`fix/ui-r3-layout`，问题 7）**：
  上游 Pivot 标题写的是 `LatestVersion = gamePackage.Main.Major.Version`
  （`Features/GameLauncher/GameLauncherSettingDialog.xaml.cs:998`，即**包体清单**版本，
  `GetGameResourcePackages` 返回的那份）；国服原神清单里的文件名是 `YuanShen_5.5.0.*`，
  而游戏版本（`getGameBranches` 的 `branch.Main.Tag`）是 7.1.0，于是标题看起来
  「版本过旧」。LunarWard 有意偏离：标签页标题固定用**游戏版本**，包体清单版本降为
  页内副标题「游戏包体 5.5.0 · getGamePackages」，两者互不覆盖，缺字段才回退
  （`—` / 「获取游戏安装包信息失败…」）。顺带修掉旧实现把自检假数据（6.2.0）塞进
  生产异常路径的问题——`SeedPackagesForCapture()` 现在只在 `--screenshot` 下生效。

## 四、字体与背景（2026-10-25，`fix/ui-parity-pass`）

| 上游 | LunarWard | 说明 / 理由 |
|---|---|---|
| `Segoe UI`（系统字体，Windows） | `Inter` → `Noto Sans CJK SC` → `Microsoft YaHei UI` → `Noto Sans JP` → `Segoe UI` | 对齐 Windows 的笔画粗细与字宽；Linux 上不再依赖用户装 wps 的 Segoe UI。`Avalonia.Fonts.Inter`（OFL）随包分发，`Program.cs` 里 `FontManagerOptions.DefaultFamilyName` 与 fallback 链同步改 |
| `Segoe Fluent Icons` | `Segoe MDL2 Assets`（本机包，`fc-match` 已验证） | 上游 Fluent Icons 在 Linux 不可分发；全仓字形只使用 MDL2 码位。若某处引用了 Fluent 新增码位（E62E/EBD3/E917/F012），Avalonia 会回退到系统字体，评审截图时会看到「字形变了」 |
| `MediaPlayerElement`（WinUI，硬件解码） | **`ffmpeg` 子进程解帧** → `WriteableBitmap`（`Services/VideoBackgroundPlayer.cs`） | 官方动态背景先落盘缓存（`~/.cache/lunarward/bg/<sha16>.<ext>`，`.part` 原子写 + 512 MiB LRU），ffmpeg 读本地文件循环，避免 `-stream_loop -1` 每圈重下一遍。**不用** `LibVLCSharp.Avalonia` 的 `VideoView`（原生 X11 子窗口，Wayland 下背景全黑），也不用 LibVLC 的 vmem（Arch 拆分 `vlc-plugin-*` 后常缺解码器）。输出固定 **960×540 BGRA、24 fps 上限**（1280×720 肉眼无差异，但每帧多传 44% 数据）；启动器页不可见或窗口失焦时对 ffmpeg 发 SIGSTOP，CPU 归零。GPU 解码实测无收益（`vaapi`/`cuda` + `hwdownload` 的耗时反而更高，因为帧最终要回 CPU 变成 Avalonia 位图），首帧 5 秒不到即回退静态图 |
| 背景切换条 60px 区显示 | 同左，且**只在可选项 > 1 时出现**（与上游 `Count > 1` 一致） | 背景来源见 07 §2：官方接口图/视频 + 版本海报 + 本地目录 |
| 右上 / 右下 400×400 暗角渐变（`AppBackground.xaml`、右侧 200px 竖条） | **整层去掉** | 在亮色版本图上看起来是两块黑色半透明遮罩（用户反馈），且「开始游戏」按钮与便笺时钟自带底色，不需要压暗。上游这两层是为亮色壁纸准备的可读性兜底，去掉后与素材无关的页面仍靠 `Border_OverlayMask` 压暗 |
| 标题栏整宽拖拽层（`AppWindow.TitleBar` + 内容层之上的 48px Grid） | 窗口级 `PointerPressed` + `y ≤ 48` 判断，控件自己接管的点击不参与 | Avalonia 的 `Border`/`Grid` 铺 `Transparent` 会吃掉下层命中，导致左上角游戏切换按钮只有下边沿能点（用户反馈）。改由窗口过滤：只把顶栏空白处当拖拽区 |
| 背景切换条 60px 命中区 | `<Panel Background="Transparent">` | Avalonia 中无 `Background` 的 `Panel` **不参与命中测试**。原来内容收起后指针进不来，切换条只出现一次就再也呼不出（用户反馈） |
| 公告 Banner `FlipView` 滚轮翻页 | 手写 `PointerWheelChanged` → 上一页/下一页 | Avalonia 无 `FlipView`；上游 FlipView 的触摸/滚轮切换用等价手势复刻 |

- **文字抗锯齿**：Windows 用 ClearType 子像素渲染，Avalonia 默认也是子像素，截图里能在
  「开始游戏」「图标字形」边缘看到 RGB 彩边。本轮在 `MainWindow.axaml` 用
  `TextOptions.TextRenderingMode="Antialias"` 强制灰度抗锯齿——这是**有意偏离**上游的
  唯一一处视觉差异，换来与 Windows 截图并排比对时不出现彩色毛边。
