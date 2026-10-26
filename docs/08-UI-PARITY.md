# 08 — UI 源码对照（Starward → LunarWard）

> 用户的验收标准：**Starward 的 UI 源码是唯一真值**。先一比一移植 XAML 结构与
> Style，再用 1200×676 截图验证；不按截图猜版式，不把"自创设计"写进注释冒充合同。
> 本文记录每一页的源文件对应关系、当前完成度、剩余差距和下一步队列。
> `07-UI-REPLICA.md` 仍是视觉 token / 布局契约；发生冲突时以 Starward 源码为准，
> 并回头修正 07（见 §5）。

> 与上游的所有主动差异（不可移植、素材缺失、平台不同实现）集中登记在
> `09-LINUX-DIVERGENCES.md`。

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

**一阶段完成度速览（2026-10-25，逐轮验收后）**：外壳/左栏/设置总览 3 行 ✅，其余 20 行 🟡、0 行 🔴。
这 20 行的剩余差异按性质分三类，均已登记在 [09-LINUX-DIVERGENCES.md](09-LINUX-DIVERGENCES.md)：

- **无 Linux 等价物**：WebView2（公告窗改浏览器打开）、H.NotifyIcon 之外的系统 API、Windows 专属卸载器/CMD 启动/渠道 SDK 分组等 —— 这些在 Linux 上禁用而非伪造；
- **形态不同但功能不缺**：`ContentDialog`/`Popup`/`ProgressRing`/`InfoBar` 换成自绘窗口与控件（`InAppToast`、`Arc`、页面内浮层），备份从 `File.Copy` 换成 `VACUUM INTO`；
- **缺美术素材**：上游 emoji/图标 PNG 与卡池底图未随源码搬运（emoji 属 Starward 自身资源，MIT 可搬但体积大），界面用字形/占位面板代替。

仍属真实能力缺口的只有两处：游戏公告窗（无法内嵌 WebView2）与 emoji/卡池底图素材。

