# 08 — UI 源码对照（Starward → LunarWard）

> 用户的验收标准：**Starward 的 UI 源码是唯一真值**。先一比一移植 XAML 结构与
> Style，再用 1200×676 截图验证；不按截图猜版式，不把"自创设计"写进注释冒充合同。
> 本文记录每一页的源文件对应关系、当前完成度、剩余差距和下一步队列。
> `07-UI-REPLICA.md` 仍是视觉 token / 布局契约；发生冲突时以 Starward 源码为准，
> 并回头修正 07（见 §5）。

## 0. 方法

1. 每个页面先读 Starward 对应 `.xaml`（结构、Margin、尺寸、字号、圆角、资源键）
   与 `.xaml.cs`（状态、交互、动画），逐段映射到 Avalonia 的等价控件。
2. WinUI 专有控件（NavigationView、Pivot、FlipView、ItemsView、SplitButton、
   AnimatedIcon、Popup、TeachingTip）在 Avalonia 侧用同结构的替代实现，
   但**位置、尺寸、间距、层级、状态**必须对得上。
3. LunarWard 独有能力**追加**在 Starward 已有页面/位置，不替换原版内容。
4. 不伪造数据与素材：缺素材就保留占位结构并注释原因，不画假的卡片。

## 1. 页面完成度

图例：✅ 结构已 1:1；🟡 结构接近、细节缺失；🔴 版式自创，需重写。

