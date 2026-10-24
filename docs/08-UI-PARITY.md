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
| 启动器 | `GameLauncherPage.xaml`(684，其中 PipsPager 样式 ~330 行) | `Pages/GameLauncherPage.axaml`(约 560) | 🟡 | 公告卡/开始按钮/时长胶囊位置一致；**便笺已改为 `DailyNoteButton.xaml` 的 Flyout**（刷新/角色行/原神树脂+委托+宝钱+派遣/绝区零电量+会员+任务+活跃度+刮刮卡+录像店+悬赏+周纪）；Starward 默认隐藏账号切换器，已移除自创的圆形账号按钮。账号切换条已补（40 高双胶囊：账号 ComboBox + 切换/编辑按钮，Margin 0,0,40,100；源码需在设置开启，这里改为保存过账号即显示；“编辑”打开 LunarWard 已有的账号窗口，而非源码的内联 Flyout）。背景切换条已补：可点圆点（选中 8/普通 6，源码 PipsPager 同构）、滚轮循环切换、图库（Linux 等价为打开背景目录）/复制（图片进剪贴板）/另存为（文件选择器）/视频播放暂停。未移植：预下载、安装进度态、DX12 选项、区服 ComboBox、拖放背景、`GameAuthLoginButton` |
| 游戏设置 | `GameSettingPage.xaml`(199) + `GameSettingService.cs` | `Pages/GameSettingPage.axaml`(约 900) + `Services/WineRegistry.cs` / `GameSettingRegistry.cs` | 🟡 | 本轮已按源码恢复左列（分辨率/全屏/无边框/自定义分辨率/语音语言）+ 右列图形设置 + 60px 应用条；值通过 `wine reg` 读写游戏 prefix 的 `HKCU\Software\miHoYo\<游戏>`（与 Starward 同一组键）。LunarWard 目录/wine/env/外部程序/诊断移到下方追加区。未移植：HDR（Linux/Wine 无等价物，开关禁用+说明）、按显示器尺寸过滤分辨率列表、星铁帧率（LunarWard 不支持星铁）、游戏版本检测（用 exe 是否存在近似） |
| 设置总览 | `SettingPage.xaml`(108) | `Pages/SettingPage.axaml`(160) | ✅ | 260 二级导航 + Frame Margin 24,24,0,0 一致 |
| 关于 | `AboutSetting.xaml`(135) | `SettingAboutView.axaml`(约 190) | 🟡 | 本轮已按源码重排：Logo 160 / 24px Bold / 版本行 / 预览版 Toggle / 检查更新按钮 / 相关链接；Windows 更新器与预览通道如实禁用 |
| 通用 | `GeneralSetting.xaml`(87) | `SettingGeneralView.axaml`(约 160) | 🟡 | 本轮已对齐：语言 ComboBox 36/260/圆角 8,18,18,8、帮助翻译链接、关闭窗口两 Radio、注册表账号 Toggle、分隔线、系统效果说明位；LunarWard 主题/启动/公告追加在后 |
| 下载 | `DownloadSetting.xaml`(104) | `SettingDownloadView.axaml`(约 210) | 🟡 | 本轮已对齐：默认安装路径 + 定位 + 文件夹胶囊、硬链接 Toggle、限速行；Windows 默认路径/硬链接/限速控件禁用并说明 |
| 文件管理 | `FileManageSetting.xaml`(206) | `SettingFileManageView.axaml`(约 400) | 🟡 | 本轮已对齐：数据文件夹、备份数据库（ProgressRing→ProgressBar）、删除所有设置、日志、缓存五分类真实占用、清理缓存 + 清理背景；SQLite 备份/改目录禁用并说明 |
| 截图设置 | `ScreenshotSetting.xaml`(189) | `SettingScreenshotView.axaml`(约 350) | 🟡 | 本轮已对齐：文件夹按钮/胶囊、快捷键占位、格式质量（Segmented 无等价物→禁用占位）、三个 Toggle、HDR 说明、测试/清缓存、两段 12px 说明；WIC 转码/全局热键禁用并说明 |
| 快捷键 | `HotkeySetting.xaml`(62) | `SettingHotkeyView.axaml`(约 130) | 🟡 | 本轮已对齐标题/行高/宽度；`HotkeyInput` 用同边框/圆角的禁用占位（Linux 无全局热键） |
| 手柄增强 | `GamepadControlSetting.xaml`(207) | `SettingGamepadView.axaml`(约 200) | 🟡 | 本轮已按源码重排标题/Toggle/说明/模拟输入/按键映射/三段说明；GameInput 不存在 → 控件禁用 + Linux 替代方案（Steam Input）追加在后 |
| 高级 | `AdvancedSetting.xaml`(125) | `SettingAdvancedView.axaml`(约 230) | 🟡 | 本轮已对齐 URL 协议 / RPC 服务 / 日志级别三段；RPC 在 LunarWard 不存在 → 保留状态行、去掉伪造按钮；日志/env 追加在后 |
| 工具箱 | `GameRecordPage.xaml`(527) + `.cs` + 各战绩子页 | `Pages/ToolboxPage.axaml`(约 430) + `.cs`/VM；子页 `Pages/Record/SpiralAbyssPage.axaml`(约 360) + VM(约 480)、`Pages/Record/ImaginariumTheaterPage.axaml`(约 470) + VM(约 520)、`Pages/Record/StygianOnslaughtPage.axaml`(约 320) + VM(约 430)、`Pages/Record/InterKnotMonthlyReportPage.axaml`(约 350) + VM(约 540)、`Pages/Record/DeadlyAssaultPage.axaml`(约 600) + VM(约 480)、`Pages/Record/ShiyuDefensePage.axaml`(约 760) + VM(约 740) | 🟡 | 容器已按源码重写：248px 左栏（头像/角色/登录 Flyout/角色列表）、战绩菜单、底部「更新设备指纹」、右侧 Frame；角色通过 cookie 调 `GetGameRolesAsync` 实时拉取。**深境螺旋、幻想真境剧诗、幽境危战、绳网月报、危局强袭战、式舆防卫战六个子页已 1:1 移植**；其余子页仍是"尚未移植"占位（不伪造数据）。登录用 cookie 代替 WebView；头像暂用通用图标（未拉 HeadIcon）；难度勋章/Segmented 因缺素材与控件差异用数字圆章/双 Button 占位（幻想真境剧诗的勋章/星章/幻剧之花同样用同色圆章 + 星形字形占位） |
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
- **游戏设置页**（Lead）：按 `GameSettingPage.xaml` 恢复 Starward 基础设置两列 +
  60px 应用条；新增 `Services/WineRegistry.cs`（`wine reg query/add`，含 REG_DWORD /
  REG_BINARY 解析）与 `Services/GameSettingRegistry.cs`（键名/值名逐条照抄
  `GameSettingService.cs`，原神分辨率/全屏/语音、绝区零分辨率/全屏）。
  LunarWard 设置移到下方追加区（20px 标题 + 说明）。