| 页面 | Starward 源 | LunarWard 实现 | 状态 | 主要差距 |
|---|---|---|---|---|
| 窗口外壳 | `Features/ViewHost/MainWindow.xaml`(35) + `.cs`(268) | `MainWindow.axaml`(52) | ✅ | WinUI 系统标题栏按钮用自己的 46×32 实现；不可缩放/最大化一致 |
| 主视图 | `MainView.xaml`(152) | `StarwardShell.axaml`(487) | 🟡 | 导航项 7 vs 7（千星奇域颂愿已补上）；`Border_OverlayMask` 已按上游改为 `Opacity` 绑定 + 250ms `DoubleTransition`（启动器页 0、其余 1，对应上游 `MainView.NavigateTo`）；`BlankPage` 是上游「未选游戏 / 加载中」的空页，LunarWard 的档案模型始终有当前游戏，没有等价状态（记 N/A，不算缺失） |
| 左栏 | `MainView.xaml` 的 NavigationView | `NavigationRail.axaml`(141) | ✅ | LeftCompact 56px、项 40×40、外边距 8,2、圆角 4、图标 16 居中（本轮把 `HorizontalContentAlignment` 从 Left 改 Stretch，修掉图标贴左的排版问题）、选中 3×16 强调条一致；当前游戏图标已移出左栏、与图标条同层，见下 |
| 游戏选择 | `GameSelector.xaml`(572) + `.cs`(975) | `StarwardShell.axaml`(487) + `GameChoiceViewModel.cs`(324) | 🟡 | **本轮按上游 `InitializeGameServerArea` 重做数据模型**：每款游戏一张卡（`GameChoiceViewModel` / `GameServerItem` / `GameChoiceBuilder`），国服官方 / B 站 / 国际服收进 `Servers`，不再出现「原神·国服」「原神·国际服」两张卡；卡片 192×108、`#40000000`、圆角 8、UniformToFill 缩略图 + Logo +「选择游戏服务器」悬停遮罩（#D0000000 + 强调色 2px + 48 图标 + 0.167s）已按 `GameSelector.xaml:144-186` 对齐；**区服 Flyout 改为点卡片才弹**（Bottom/Transient，24×24 区服角标 + 名称 + 固定按钮 E718/E841），删除旧「悬停 PointerEntered 自动 ShowAt」——那是用户反馈的闪烁根因；单区服卡片直接切换并写回 `profile.channel`；卡片标题始终用中文游戏名（修掉国际服显示 Genshin/ZZZ 的 bug）；语言过滤按 UI 语言（`zh-cn` 取国服信息、否则国际服信息，与上游 `LanguageUtil.FilterLanguage` 同义，上游无 `AreGameServersEnabled` 符号）；左上角当前图标 40×40 / Margin 8,10,8,8 / 圆角 8、未固定图标条时叠 20×20 区服角标；图标条 60 高、Margin 56,0,0,0、Padding 6,0,12,0、40×40 横排、Spacing 0，用 `Translation -100 ↔ 0` + 0.167s 过渡代替旧的 `IsVisible`，且仅 60px 高命中、不再在窗口左边缘残留裁切竖条；图标条只显示固定区服（`PinnedGameServers`），选择层打开时显示 E840/E841 固定按钮（配置 `GameIconsPinned`）。「已安装游戏实际占用大小」Expander 已按 `GameSelector.xaml:268-341` 补（合计 + 刷新 + 自动查找 + 每行游戏图标/游戏名/区服角标/区服名/容量/路径）；同盘重复安装的省空间量仍是「同名同大小」估算（源码用文件 ID 精确统计）；头部「计算中」用 `ProgressBar IsIndeterminate` 16×16 代替 `ProgressRing`。卡片拖放排序保留（`GameChoiceOrder` 现在存游戏 kind，读取时兼容旧档案 id / gamebiz）；`AutoSearchInstalledGamesCommand` 走 `GameScanner`。未做：上游 `TeachingTip_SelectGame`（首次引导气泡）、拖拽窗口区的动态 `UpdateDragRectangles`（Avalonia X11 无对应 API）、区服图标 PNG（缺素材，用彩色角标「国/际/B」占位） |
| 启动器 | `GameLauncherPage.xaml`(684，其中 PipsPager 样式 ~330 行) | `Pages/GameLauncherPage.axaml`(约 946) | 🟡 | 公告卡/开始按钮/时长胶囊位置一致；**便笺已改为 `DailyNoteButton.xaml` 的 Flyout**（刷新/角色行/原神树脂+委托+宝钱+派遣/绝区零电量+会员+任务+活跃度+刮刮卡+录像店+悬赏+周纪）；Starward 默认隐藏账号切换器，已移除自创的圆形账号按钮。拖放背景已补（DragOver 顶部提示条 + Drop 拷入背景目录并立刻切换，支持 jpg/png/webp/bmp/mp4/webm/mkv）。账号切换条已补（40 高双胶囊：账号 ComboBox + 切换/编辑按钮，Margin 0,0,40,100；与源码一致由通用设置「基于注册表的游戏账号切换功能」开关控制，开关已实现为读写 Wine prefix 的 user.reg；“编辑”打开 LunarWard 已有的账号窗口，而非源码的内联 Flyout）。背景切换条已补：可点圆点（选中 8/普通 6，源码 PipsPager 同构）、滚轮循环切换、图库（Linux 等价为打开背景目录）/复制（图片进剪贴板）/另存为（文件选择器）/视频播放暂停。公告铃铛已接 `GameNoticeClient.GetGameNoticeUrl`（WebView2 窗口换成桌面浏览器打开，同「战绩」策略），红点走 `IsNoticeAlertAsync`。DX12 开关已补（右下角 32 高 CompactCheckBox，写 `profile.enhancements.dx12`，启动追加 `-use-d3d12`；显隐已对齐源码 `CheckDX12ConfigAsync`：已开启或 HoYoPlay `GetDXConfigsAsync` 的 `enable_dx_switch` 为真才显示，取不到配置则隐藏；“详情”引导弹窗已补（`Views/Starward/DX12IntroDialog.axaml`，680 宽；源码的 DX11/DX12 官方对比图无素材，用同尺寸占位面板；`I18nIntro` 接口文案换成按语言的固定说明））。时长胶囊已接本机 SQLite 的 `PlayTimeStats` 表（`PlayTimeService`/`PlayTimeStore`，按区服 GameBiz 汇总，与抽卡同一个 `LunarWardDatabase.db`），点击弹出 1:1 移植的 `PlayTimeStatsDialog`（6 卡 + 15 天/12 周/12 月柱状图 + 近一年热力图，独立无边框窗口代替 Windows `ContentDialog`）；0 时长仍显示 `0h 0m`，与源码常显一致。安装/更新 UI 已补：`InstallGameDialog`（560 宽亚克力卡：路径可改/音频语言分段（汉英日韩）/解压所需+可用空间/自动创建子文件夹/硬链接说明/开始安装）+ 启动按钮按 `StartGameButton` 重做（安装中「Arc 圆环 + 已下载/剩余时间」、悬停「暂停/继续/取消」、按钮上方 320 宽进度卡；接 `LunarWard.Installer` 的 `GameInstallController` 门面，安装完自动把 profile 路径指向安装目录；「更新游戏」直接按已安装目录起 Update 任务，不弹路径对话框）。右上侧栏已补 `DailyNoteButton` + `GameAuthLoginButton`（40×40 竖排、Spacing 4）：后者按源码条件「国服 + cookie 里有 `stoken`/`mid`」显示，Flyout 为 60×60 通行证图标 + 「通行证 ID：」+ 错误行 + 重新认证 + 授权登录开关（通行证 ID 走 passport `/account/ma-cn-session/app/verify`，开关写 `Launcher.EnableLoginAuthTicket`；源码 `icon_hyperion.png`/`alert.png` 用字形代替）。预下载已补：底栏最左 `PreDownloadButton`（44 高、图标 EBD3/F5B0/E62E 随悬停切换、文案「预下载/等待中/百分比/已暂停/已完成/出错了」、按钮上方 200~240 宽的下载信息卡：已下载/速度/剩余时间/错误，接 `GameInstallOperation.Predownload`；点「预下载」先弹 `PreDownloadDialog` 确认框——400 宽、标题 + 体积卡（资源包大小/解压所需空间/可用空间，装不下标 Caution 色）+ 两条警告（无可用补丁包 / 预下载不可用）+ Accent「开始预下载」+ 右上关闭，体积口径为「匹配本地版本的增量包优先，否则 `pre_download.major`」；与源码的差异：体积极少走 Chord manifest 精算（读 package 的 Size 字段，与安装对话框同口径）、信息卡用同层 Border 代替 `Popup`）。修复入口已补在 `GameLauncherSettingDialog` 基本信息页（Accent「修复游戏」→ Repair 任务）。未移植：卸载游戏（Linux 无游戏自带卸载器）、CMD 启动（Windows 专有，开关禁用占位）、渠道 SDK 包体分组、背景音量；安装时的「详细信息/预定下载」弹窗与 `PreDownloadButton` 已实现（信息卡 + 确认框 + 增量包精算，见 `d9b780d`/`6fd9ec9`）；**网络驱动器警告已补**（`DriveHelper.GetDriveType` 判网络文件系统，定位游戏被拒并提示，与安装对话框同口径）；**公告/红点开关已接**（`LauncherSettings.ShowGameNotice` 控制铃铛可见性、`DisableNoticeRedDot` 压制红点）；**基本信息的「定位游戏」按钮已补**；齿轮按钮改为弹本对话框（此前误导航到游戏设置页，属回归修复）；**游戏包体页已补明细列表**：`TabControl` 两页（正式版版本 / 预下载版本，无预下载包时第二页隐藏），每页一列 `Expander` 分组——「完整包」+ 每个本地版本的「增量包 x.y.z」，标题右侧复制整组下载链接（接剪贴板），展开后逐行「文件名 / 体积（GB 两位小数）/ MD5」，数据来自 `GetGamePackageAsync` 的 `main`/`pre_download`。 |
| 千星奇域颂愿 | `Gacha/GenshinBeyondGachaPage.xaml`(637) + `.xaml.cs`(427) | `Pages/GenshinBeyondGachaPage.axaml` + `ViewModels/Starward/GenshinBeyondGachaPageViewModel.cs` | 🟡 | 44px 页头（UID ComboBox、更新记录、设置、材料统计 Pane 按钮）、空态、两张统计卡（常驻颂愿 1000 按 4★、活动颂愿 2000 按 5★：平均/统计四行 + 页签保底列表）、右侧 508px 材料统计 Pane（80×97 卡片 + 「N 次」）均按源码版式；保底渐变底色（64/70 抽阈值、绿 #00E079 / 红 #C83C23、Opacity 0.4）逐行照搬。**持久化与上游一致**：SQLite `LunarWardDatabase.db` 的 `GenshinBeyondGachaItem` / `GenshinBeyondGachaInfo` 两表（DDL 照抄 Starward `Sql_v16`/`Sql_v17`，`Launch/Database/GenshinBeyondGachaStore.cs`），统计口径（保底/平均/比例/倒序列表 + 头部「保底」占位行/物品计数排序）逐行对齐，7 个单测覆盖。差异：上游 `SplitButton` → 主按钮 + 下拉；`Pivot + Segmented` → 两个胶囊页签（同步两张卡）；远程图标 PNG → 只用名称文字；`ContentDialog` 输入 URL → 复用自绘 `Window`；材料卡无稀有度底图素材，用稀有色半透明底 |
| 账号记录 | `SelfQuery/SelfQueryPage.xaml`(266) + `.xaml.cs`(791) | `Pages/SelfQueryPage.axaml` + `ViewModels/Starward/SelfQueryPageViewModel.cs` | 🟡 | 44px 页头（UID ComboBox + 输入 URL）、用户信息卡、类型统计（左下 7 行，收支 + 刷新）、右侧月份明细三件套（原神/星铁/绝区零各一）均按源码版式。**持久化与上游一致**：SQLite `LunarWardDatabase.db` 的 `GenshinQueryItem` / `StarRailQueryItem` / `ZZZQueryItem` 三表（DDL 与索引照抄 Starward `Sql_v7`/`Sql_v9`，`Launch/Database/SelfQueryLogStore.cs`，`INSERT OR REPLACE` 去重、按月统计/删除、「检索全部」按涉及月份先删后写）。差异：更新中的 ProgressRing 用 `Arc` + 1s 无限旋转动画模拟（`SelfQueryPage.axaml:146-168`）；类型图标/游戏图标是上游 Assets PNG，这里用字形占位；`ContentDialog`（输入 URL / 删除本月）换成自绘 `Window`；上游的 `InAppToast` 已按上游位置自建（底部居中 StackPanel、`Margin 24,72,24,72`、左色条+标题+正文、30s 自动消失；WinUI 的 `InfoBar` 换成纯 Border，见 `Services/InAppToast.cs`），账号保存/切换与抽卡复制等操作已接入 |
| 游戏设置 | `GameSettingPage.xaml`(199) + `GameSettingService.cs` | `Pages/GameSettingPage.axaml`(约 900) + `Services/WineRegistry.cs` / `GameSettingRegistry.cs` | 🟡 | Linux 增强：启动参数页新增「Linux 性能工具」三个开关（GameMode / gamescope / MangoHud，gamescope 参数框 `-f -W 2560 -H 1440`，`--` 由启动计划自动补）：写进档案 `Wrappers`（gamemoderun 在外、mangohud 在内），并显示本机是否装了对应命令； 本轮已按源码恢复左列（分辨率/全屏/无边框/自定义分辨率/语音语言）+ 右列图形设置 + 60px 应用条；值通过 `wine reg` 读写游戏 prefix 的 `HKCU\Software\miHoYo\<游戏>`（与 Starward 同一组键）。LunarWard 目录/wine/env/外部程序/诊断移到下方追加区。未移植：HDR（Linux/Wine 无等价物，开关禁用+说明）、按显示器尺寸过滤分辨率列表、星铁帧率（LunarWard 不支持星铁）、游戏版本检测（用 exe 是否存在近似） |
| 设置总览 | `SettingPage.xaml`(108) | `Pages/SettingPage.axaml`(198) | ✅ | 260 二级导航 + Frame Margin 24,24,24,24 一致；左栏条目改 `MinHeight` 自适应（工具箱行的分隔线单独占位），子页留白统一由宿主给 |
| 关于 | `AboutSetting.xaml`(135) | `SettingAboutView.axaml`(约 190) | 🟡 | 本轮已按源码重排：Logo 160 / 24px Bold / 版本行 / 预览版 Toggle / 检查更新按钮 / 相关链接；检查更新已接线（GitHub `releases/latest`，三种状态：已是最新/有新版可打开发布页/失败，对应上游三态）；预览通道与 Windows 更新器如实禁用 |
| 通用 | `GeneralSetting.xaml`(87) | `SettingGeneralView.axaml`(约 198) | 🟡 | 关闭行为两选项已可用（托盘用 Avalonia `TrayIcon`≈上游 `SystemTrayWindow` 的 `H.NotifyIcon`；无 SNI 宿主时自动退回退出并写配置）； 本轮已对齐：语言 ComboBox 36/260/圆角 8,18,18,8、帮助翻译链接、关闭窗口两 Radio、注册表账号 Toggle、分隔线、系统效果说明位；**Linux 扩展：开机自启动 Toggle**（写 `$XDG_CONFIG_HOME/autostart/lunarward.desktop`，Exec 带 `--start-hidden`；`--start-hidden` 现在即使关闭行为是「完全退出」也会建托盘，避免窗口藏起来没入口）；LunarWard 主题/启动/公告追加在后 |
| 下载 | `DownloadSetting.xaml`(104) | `SettingDownloadView.axaml`(约 210) | 🟡 | 本轮已对齐：默认安装路径 + 定位 + 文件夹胶囊、硬链接 Toggle、限速行；Windows 默认路径/硬链接/限速控件禁用并说明 |
| 文件管理 | `FileManageSetting.xaml`(206) | `SettingFileManageView.axaml`(约 296) | 🟡 | 本轮已对齐：数据文件夹、**备份数据库已接线**（`VACUUM INTO` 热备份，保存对话框；上游是 `File.Copy`）（ProgressRing→ProgressBar）、删除所有设置、日志、缓存五分类真实占用、清理缓存 + 清理背景；SQLite 备份/改目录禁用并说明 |
| 截图设置 | `ScreenshotSetting.xaml`(189) | `SettingScreenshotView.axaml`(约 275) | 🟡 | 本轮已对齐：文件夹按钮/胶囊、快捷键占位、格式质量（Segmented 无等价物→禁用占位）、三个 Toggle、HDR 说明、测试/清缓存、两段 12px 说明；WIC 转码/全局热键禁用并说明 |
| 快捷键 | `HotkeySetting.xaml`(62) | `SettingHotkeyView.axaml`(约 130) | 🟡 | 本轮已对齐标题/行高/宽度；`HotkeyInput` 用同边框/圆角的禁用占位（Linux 无全局热键） |
| 手柄增强 | `GamepadControlSetting.xaml`(207) | `SettingGamepadView.axaml`(约 200) | 🟡 | 本轮已按源码重排标题/Toggle/说明/模拟输入/按键映射/三段说明；GameInput 不存在 → 控件禁用 + Linux 替代方案（Steam Input）追加在后 |
| 高级 | `AdvancedSetting.xaml`(125) | `SettingAdvancedView.axaml`(约 230) | 🟡 | 本轮已对齐 URL 协议 / RPC 服务 / 日志级别三段；URL 协议已真正接线（`lunarward://startgame/<biz>`、`lunarward://playtime?biz=&pid=` 走 `StartupCommands`，桌面文件的 `x-scheme-handler/lunarward` 由打包注册）；RPC 在 LunarWard 不存在 → 保留状态行、去掉伪造按钮；日志/env 追加在后 |
| 工具箱 | `GameRecordPage.xaml`(527) + `.cs` + 各战绩子页 | `Pages/ToolboxPage.axaml`(约 331) + `.cs`/VM；子页 `Pages/Record/SpiralAbyssPage.axaml`(约 360) + VM(约 480)、`Pages/Record/ImaginariumTheaterPage.axaml`(约 470) + VM(约 520)、`Pages/Record/StygianOnslaughtPage.axaml`(约 320) + VM(约 430)、`Pages/Record/InterKnotMonthlyReportPage.axaml`(约 350) + VM(约 540)、`Pages/Record/DeadlyAssaultPage.axaml`(约 600) + VM(约 480)、`Pages/Record/ShiyuDefensePage.axaml`(约 1037) + VM(约 740) | 🟡 | 容器已按源码重写：248px 左栏（头像/角色/登录 Flyout/角色列表）、战绩菜单、底部「更新设备指纹」、右侧 Frame；角色通过 cookie 调 `GetGameRolesAsync` 实时拉取。**十三个子页已 1:1 移植**：深境螺旋、幻想真境剧诗、幽境危战、绳网月报、危局强袭战、式舆防卫战、忘却之庭、虚构叙事、末日幻影、异象仲裁、开拓月历，外加旅行者札记；末日幻影统计卡含三个 BOSS，异象仲裁含王棋/骑士两段战绩与 Buff，开拓月历含实时/历史/每日数据三卡。模拟宇宙（2026-10-25）也已移植：数据总览 / 本期 / 每期 Expander 记录（世界与祝福图标以圆底字形占位）。至此源码 13 个战绩菜单全部有对应页，只有「战绩」本身仍打开系统浏览器（WebView 无法移植）。登录用 cookie 代替 WebView；头像已接（`GetGameRolesAsync` 自动填 `role.HeadIcon`，页面用 `GachaIconCache` 的内存+磁盘缓存加载，失败保留占位图标）；难度勋章/Segmented 因缺素材与控件差异用数字圆章/双 Button 占位（幻想真境剧诗的勋章/星章/幻剧之花同样用同色圆章 + 星形字形占位） |
| 截图页 | `ScreenshotPage2.xaml`(319)（+`ImageViewWindow2.xaml` 731） | `Pages/ScreenshotPage.axaml`(263) + `ImageViewWindow2.axaml`(约 360) | 🟡 | 本轮已按源码重写：44px 页头 + 磁贴墙（列宽按 UniformGridLayout 公式均摊）+ 空态。**`ImageViewWindow2` 已移植**：48px 顶栏（文件名居中 / 编辑图像 / 打开菜单 / 复制路径 / 删除二次确认 / 全屏）、可缩放拖动的图片区（滚轮以指针为锚点、按住拖动、双击还原、← → 翻页、Ctrl+0 适应窗口）、左右 80px 悬停翻页按钮（0.083s 淡入淡出）、128×72 图库条（开关 + 选中同步）、48px 底栏（索引 / 像素 / 文件大小 / 适应窗口 / 百分比 / 缩小 / 滑块 / 放大）、右侧 360px 信息面板（图像信息 / 显示器信息 / 导出图像）。磁贴点击改为打开它（不再走系统查看器）。**截图文件夹管理对话框已移植**（`Views/Starward/ScreenshotFolderManageDialog.axaml`，600 宽 / 240–400 高：说明 + 分隔线 + 文件夹列表（路径 + 打开 + 移除 / 备份）+ 备份中与结果 + 添加文件夹 + 保存 / 取消）；页头按钮从 Flyout 换成它，额外目录存 `ProfileConfig.ScreenshotFolders` 并合并进图库。**悬停 1.2× 放大已补**（`Border.tile:pointerover Image.thumb` 用 `TransformOperationsTransition`，0.6s / QuadraticEaseOut，对应源码 0:0:1 / CircleEase）。未移植：ItemsView 容器虚拟化（Avalonia 的 `ItemsRepeater` 包最高 12.0.0、与 12.1.4 核心不兼容，见 09 §三；当前 `ItemsControl` 全量建容器、缩略图懒解码）、AnnotatedScrollBar（以滚动浮出胶囊近似，已验证 2026-10→2026-05）、Win2D `CanvasSwapChainPanel` 的 HDR→SDR 亮度映射与「显示模式 / SDR 亮度」编辑区（Linux 无等价物，面板里如实注明）|
| 抽卡页 | `GachaLogPage.xaml`(475) + `GachaStatsCard.xaml`(293) + `ZZZGachaStatsCard`(294) | `Pages/GachaPage.axaml`(451) + `Controls/GachaStatsCard.axaml`(306) | 🟡 | 本轮已按源码重写：44px 页头（UID/更新 SplitButton/设置 Flyout/强调色 Pane 按钮）+ SplitView + 统计卡（行序、字号、稀有度色照抄）+ 5★/4★ 列表 + 空态。**ZZZ 统计卡已移植**（`Controls/ZZZGachaStatsCard.axaml`，S/A/B 版式 + `S 平均 / UP` + ` / 平均抽数 (UP 数量)`，绝区零记录按 `IsZZZGachaStatsCardVisible` 切换）；**`up!` 标记已接**（`LunarWard.Core/Gacha/GachaNoUp.cs` 整份复制上游卡池时间表，读库后 `GachaService.ApplyUpFlags` 按 `ItemId` + 时间重算 5★ 行的 UP；常驻池没有该表则不显示，与上游一致；4★ 保持接口值）；**三个游戏的抽卡页都已开放**（`SupportsGacha` 含 `zzz`；星铁用 `StarRailGachaClient` 拉取、卡池名走 `StarRailPools`，`gacha_id` 从 SRGF 接口写入 `StarRailGachaItem.GachaId` 列并在 UIGF v4.2 的 `hkrpg` 区段导出/导入，缺 `gacha_id` 按上游报 `MissingField`）；**「删除指定时间段」对话框已移植**（`Views/Starward/DeleteGachaLogDialog.axaml`，480→540 宽、UID/记录数/日期+24 小时制时间/6 个月提示/「确认删除」Flyout/强调色取消；删除动作与上游一致，直接 `DELETE FROM` SQLite 行）。**旧格式导出已按 UIGF v3.0 / SRGF v1.0 信封实现**（`Launch/GachaExport.cs`：`{info,list}`、`export_timestamp/export_time/export_app/region_time_zone`、原神每条带 `uigf_gacha_type`（400→301）、星铁写 `srgf_version: v1.0`；导出走 `StorageProvider.SaveFilePickerAsync`，文件名 `UIGF-v3.0-{uid}-{time}.json`）。**UIGF v4.2 窗口已移植**（`Views/Starward/Uigf4GachaWindow.axaml` + `ViewModels/Starward/Uigf4GachaWindowViewModel.cs` + `Launch/Uigf4/`：左侧「导出记录/导入记录」两项、导出列表按「游戏+UID」勾选、`uigf_gacha_type` 400→301、`timezone` 按 UID 首位推断（6→-5 / 7→1 / 其余 8）、导入按 UID 校验 + 时区偏移平移 + `Id` 去重，千星奇域 `hk4e_ugc` 单独成行、走 `GenshinBeyondGachaStore`（`GenshinBeyondGachaItem` 表）导出与导入（region / is_up / schedule_id 扩展字段照上游；上游导入默认时区 0、不按 UID 推断）；数据源与上游一致：SQLite `<DataHome>/LunarWardDatabase.db`（表 `GenshinGachaItem` / `StarRailGachaItem` / `ZZZGachaItem` 的列、主键、索引逐字照抄 Starward 的 `StarwardDatabase.db`，含 `GachaLogUrl` 表），抽卡页每次「更新记录」`INSERT OR REPLACE` 入库）。**抽卡语言切换已接**（设置 Flyout 的语言输入框双向绑定 `GachaLanguage`，「应用」按 `GachaService.ChangeItemNamesAsync` 拉对应语言 wiki 名称表、按 `item_id` 改名并回写页面与存档；网络失败只落错误条，`GachaService.ApplyItemNames` 有离线单测）。**旧格式导入已接**（设置 Flyout 的「从 UIGF v3.0 导入」按钮 → 文件对话框 → `Launch/GachaLegacyImport.cs` 解析 `{info,list}` 信封/裸数组、数值字段允许字符串，按 `GachaService.MergeById` 的 `Id` 去重合并后写入 SQLite，成功显示「已导入 N 条，当前 M 条」；解析失败只落错误条。`--gacha-import` 可做导出→导入往返自检）。**Excel 导出已接**（`Launch/GachaExcelExport.cs`：MiniExcel 1.46.0 + `Assets/Template/GachaLog.xlsx`（绝区零用 `ZZZGachaLog.xlsx`），两份模板都复制自 Starward 安装目录的同名文件，占位符 `{{list.Uid/IdText/Time/Name/ItemType/RankType/GachaType/Index/Pity}}` 与上游一致；行的 `Index`/`Pity` 照 `GetGachaLogItemEx` 按可查询卡池分组累加、出 5★ 归零（绝区零出 S=RankType 4 归零），原神 400 与 301 同池，行序按 `Id`；`--gacha-excel` 自检）。物品图标已按上游路径实现（首次打开页面后台拉 wiki 的 `GachaInfo` → 写 `Genshin/StarRail/ZZZGachaInfo` 表 → 用名字回填老记录 `ItemId` → 28×28 图标走 `GachaIconCache` 磁盘缓存；网络不可用时保留占位块）；材料统计卡也改成图标 + 次数（按 `ItemId` 分组，没同步过 wiki 时退回名称占位）；未移植：卡池底图（上游是美术资源，仓库里没有；行列表上游同样用 ItemsControl + ScrollViewer，未做虚拟化，不算差异） |
| 游戏公告窗 | `GameNoticeWindow.xaml`(32) | 无（浏览器打开同 URL） | 🟡 | WebView2 无 Linux 等价，改为 `xdg-open` 打开 `GameNoticeUrl`；窗口内按 Esc 关闭/`openInBrowser` 桥接不适用 |
| 安装/更新 UI | `InstallGameDialog.xaml` + `StartGameButton.xaml`(412，含 `Popup_GameInfoOrDownloadProgress`) | `Views/Starward/InstallGameDialog.axaml` + `Pages/GameLauncherPage.axaml` 按钮段 | 🟡 | `InstallGameDialog` 已 1:1（路径可改/语音包分段/解压所需+可用空间/自动子文件夹/硬链接说明）；开始按钮按 `StartGameButton` 重做：44 高胶囊、可用时强调色底、安装中换成「Arc 进度圆环 + 已下载/剩余时间两行」、悬停换成「暂停 / 继续 / 取消」（`ChangeGameInstallTaskStateAsync` 状态机）、按钮上方 320 宽进度卡（下载/解压/校验速度 + 剩余时间 + 百分比）。「更新游戏」直接按已安装目录起 Update 任务（源码 `UpdateGameAsync`，不弹路径对话框；扫描目录里读语音包语言）。Avalonia 无 `ProgressRing`，用 `Arc` 画；`Popup` 是独立窗口无头截不到，用页面内同位置浮层复刻版式。右上侧栏已补 `DailyNoteButton` + `GameAuthLoginButton`（40×40 竖排、Spacing 4）：后者按源码条件「国服 + cookie 里有 `stoken`/`mid`」显示，Flyout 为 60×60 通行证图标 + 「通行证 ID：」+ 错误行 + 重新认证 + 授权登录开关（通行证 ID 走 passport `/account/ma-cn-session/app/verify`，开关写 `Launcher.EnableLoginAuthTicket`；源码 `icon_hyperion.png`/`alert.png` 用字形代替）。卸载入口已按 `GameLauncherSettingDialog.xaml:222-246` 补上（卸载游戏 → Caution 警告行 → 强调「确认卸载」，两段式确认；守卫照抄源码：根目录/数据目录在游戏内/程序目录在游戏内一律拒绝，`Services/GameUninstaller.cs`；删除前把 `<游戏目录>/ScreenShot*` 的图片复制到 `~/Pictures/LunarWard`）。安装中悬停的「游戏信息 / 下载进度」卡片也已按 `StartGameButton.xaml:223-407` 的 `Popup_GameInfoOrDownloadProgress` 重做（260 内宽、Padding 20,12、STATUS+百分比+剩余时间、下载/安装/校验三段与错误文本；旧的 320 宽自造进度卡已删除）。未移植：安装对话框内的区服选择 |
| 素材 | Starward 自带 emoji/图标 PNG | 运行时从 HoYoPlay 拉 | 🟡 | 无 emoji 素材，空态用字形占位并在注释说明 |