| 页面 | Starward 源 | LunarWard 实现 | 状态 | 主要差距 |
|---|---|---|---|---|
| 窗口外壳 | `Features/ViewHost/MainWindow.xaml`(35) + `.cs`(268) | `MainWindow.axaml`(45) | ✅ | WinUI 系统标题栏按钮用自己的 46×32 实现；不可缩放/最大化一致 |
| 主视图 | `MainView.xaml`(152) | `StarwardShell.axaml`(135) | 🟡 | 导航项 5 vs 7（缺 SelfQuery / GenshinBeyondGacha，账号记录被并进工具箱）；无 BlankPage 状态；`Border_OverlayMask` 的淡入淡出用了 IsVisible 切换 |
| 左栏 | `MainView.xaml` 的 NavigationView | `NavigationRail.axaml`(129) | ✅ | LeftCompact 56px、40×40、选中 3×16 强调条一致；图标条见下 |
| 游戏选择 | `GameSelector.xaml`(572) + `.cs`(975) | 外壳内的全屏层 | 🟡 | **已补横排图标条**（悬停当前图标滑出、#60000000 遮罩、当前图标不压暗）；全屏层的服务器选择、固定、未安装游戏列表、拖放排序未移植 |
| 启动器 | `GameLauncherPage.xaml`(684，其中 PipsPager 样式 ~330 行) | `Pages/GameLauncherPage.axaml`(281) | 🟡 | 公告卡/开始按钮/时长胶囊位置一致；预下载、安装进度态、DX12 选项、区服 ComboBox、拖放背景、账号切换器（Starward 是 40 高 ComboBox + 切换/编辑）、便笺 Flyout 未移植 |
| 游戏设置 | `GameSettingPage.xaml`(199) | `Pages/GameSettingPage.axaml`(720) | 🔴 | Starward 左列=分辨率/全屏/语音，右列=图形（HDR/帧率），底部 60 应用条；当前页是目录/区服/wine/env/外部程序——**Starward 原版设置没有移植**，需按 07 §2 追加区的方式重排 |
| 设置总览 | `SettingPage.xaml`(108) | `Pages/SettingPage.axaml`(160) | ✅ | 260 二级导航 + Frame Margin 24,24,0,0 一致 |
| 关于 | `AboutSetting.xaml`(135) | `SettingAboutView.axaml`(110) | 🟡 | LunarWard 品牌内容替换（合理），链接区/日志目录已本地化 |
| 通用 | `GeneralSetting.xaml`(87) | `SettingGeneralView.axaml`(119) | 🟡 | 追加了主题/启动行为；缺"帮助翻译"链接；Windows 系统效果段落省略（合理） |
| 下载 | `DownloadSetting.xaml`(104) | `SettingDownloadView.axaml`(102) | 🟡 | 逐项核对后补齐 |
| 文件管理 | `FileManageSetting.xaml`(206) | `SettingFileManageView.axaml`(180) | 🟡 | 缓存/日志列表结构需逐项核对 |
| 截图设置 | `ScreenshotSetting.xaml`(189) | `SettingScreenshotView.axaml`(96) | 🟡 | 只移植了一部分开关 |
| 快捷键 | `HotkeySetting.xaml`(62) | `SettingHotkeyView.axaml`(89) | 🟡 | 键盘录入对话框（`HotkeyInput.xaml` 117）未移植 |
| 手柄增强 | `GamepadControlSetting.xaml`(207) | `SettingGamepadView.axaml`(38) | 🟡 | 依 Windows GameInput，LunarWard 不支持；按"说明 + 保留菜单项"处理（如实告知） |
| 高级 | `AdvancedSetting.xaml`(125) | `SettingAdvancedView.axaml`(169) | 🟡 | 追加了 LunarWard 项；逐项核对 |
| 工具箱 | `GameRecordPage`（GameRecord/*）+ `DailyNoteButton.xaml`(722) | `Pages/ToolboxPage.axaml`(409) | 🔴 | Starward 工具箱是游戏记录总览；每日便笺是**启动器右下按钮的 Flyout**。当前页把便笺/账号做成两个 Tab，且便笺 Tab 显示 cookie 输入框——版式自创，需重写 |
| 截图页 | `ScreenshotPage2.xaml`(319)（+`ImageViewWindow2.xaml` 731） | `Pages/ScreenshotPage.axaml`(263) | 🟡 | 本轮已按源码重写：44px 页头 + 磁贴墙（列宽按 UniformGridLayout 公式均摊）+ 空态。未移植：ItemsView 容器虚拟化、AnnotatedScrollBar、悬停 1.2× 放大、`ImageViewWindow2`（点击走系统查看器）、截图文件夹管理对话框（Flyout 三行代替） |
| 抽卡页 | `GachaLogPage.xaml`(475) + `GachaStatsCard.xaml`(293) + `ZZZGachaStatsCard`(294) | `Pages/GachaPage.axaml`(451) + `Controls/GachaStatsCard.axaml`(306) | 🟡 | 本轮已按源码重写：44px 页头（UID/更新 SplitButton/设置 Flyout/强调色 Pane 按钮）+ SplitView + 统计卡（行序、字号、稀有度色照抄）+ 5★/4★ 列表 + 空态。未移植：ZZZ 统计卡、材料图标与卡池底图（无素材，占位）、按时间段删除、UIGF v4、虚拟化 |
| 游戏公告窗 | `GameNoticeWindow.xaml`(273) | 无 | 🔴 | 未移植 |
| 安装/更新 UI | Installer + `InstallGameDialog` | 仅后端 | 🔴 | 未移植 |
| 素材 | Starward 自带 emoji/图标 PNG | 运行时从 HoYoPlay 拉 | 🟡 | 无 emoji 素材，空态用字形占位并在注释说明 |

## 2. 与 `07-UI-REPLICA.md` 的更正

| 07 原文 | Starward 源码事实 | 处理 |
|---|---|---|
| §4.4「公告卡无自动轮播（Starward 无 timer）」 | `GameBannerAndPost.xaml.cs` 有 5s `DispatcherQueueTimer`，悬停暂停、移出恢复 | 实现已按源码（VM `_bannerTimer`），改正 07 |
| §5「账号记录并入工具箱」 | `MainView.xaml` 有独立 `SelfQueryPage` 导航项 | 属自创合并；恢复独立页再按 1:1 移植 |
| 截图页「左列表 + 右预览」 | `ScreenshotPage2.xaml` 是 44px 页头 + `ItemsView` 磁贴墙（204×115、间距 12） | 重写 |
| 抽卡页「整页虚拟化列表」 | `GachaLogPage.xaml` 是 44px 页头 + `SplitView`，记录收在 `GachaStatsCard` 的 5★/4★ Pivot 里 | 重写 |

## 3. 本轮改动（2026-10-23 夜）

- **补 `GameSelector` 横排图标条**：`StarwardShell.axaml` 新增 `GameIconsArea`
  （Margin 56,0,0,0 / Height 60 / Padding 6,0,12,0；图标 40×40、圆角 8、每项 6,8；
  `#60000000` 遮罩，当前/悬停图标 MaskOpacity=0）。`NavigationRail` 的当前游戏图标
  外层改成 56 宽的透明 Border，悬停即滑出图标条；点击图标直接切档案。
  `--game-icons` 自检开关用于截图。
- **抽卡页重写**（t-gacha）：按 `GachaLogPage.xaml` 44px 页头 + `SplitView` +
  `GachaStatsCard` 结构；数据仍来自现有 `GachaPageViewModel`。构建 0 警告 0 错误。
- **截图页重写**（t-screenshot）：按 `ScreenshotPage2.xaml` 44px 页头 + 磁贴墙；
  缩略图后台 412px 解码，列宽按 `floor((w+12)/(204+12))` 均摊。构建 0 警告 0 错误。
- 全量测试 334 通过（276 单测 + 31 安装器 + 27 集成）。

## 4. 下一步队列（按用户可见度排序）

1. 游戏设置页：把 Starward 的分辨率/语音/图形设置按原版两列 + 60px 应用条恢复，
   LunarWard 的目录/区服/wine/env/外部程序/诊断作为**下方追加区**。
2. 工具箱：按 `GameRecordPage` 结构重写；每日便笺改成启动器右下按钮的 Flyout
   （`DailyNoteButton.xaml` 1:1），账号记录恢复独立导航页（`SelfQueryPage.xaml`）。
3. 启动器剩余控件：`GameAccountSwitcher`（40 高 ComboBox + 切换/编辑）、
   `StartGameButton` 的安装进度态、预下载、DX12、区服 ComboBox、拖放背景。
4. 设置子页逐项核对（截图/快捷键/文件管理/高级/下载）。
5. 公告窗、安装/更新 UI、`ImageViewWindow2`。

## 5. 验收

- 每页改动都跑：`dotnet build` 0 警告 0 错误；`--screenshot` 1200×676 出图；
  `dotnet test` 全绿。
- 在本文 §1 更新状态与差距；新增"自创"必须当场改回源码结构，不得只改注释。