- 真 wine 往返：`LUNARWARD_REAL_WINE=1 LUNARWARD_TEST_WINE=<wine>` 跑
  `WineRegistryRealWineTests`（wineboot 建 prefix → 写 DWORD → 读回），本机通过。
- **工具箱容器重写**（Lead）：按 `GameRecordPage.xaml` 把自创的"每日便笺/账号记录"
  双 Tab 换成 Starward 的 248px 左栏 + 战绩菜单 + 空白 Frame；登录 Flyout 用 cookie
  调 `GameRecordClient.GetGameRolesAsync` 拉角色（昵称/区服/Lv），凭据复用便笺存储；
  「更新设备指纹」保留位置但禁用；「本机游戏账号」是 LunarWard 追加入口（wine 快照）。
  战绩子页未移植处显示占位并如实标注。
- **每日便笺 Flyout**（Lead）：按 `DailyNoteButton.xaml` 把启动器右下便笺按钮从
  「跳到工具箱」改为真正的 Flyout；`DailyNoteService` 增加原神/绝区零结构化字段
  （`GenshinDailyNoteExtra` / `ZzzDailyNoteExtra`），角色行/刷新/错误/无 cookie 引导
  照源码；移除 Starward 默认隐藏的账号切换圆形按钮。新增 `--open-daily-note`
  自检开关（Flyout 是独立 Popup，主窗口截图拍不到，自检时单独渲染 `PopupRoot`）。