## 2. 与 `07-UI-REPLICA.md` 的更正

| 07 原文 | Starward 源码事实 | 处理 |
|---|---|---|
| §4.4「公告卡无自动轮播（Starward 无 timer）」 | `GameBannerAndPost.xaml.cs` 有 5s `DispatcherQueueTimer`，悬停暂停、移出恢复 | 实现已按源码（VM `_bannerTimer`），改正 07 |
| §5「账号记录并入工具箱」 | `MainView.xaml` 有独立 `SelfQueryPage` 导航项 | 已恢复独立页（2026-10-25），工具箱不再放账号记录 |
| 截图页「左列表 + 右预览」 | `ScreenshotPage2.xaml` 是 44px 页头 + `ItemsView` 磁贴墙（204×115、间距 12） | 重写 |
| 抽卡页「整页虚拟化列表」 | `GachaLogPage.xaml` 是 44px 页头 + `SplitView`，记录收在 `GachaStatsCard` 的 5★/4★ Pivot 里 | 重写 |

## 3. 本轮改动（2026-10-25 夜，GameSelector 对齐）

- **游戏选择层数据模型重做**：新增 `ViewModels/Starward/GameChoiceViewModel.cs`
  的 `GameChoiceViewModel`（每款游戏一张卡）/ `GameServerItem`（区服）/
  `InstalledGameItem` / `GameChoiceBuilder`；`ShellViewModel.GameChoices` 改为
  per-game，`Servers` 承载国服官方 / B 站 / 国际服。旧档案 id 与 gamebiz 形式的
  `GameChoiceOrder` 读取时归一化到 kind（`NormalizeOrderKey`）。
- **Flyout 只在点击卡片时弹出**：`StarwardShell.axaml.cs`
  删除 `OnGameChoicePointerEntered/Exited` 的自动 `ShowAt/Hide`（闪烁根因），
  改为 `Tapped` 里 `PopupFlyoutBase.Placement=Bottom`、`ShowMode=Transient`；
  单区服卡片直接 `SelectServer`。
- **左上角图标条**：当前游戏图标 + 横排图标条从 `NavigationRail` 移到
  `StarwardShell.GameIconsHost`（同层，才能无缝滑出）；收起/展开用
  `RenderTransform translate(0,-100px) ↔ translate(0,0)` + 0.167s
  `TransformOperationsTransition`；命中区仅 60px 高，修掉旧的左边缘裁切竖条。
- **左栏排版**：`Button.rail` 的 `HorizontalContentAlignment` 改为 Stretch，
  40×40 内图标水平/垂直居中（此前贴左 8px）。
- 验证：`dotnet build -c Debug -warnaserror` 0 警告 0 错误；单测 584 通过
  （新增 `GameChoiceBuilderTests` 9 例：per-game 组装、中文名、角标、pin、
  current、order 兼容、单区服、`GameIconsPinned` 往返）；自检截图
  `/tmp/sel-launcher.png`、`/tmp/sel-selector.png`、`/tmp/sel-hover.png`、
  `/tmp/sel-icons.png`。