- **设置页 9 个子页**（t-settings）：按 `Features/Setting/*.xaml` 逐段重排，
  每个 XAML 头部注释标源码行号；Windows 专有项保留结构位置并禁用/说明，不伪造。
  新增 `--settings-section <tag>` 自检开关（about/general/download/files/screenshot/
  hotkey/gamepad/advanced/toolbox），逐子页出图（含 en-US）。
  已知偏差：ProgressRing→ProgressBar、Segmented→禁用占位、Hyperlink→透明按钮、
  GridView 无虚拟化、XboxFluent2 字形用 MDL2 替代、禁用 Toggle 文案不变灰
  （全局样式属 Lead 独占，未动）。
- 全量测试 348 通过（290 单测 + 31 安装器 + 27 集成；其中 14 个是新增的注册表
  解析/映射用例，真 wine 往返用例默认跳过）。

- **深境螺旋子页**（Lead）：按 `Features/GameRecord/Genshin/SpiralAbyssPage.xaml` 移植
  周期 ListView（日期 + 星数、选中态）、刷新按钮（0,18,18,0 强调胶囊）、统计周期行、
  出战统计 3 列（-4 上移）、出战次数 4 卡（80×97、稀有度底、`×次数` 右下）、
  战斗数据榜（2*,*,*、隔行 -24 底色、图标 44×36）、每层卡片（24,12 内边距、28 高层头、
  9/9 星数）与每间（上/下半阵容 60×73、中间间名/时间/三星）。数据用
  `GameRecordClient.GetSpiralAbyssAsync`（国服 Hyperion / 国际 HoYoLab 自动分流）。
  新增 `--scroll-y <px>` 自检开关（截图前滚动主 ScrollViewer，截长页下半部分）。
  420 行以下页面走 demo 夹具 `LUNARWARD_TOOLBOX_DEMO=1`（`SpiralAbyssPageViewModel` 内），
  不联网。
- **幽境危战子页**（Lead）：按 `Features/GameRecord/Genshin/StygianOnslaughtPage.xaml`
  移植各期 ListView（日期 + 难度勋章 28×28 + `Ns`）、0,18,18,0 强调刷新胶囊、
  统计周期行、最佳记录（难度 + `Ns`）与单人/多人 Segmented、每个 Boss 卡片
  （24,12 内边距、怪物名 / 战斗用时 / 80×97 阵容 + Rank 角标 + Lv / 220 高怪物立绘 /
  怪物等级圆章 + 描述 Flyout 360 / 最强一击 + 最高总伤 40×40 图标）。
  数据一次 `GameRecordClient.GetStygianOnslaughtInfosAsync` 拉全部期（API 返回完整
  对象，Starward 的 SQLite Value 列同源）；缺本地勋章 PNG 与 Segmented 控件，
  用难度色数字圆章 + 两个 Button（`Classes.selected`）占位，不伪造素材。
  demo 夹具同样走 `LUNARWARD_TOOLBOX_DEMO=1`。
- **绳网月报子页**（Lead）：按 `Features/GameRecord/ZZZ/InterKnotMonthlyReportPage.xaml`
  移植：左栏 0,18,18,0 强调刷新胶囊 + 「获取明细」MenuFlyout（可选月份）、月份 ListView
  （`yyyyMM` + 菲林数）；右侧 24 外边距，实时数据卡与历史数据卡均含（菲林/母带/邦布券三图标 +
  数量、分隔线、`菲林收入组成` 色条 `ColorRectChart` + 百分比图例），每日数据卡三列进度条
  （菲林 #A0FF53B2 / 母带 #A066BCF2 / 邦布券 #A0F2DE77）。数据用
  `GameRecordClient.GetInterKnotReportSummaryAsync` + `GetInterKnotReportDetailAsync`
  （按 award.data_type 逐类拉取，区域按 GameBiz 走 Hyperion/HoYoLab），月份明细按 (月, 类型) 缓存。
  Starward 用 SQLite 缓存月份列表，LunarWard 改为实时 summary + 明细字典重建列表
  （★ 差异：首次加载前「获取明细」为空列表；单类别明细 404/无数据时忽略，整页失败只在页面显示错误文案）。
  菲林/母带/邦布券 PNG（IconCurrency/GachaTicket2Big/GachaTicket3Big）未打包，用同色
  `✦`/`▤`/`◉` 字形占位并在 XAML 注释；demo 夹具 `LUNARWARD_TOOLBOX_DEMO=1`。
- **危局强袭战子页**（Lead）：按 `Features/GameRecord/ZZZ/DeadlyAssaultPage.xaml` 移植：
  左栏 0,18,18,0 强调刷新胶囊 + 赛期 ListView（日期 + 绝境模式行「分数 + 星 + xN」+
  试炼模式行「分数 + 星 + xN」，绝境用 `star-icon-hard` 橙色、试炼用 `star-icon-light`
  浅蓝，以字形占位）；右侧 24 外边距，统计周期行后按 `HasHard` 显示绝境模式卡
  （标题 / 28 号分数 + 排名百分比圆章 / 右对齐 36 号星 + xN），每节点卡（142×194
  Boss 立绘 + 名字 / 24 号分数 + 星 / 通关时刻 / 76×94 角色卡 + 58×78 邦布 +
  32×32 增益图标与描述，列间距 16、行间距 4、圆角 12、`BoxShadow`），再是试炼模式卡
  （同结构，星用浅色）。数据用 `GameRecordClient.GetDeadlyAssaultInfoAsync(schedule=1/2)`
  （当期 + 上期）；缺 `rank-bg-*.png` / 稀有度图标 / `star-icon-*.png`，用同色渐变圆章 +
  `S`/`A` 字形 + `E735` 星形字形占位，不伪造素材。demo 夹具 `LUNARWARD_TOOLBOX_DEMO=1`。
- **式舆防卫战子页**（Lead）：按 `Features/GameRecord/ZZZ/ShiyuDefensePage.xaml` 移植：
  左栏 0,18,18,0 强调刷新胶囊 + 赛期 ListView（日期 + S/A/B 圆章 + 次数，v2 赛期显示
  概要分数）；右侧按 `hadal_ver` 分流——**v1**：出战统计条（S/A/B 次数 + 最高防线 + 4-7 层
  时长，三列居中的 S/A/B 章 + 两列居中的最高防线 / 4-7 层时长）与逐防线卡（20,8 内边距、
  防线名 / 防线效果 Flyout、右对齐评级章 + 挑战时间、分隔线、阵容 1 / 阵容 2 两栏（各 312 宽、
  中间竖分隔线、敌人详情 Flyout：68px 怪物图占位 + 名字 + 弱点/抗性属性章）、每栏 76×94 角色卡 +
  58×78 邦布、逐栏通关时长）；**v2**：680 宽概要卡（28 号分数 +
  排名百分比章 + 评级 + 挑战时间 / 通关时长）作为第五防线卡头，卡内逐关 208 起高卡（左：24 号分数 +
  评级章 + 防线效果 Flyout + 通关时长 + 通关时刻；右：220 宽怪物图占位；下：76×94 角色卡 + 邦布），
  第四防线卡（防线效果 Flyout + 通关时刻 + 右对齐评级章 + 横向 312 宽阵容栏）。
  数据用 `GameRecordClient.GetShiyuDefenseInfoAsync(schedule=1/2)` 拉 `hadal_info_v1`/
  `hadal_info_v2`；缺 S/A/B 评级图与属性图标，用同色文字章占位；demo 夹具
  `LUNARWARD_TOOLBOX_DEMO=1`（`LUNARWARD_SHIYU_V1=1` 切到 v1 演示赛期，便于截图）。
- **幻想真境剧诗子页**（Lead）：按 `Features/GameRecord/Genshin/ImaginariumTheaterPage.xaml`
  移植各期 ListView（日期 + 勋章圆章 + `幕/最大幕` + `星章/最大幕`）、0,18,18,0 强调刷新胶囊、
  统计周期行、演出回顾 5 行（最佳记录 = 难度模式 + 第 N 幕 + 月谕圣牌数 / 12 颗逐幕星章 /
  幻剧之花数 / 触发场外观众声援 / 助演角色支援其他玩家，隔行 -24 底色）、战斗统计 5 行
  （演出总时长 / 最高伤害输出 / 击败最多敌人 / 承伤最多 / 最快完成演出队伍，44×36 头像）、
  参演名单（每幕：星章 + 第 N 幕或圣牌挑战 N + 完成时间、80×97 阵容、辉彩祝福 / 奇妙助益 /
  神秘收获三组 32×32 图标 + ToolTip）。数据一次 `GameRecordClient.GetImaginariumTheaterInfosAsync`
  （`need_detail=true` 返回完整对象）；缺勋章/星章/幻剧之花 PNG，用同色圆章 + `★`/`✦`/`✿`
  字形占位并在 XAML 注释；头像类图片缺失时回退 `E77B` 字形（与深境螺旋一致）。
  demo 夹具同样走 `LUNARWARD_TOOLBOX_DEMO=1`。