## 3.1 上一轮改动（2026-10-23 夜）

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
  战绩菜单 14 个子页全部已移植（原神 5 + 崩铁/绝区零 9）；「战绩」本身在 Linux 上用系统浏览器打开同名网页。
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
  新增 `--scroll-y <px>` 与 `--scroll-target <控件名>` 自检开关（截图前滚动指定容器；不给名字时退回「面积最大的 ScrollViewer」）。
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
   数据用 `GameRecordClient` 的对应 API。（账号记录独立页 ✅ 2026-10-25）
2. 启动器剩余控件：安装时的「详细信息/预定下载」弹窗、卸载入口（Linux 需先有卸载方案）。
   （`GameAccountSwitcher` ✅、拖放背景 ✅、安装/更新进度 ✅、预下载 ✅ 2026-10-25、修复 ✅ 2026-10-25 已补，见 §1 启动器行；
   源码当前 `InstallGameDialog.xaml` 并无「区服 ComboBox」，原队列项作废）
3. 设置子页逐项核对（截图/快捷键/文件管理/高级/下载）。
4. 安装/更新 UI、`ImageViewWindow2`。

## 5. 验收

- 每页改动都跑：`dotnet build` 0 警告 0 错误；`--screenshot` 1200×676 出图；
  `dotnet test` 全绿。
- 在本文 §1 更新状态与差距；新增"自创"必须当场改回源码结构，不得只改注释。

## 6. 已知测试不稳定（2026-10-25）

`tests/LunarWard.Installer.UnitTests` 里与下载/超时相关的用例偶发失败（观测到两次：Round 13、Round 35），单独重跑即通过，判定为**耗时敏感**而非逻辑错误。

处理建议（尚未实施）：把固定 `Task.Delay` 改成条件轮询 + 每个用例独立的临时目录，避免共享状态与真实计时器耦合。