- **旅行者札记子页**（Lead）：按 `Features/GameRecord/Genshin/TravelersDiaryPage.xaml` 移植：
  左 280 栏月份卡列表（`E787` 图标 + `YYYY-MM` + ◆ 原石数）+ 刷新 / 获取明细两枚按钮；
  右栏三张 8 圆角 acrylic 卡 —— 实时数据卡（今日共获取 / 当月共获取两列，原石 ◆ / 摩拉 ● 字形 +
  数值、`ColorRectChart` 20 高分段条 + 图例换行面板）、历史数据卡（选中月份同结构）、
  每日数据表（`MM-dd` / 原石数 / 进度条 `#A066BCF2` / 摩拉数 / 进度条 `#A0F2DE77`，每日进度 =
  当天值 / 当月最大值）。新增 `Controls/ColorRectChart.cs`（硬边 `LinearGradientBrush`，
  offset 成对写 stop）；「获取明细」在代码后置里现场 `MenuFlyout` 月份项（前台 Flyout 内容
  不在逻辑树里，模板绑定拿不到 DataContext）。
  数据：`GetTravelsDiarySummaryAsync` + `GetTravelsDiaryDetailAsync(month, type=1/2)`；LunarWard
  没有 Starward 的 SQLite 全量缓存，月份列表由 `optional_month` 直接生成，选中月份时按需拉取。
  缺 `UI_ItemIcon_201/202.png`，原石/摩拉用 `◆`/`●` 字形占位。
  demo 夹具同样走 `LUNARWARD_TOOLBOX_DEMO=1`（示例：2026-10/12800 原石/1234567 摩拉 + 6 段图例 + 24 天明细）。
- **工具箱挂载**：`ToolboxPageViewModel.PortedTags` 列出已移植菜单
  （SpiralAbyss / ImaginariumTheater / StygianOnslaught / InterKnotMonthlyReport / DeadlyAssault / ShiyuDefense / TravelersDiary），`ToolboxPage.UpdateHostedPage`
  按 tag 创建 `Record.SpiralAbyssPage` / `Record.ImaginariumTheaterPage` /
  `Record.StygianOnslaughtPage` / `Record.InterKnotMonthlyReportPage` / `Record.DeadlyAssaultPage` / `Record.ShiyuDefensePage` / `Record.TravelersDiaryPage` 挂到 `ContentControl`。
- **战绩（BattleChronicle）**：Starward 是独立 `BattleChronicleWindow` + `BBSWebBridge`（WebView2 载入
  米游社/HoYoLAB 网页并注入 cookie）。Linux 没有 WebView2，等价实现为按 `BBSWebBridge.xaml.cs`
  的 `(GlobalServer, Game)` 映射用系统浏览器打开同一 URL（`ToolboxPageViewModel.BattleChronicleUrl` /
  `OpenBattleChronicleAsync`），工具箱内容区只显示标题 + 浏览器提示（`ShowWebRecordHint`）。

## 4. 下一步队列（按用户可见度排序）

1. 工具箱战绩子页：按 Starward `Features/GameRecord/{Genshin,ZZZ}/*Page.xaml`
   逐个移植（深境螺旋 ✅、幻想真境剧诗 ✅、幽境危战 ✅、绳网月报 ✅、危局强袭战 ✅、式舆防卫战 ✅、旅行者札记 ✅ → 战绩 BattleChronicle、绳网月报历史等剩余子页），
   数据用 `GameRecordClient` 的对应 API；再补 `SelfQueryPage`（账号记录独立页）。
2. 启动器剩余控件：`StartGameButton` 的安装进度态、预下载、DX12、区服 ComboBox、
   拖放背景、`GameAuthLoginButton`。（`GameAccountSwitcher` ✅ 已补，见 §1 启动器行）
3. 设置子页逐项核对（截图/快捷键/文件管理/高级/下载）。
4. 公告窗、安装/更新 UI、`ImageViewWindow2`。

## 5. 验收

- 每页改动都跑：`dotnet build` 0 警告 0 错误；`--screenshot` 1200×676 出图；
  `dotnet test` 全绿。
- 在本文 §1 更新状态与差距；新增"自创"必须当场改回源码结构，不得只改注释。
