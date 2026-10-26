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

- **区服 logo 已补齐（2026-10-26）**：`gameicon_hyperion.png` / `gameicon_hoyolab.png` /
  `gameicon_bilibili.png` 原本是「素材缺失」项，本轮改为**直接引用上游同一个 NuGet 内容包**
  `Starward.Assets` 0.6.5（`Directory.Packages.props`，版本与上游 `Starward.csproj:54` 对齐）。
  该包纯 `contentFiles`（166 个文件 / 11 MB，`copyToOutput=true`），restore 后落进输出目录
  `Assets/`，路径映射复刻上游 `GameBizIcon.GameBizToServerIcon`（见 `Services/GameAssets.cs`）。
  代价：输出目录多 11 MB，且每次上游升包版本都需要同步；图片缺失时角标退回「国/际/B」文字。
- - **截图页月份标注（2026-10-25，`f9f5622`）**：上游用 WinUI `AnnotatedScrollBar`
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

- **「游戏设置 → 游戏包体」的版本号语义（2026-10-26 回退）**：此前的"有意偏离"已撤销。
  上游 Pivot 标题写的就是 `LatestVersion = gamePackage.Main.Major.Version`
  （`Features/GameLauncher/GameLauncherSettingDialog.xaml.cs:997-998`，包体清单版本；
  预下载同理 1018-1021）。LunarWard 已改回同一口径：`TabItem.Header` 取
  `GetGamePackageAsync` 返回的 `main.Major.Version` / `pre_download.Major.Version`，
  删除了"游戏版本作标题 + 包体版本作副标题"的自造 `FormatManifestSubtitle` 与对应测试
  （`src/LunarWard/Views/Starward/GameLauncherSettingDialog.axaml(.cs)`）。
  这属于 D1 修订后要消灭的"主动 UX 偏离"，不再是差异项。

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
| 窗口圆角 8 + 1px `#1AFFFFFF` 描边（Win11 由 DWM 画） | 透明窗口 + 8px 圆角 `Border ClipToBounds` 自绘 | Linux 没有等价窗口管理器处理（无边框窗口就是直角）；KWin 支持 ARGB 窗口，视觉一致。`ActualTransparencyLevel == None` 时自动退回直角，避免四角发黑 |
| `gameBackground.Theme` 版本亮点海报（含 logo/标题文字） | 独立 `Image` 层 `UniformToFill` 叠在背景图/视频帧之上 | 上游静态图用 Win2D 先合成进位图、视频用 `VideoFrameAvailable` 逐帧混合；Avalonia 直接叠一层即可得到同样画面，省掉每帧 CPU 合成。本地自定义背景没有 theme，该层自动隐藏 |
| 公告 Banner `FlipView` 滚轮翻页 | 手写 `PointerWheelChanged` → 上一页/下一页 | Avalonia 无 `FlipView`；上游 FlipView 的触摸/滚轮切换用等价手势复刻 |

- **文字抗锯齿**：Windows 用 ClearType 子像素渲染，Avalonia 默认也是子像素，截图里能在
  「开始游戏」「图标字形」边缘看到 RGB 彩边。本轮在 `MainWindow.axaml` 用
  `TextOptions.TextRenderingMode="Antialias"` 强制灰度抗锯齿——这是**有意偏离**上游的
  唯一一处视觉差异，换来与 Windows 截图并排比对时不出现彩色毛边。

## 五、浏览器内核（WebView2 → WebKitGTK 4.1，2026-10-26，`task-2`）

上游 `Microsoft.Web.WebView2` 在 Linux 上没有二进制分发。**不采用「不做」**（`10-ALIGNMENT.md` §0
反面清单），用系统自带的 WebKitGTK 4.1 做等价实现：C# P/Invoke `libwebkit2gtk-4.1`（GTK3 API）
+ Avalonia `NativeControlHost`（X11 XID 嵌入）。**不用 CEF**（Chromium 运行时 100 MB+，与发行包体积不符）。

落点：`src/LunarWard/Frameworks/WebView/`
- `WebView.cs` — Avalonia 控件 `NativeControlHost` 子类，负责 X11 socket 窗口与 GTK 侧的生命周期
- `CoreWebView2Compat.cs` — 与 WebView2 同名的门面（`CoreWebView2` / `CoreWebView2Settings` /
  `CoreWebView2Profile` / `CoreWebView2CookieManager` / 各 `…EventArgs`），移植上游窗口时只需把
  `using Microsoft.Web.WebView2.*` 换成 `using LunarWard.Frameworks.WebView;`、`WebView2` 换成 `WebView`
- `WebKitWebViewCore.cs` — 原生层（信号、脚本、Cookie、截图、自定义 scheme）
- `GtkRuntime.cs` — 独立 GTK 线程 + 主循环（Avalonia 已占用主线程，GTK 不能抢 `gtk_main`）
- `VirtualHostMapping.cs` — `SetVirtualHostNameToFolderMapping` 的等价物（`appassets://` 自定义 scheme）
- `SelfTest/` — 可 `dotnet run` 的最小验证程序（只编译原生层，不依赖 Avalonia）
- `SelfTest.Avalonia/` — 把控件放进真实 Avalonia 窗口的嵌入自检（验证 `NativeControlHost` 这条链路）

| 上游（WebView2） | LunarWard（WebKitGTK 4.1） | 说明 / 已知差异 |
|---|---|---|
| `Source` / `Navigate(url)` | `webkit_web_view_load_uri` | 一致 |
| `NavigateToString(html)` | `webkit_web_view_load_html(html, baseUri)` | 一致；`BaseUri` 用于相对路径与同源判定 |
| `ExecuteScriptAsync(script)` | `webkit_web_view_evaluate_javascript` + `jsc_value_to_json` | 返回值语义与 WebView2 一致（字符串带引号、对象为 JSON）。**默认把脚本包进函数再 `eval` 执行**，因为上游 `BBSWebBridge` / `LoginPage` 会在每次 `DOMContentLoaded` 重跑含顶层 `let` 的脚本，直接求值第二次会抛 `Identifier … has already been declared`。CSP 禁 `unsafe-eval` 时自动退回直接求值 |
| `WebMessageReceived` / `window.chrome.webview.postMessage` | `WebKitUserContentManager` 的 `script-message-received::lunarward` + 文档开始时注入的 `window.chrome.webview` 垫片 | 上游 JS 源码不用改；`TryGetWebMessageAsString()` 按 JSON 首字符是否为 `"` 判定 |
| `DOMContentLoaded` 事件 | 无等价信号 → 注入脚本在 `DOMContentLoaded` 时向内部处理器回报 | 时序与上游一致（页面脚本之前注册） |
| `NavigationStarting` / `NavigationCompleted` | `load-changed` / `load-failed` | `NavigationStarting.Cancel` 只在 `decide-policy` 里生效，当前实现记录告警；上游 LoginPage 用它拦外链，需在移植该页时接到 `decide-policy` |
| `NewWindowRequested` | `create` 信号返回 NULL（阻止默认弹窗）+ 抛事件 | 目标 URL 交给调用方 `Launcher` 打开 |
| `DocumentTitleChanged` / `SourceChanged` / `HistoryChanged` | `notify::title` / `notify::uri` | 一致 |
| `WebResourceResponseReceived` | `resource-load-started` + `webkit_web_resource_get_data` | **只对 URI 含 `.json` 的资源抓取**（ZZZGachaInfo 只嗅探 JSON），不是全量镜像响应 |
| `CoreWebView2Environment.GetAvailableBrowserVersionString()` | `webkit_get_major_version/get_minor_version/get_micro_version` | WelcomeWindow 的「WebView2 组件初始化失败」判定改读这里 |
| `CookieManager`（Get/Add/Delete/DeleteAll） | `webkit_cookie_manager_*` + `libsoup-3.0` `SoupCookie` | 一致；上游 `CreateCookie(name, value, domain, path)` 全参数保留 |
| `SetVirtualHostNameToFolderMapping(host, folder, access)` | 自定义 scheme `appassets://<host>/<path>` + 主框架 URL 重写 | **只重写主框架导航**；页面里硬编码的 `https://<host>/…` 子资源不会被改写（上游当前无调用点，移植时需注意） |
| `Profile.PreferredColorScheme` | GTK 设置 `gtk-application-prefer-dark-theme` | WebKitGTK 4.1 没有 `prefers-color-scheme` 的 API，只能从 GTK 主题侧影响页面 |
| `CallDevToolsProtocolMethodAsync` | 只等价实现 `Page.captureScreenshot`（走 `webkit_web_view_get_snapshot`） | 其它方法显式抛 `PlatformNotSupportedException`；WebKit 的 inspector 需 `WEBKIT_INSPECTOR_SERVER`，不接 CDP |
| `OpenDevToolsWindow` | 仅置 `webkit_settings_set_enable_developer_extras` 并告警 | 上游只用于调试 |

**平台嵌入与 Wayland 降级**

- X11（含 XWayland）：`NativeControlHost.CreateNativeControlCore` 里 `XCreateWindow` 建 socket 子窗口，
  交给 `gtk_plug_new(xid)` 做 XEmbed。GTK 后端必须在 `gtk_init` 前定好，所以嵌入路径会通过
  `setenv("GDK_BACKEND","x11")` 强制 X11（`LUNARWARD_WEBVIEW_GDK_BACKEND` 可覆盖）。
  注意 `.NET` 的 `Environment.SetEnvironmentVariable` 不足以让 GDK 读到，必须用 libc `setenv`。
- Wayland 原生：没有 XID 可嵌，`IsEmbedded=false`，自动降级为**独立 GTK 窗口**（同一个
  `GtkWindow` + 同一个 webview），日志与 `StatusMessage` 写明原因，不会静默黑屏。
- **Avalonia 12 的 X11 平台句柄描述符是 `XID`**（不是 `X11` / `XWindow`）；三者都要认，否则会被
  自己的判断误判成「非 X11 平台」而白白降级。
- **`CreateNativeControlCore` 不能返回 `null`**：Avalonia 12 在 `X11NativeControlHost.Attachment`
  的构造函数里直接解引用，会 `NullReferenceException` 崩在布局阶段。降级时返回一个句柄为 0 的占位
  `PlatformHandle`，真正的画面放在独立 GTK 窗口里。
- Avalonia 的 X11 后端在没有合成器/GBM 的环境会打印 `Failed to create GBM buffer of size WxH：
  无效的参数`——这是 Avalonia 自己的回退路径，不影响 WebView 内容（截图正常）。
- 尺寸：`GtkPlug` 是 `GtkWindow` 子类，尺寸默认由宿主 socket 的 XEMBED 协商决定；Avalonia 的
  XID 不会发协商消息，所以必须自己 `gtk_widget_size_allocate`，否则分配结果停在 1×1
  （实测：不调用时快照 272×1267，调用后 900×600）。

**运行证据**（真实执行，`artifacts/webview-selftest/`）

- `dotnet run --project src/LunarWard/Frameworks/WebView/SelfTest` → 10 项 PASS：导航完成、
  `DOMContentLoaded` 注入回报、JS→C# 消息（对象 + 纯字符串两种）、`ExecuteScriptAsync` 三种返回、
  顶层 `let` 脚本连跑两次、视口 900×600、截图合法、Cookie 写入+读回。
- `--xembed`：自建 X11 socket（模拟 Avalonia 的 XID），`GtkPlug 嵌入：socket=0x4000001
  plug=0x3E0000C embedded=True`，11 项 PASS（多一项 XEmbed 校验）。
- `--wayland`：`GDK 后端 = wayland`，`IsEmbedded=False` 降级独立窗口，同样全 PASS。
- `dotnet run --project src/LunarWard/Frameworks/WebView/SelfTest.Avalonia` → 真实 Avalonia 窗口里
  9 项 PASS：依赖可用、`Avalonia NativeControlHost 走 XID 嵌入（非降级）`、
  JS→C# 消息 `{"ok":true,"host":"avalonia"}`、`ExecuteScriptAsync(document.title)` = `"AVALONIA-EMBED"`、
  读到页面脚本写回的 DOM `"JS 已执行：42"`、`SetVirtualHostNameToFolderMapping` 等价物
  （`https://probe.local/` → `appassets://probe.local/index.html`，页面标题 `"APPMAPPED"`）、
  `ZoomFactor=1.5` 生效（`devicePixelRatio="1.875"`，宿主 1.25× 缩放 × 1.5）、截图 960×640；
  日志 `avalonia-selftest.log`。
- 截图：`avalonia-embed.png`（960×640，Avalonia 窗口内）、`webview-selftest-xembed.png` /
  `-x11.png` / `-wayland.png`（Wayland 下 2× 缩放 1800×1200）；
  日志：`run-x11-standalone.log`、`run-wayland.log`、`selftest.log`。

**依赖与安装**

`pkg-config --modversion webkit2gtk-4.1 gtk+-3.0` 必须能查到。缺失时的安装命令由程序自己打印
（`WebViewAvailability.InstallHint`）：Arch `sudo pacman -S webkit2gtk-4.1 gtk3 libsoup3`；
Debian/Ubuntu `sudo apt install libwebkit2gtk-4.1-0 libgtk-3-0 libsoup-3.0-0`；Fedora
`sudo dnf install webkit2gtk4.1 gtk3 libsoup3`。控件在缺库时不抛异常，`IsWebViewAvailable`
为 false 且 `MissingDependenciesMessage` 给出来源，页面照上游显示「组件初始化失败」状态。

**尚未覆盖（移植对应窗口时要补）**

1. `NavigationStarting.Cancel` 的真实拦截（需接 `decide-policy`）——LoginPage 拦站外跳转要用。
2. `WebResourceResponseReceived` 全量（现在只抓 `.json`）——ZZZGachaInfo 若以后嗅探非 JSON 需扩展。
3. 自定义 scheme 的子资源改写（现在只重写主框架 URL）。
4. `SetVirtualHostNameToFolderMapping` 的 `DenyCors` 只能退化成「不允许跨源」，粒度不如 WebView2。
5. DevTools Protocol 除截图外一律不支持；上游 `BBSWebBridge` 目前只用 `Page.captureScreenshot`。

## 六、Helpers 层（2026-10-26，Lead）

上游 `src/Starward/Helpers/` 16 个文件，逐文件落点与替换点（`OK` = 内容一字未改，仅命名空间）：

| 上游文件 | 本仓库 | 替换点 |
|---|---|---|
| `BindConverter.cs` | `Helpers/BindConverter.cs` | 返回 `bool` 而非 `Visibility`（Avalonia 用 `IsVisible`） |
| `ClipboardHelper.cs` | `Helpers/ClipboardHelper.cs` | WinRT `DataPackage`/`IRandomAccessStream` → `IClipboard` + `ClipboardExtensions.SetBitmapAsync(Bitmap)`；图片以 PNG 位图写入，不再有 `SetStorageItems`（Avalonia 无文件列表剪贴板） |
| `DriveHelper.cs` | `Helpers/DriveHelper.cs` | `DriveInfo` 取容量/文件系统；「可移动或在 USB 上」改为 `/proc/self/mountinfo` 溯源 + `/sys/block/<dev>/removable` |
| `FileHashHelper.cs` | `Helpers/FileHashHelper.cs` | OK |
| `FileCache.cs` | `Helpers/FileCache.cs` | OK（FusionCache） |
| `FileDialogHelper.cs` | `Helpers/FileDialogHelper.cs` | `TopLevel.StorageProvider`（上游 `CommonFileDialog`） |
| `FileDialogNative.cs` | — | **不移植**：Win32 `IFileOpenDialog`/`IShellItem` COM 细节，StorageProvider 已覆盖同等能力 |
| `Enumeration/` | `Helpers/Enumeration/` | OK |
| `InAppToast.cs` | `Helpers/InAppToast.cs` | 自绘 StackPanel 形态（上游 `InfoBar` + `TeachingTip` 的等价物），`ToastItem` 同名 |
| `PointerCursor.cs` | `Helpers/PointerCursor.cs` | `InputSystemCursor` → `StandardCursorType`；不再需要 `UnsafeAccessor` 写受保护属性 |
| `AccentColorHelper.cs` | `Helpers/AccentColorHelper.cs` | `WinRT Color`/`HsvColor` → `Avalonia.Media`（自实现 ToHsv/FromHsv）；删 `IBuffer` + `IBufferByteAccess` 重载，改 `Bitmap`/`Stream`/文件入口 |
| `XamlRootExtension.cs` | `Helpers/XamlRootExtension.cs` | `AppWindow.TitleBar.SetDragRectangles` → 空实现（X11 无 drag rectangle）；`RasterizationScale` → `RenderScaling`；`GetWindowHandle` → X11 XID |
| `TransparentBackdrop.cs` | `Helpers/TransparentBackdrop.cs` | `SystemBackdrop` + DWM blur → `WindowTransparencyLevel.Transparent` + 透明背景；合成器不支持时告警并退化不透明 |
| `SystemUIHelper.cs` | `Helpers/SystemUIHelper.cs` | 上游已 `Obsolete(true)` 死代码；保留形状、值恒 `true` |
| `SystemBackdropHelper.cs` | 待移植（`Features/ViewHost/SystemTrayWindow` 移植时一并落） | Mica/Acrylic → `WindowTransparencyLevel.AcrylicBlur`，X11 上退化为纯色 |
| `WhiteSpaceStringToVisibilityConverter.cs` | `Converters/WhiteSpaceStringToVisibilityConverter.cs` | 返回 `bool` |

本项目级的其它约定：

- `src/LunarWard/LunarWard.csproj` 开启 `AllowUnsafeBlocks`（上游同款，`AccentColorHelper` 用指针取像素）。
- 成员名与上游保持一致（含 `AppConfig.StarwardExecutePath` 这类带产品名的成员）；**命名空间/程序集/用户可见字符串**才改名为 LunarWard。

## 七、Features/Game*（launcher 系）移植（2026-10-26，launcher-port）

上游 `src/Starward/Features/{GameLauncher,GameAccount,GameSelector,GameSetting,GameInstall}/`
共 53 个文件。落点规则与 `docs/12-XAML-PORT-GUIDE.md` 一致：一上游文件=一本仓库文件，
类名/成员名/事件名不改，仅 `Starward.*`→`LunarWard.*`。**下列逐条为本节新增的平台替换点**，
每条都给出「位置 / 上游行为 / 本仓库行为 / 原因 / 待办」。

### 7.1 服务层（Wine prefix 取代「当前游戏注册表」）

| # | 位置 | 上游行为 | 本仓库行为 | 原因 | 待办 |
|---|---|---|---|---|---|
| 1 | `Features/GameLauncher/GamePrefixContext.cs`（**新增文件，上游无**） | 注册表 `HKEY_CURRENT_USER\Software\miHoYo\*` 天然按游戏全局唯一 | 用 `GamePrefixContext.CurrentPrefix` / `CurrentWineBinary` 两个静态字段表示「当前游戏所在 prefix」，服务层读写注册表都经它 | Linux 下每个游戏实例是独立 Wine prefix，没有全局注册表；本文件是全集群唯一新增的跨文件状态 | 待 Lead 的导航外壳在切换游戏时赋值；后续可考虑改为随页面上下文传递 |
| 2 | `Features/GameAccount/GameAccountService.cs` | ctor(ILogger)，注册表直读 | ctor(ILogger, string? prefix = null) + `WineAccountService(prefix ?? GamePrefixContext.CurrentPrefix)`；SQL 逐字保留（`GameAccount` 表、INSERT OR REPLACE、DELETE WHERE SHA256） | 无 DI 注册时可无参构造；注册表访问必须带 prefix | DI 侧由 `AppConfig.ServiceProvider` 注册，prefix 运行时由 #1 提供 |
| 3 | `Features/GameLauncher/GameNoticeService.cs` | 注册表读 `__LastUid___*` / `App_LastUserID_*` / `GENERAL_DATA_V2_LastLoginUserId_*` | `WineAccountService(prefix).Read(gameBiz.Value)?.Uid` | 同上 | 上游走 `GameRecordService.GetLastSelectGameRecordRoleOrTheFirstOne`；本地 GameRecord 未落地，暂直查 `GameRecordRole`+`Setting` 两张表（SQL 同上游），待 task-5 完成后切回服务 |
| 4 | `Features/GameLauncher/GameAuthLoginService.cs` | `AppConfig.GetService<GameRecordService>().UpdateDeviceFpAsync(false, ct)` | 私有 static `UpdateDeviceFpAsync`：`new HyperionClient()` + `AppConfig.HyperionDeviceId/Fp/FpLastUpdateTime` 复刻「3 天过期」；其余（stoken/mid 校验、passport-api 请求、x-rpc-* 头、回写 stoken）逐字 | GameRecordService 属他不属我；HyperionClient 已在 Core | task-5 落地后改回服务调用 |
| 5 | `Features/GameLauncher/GameLauncherService.cs` | ctor(ILogger, HoYoPlayService, PlayTimeRecordService, GameAuthLoginService) | ctor 去掉 `PlayTimeRecordService`（时长统计由本地 `Launch.PlayTimeService` 在启动外壳里记，避免重复计数） | 无 PlayTimeRecordService 等价物 | 若后续 GameRecord 提供记录接口，可恢复四参构造 |
| 6 | 同上 `GetGameProcessAsync` | `Process.GetProcessesByName` + `SessionId==WTSGetActiveConsoleSessionId` + `IsProcessPending`（线程挂起探测） | `Process.GetProcesses()` 按 ProcessName 匹配；`IsProcessPending` 读 `/proc/<pid>/stat` 第 3 字段是否为 `T` | Linux 无 Session/WTS；挂起态在 proc 里就是 state=T | 多用户/容器场景可再细化为 `XDG_SESSION_ID` 过滤 |
| 7 | 同上 `StartGameAsync` | `ProcessStartInfo{ UseShellExecute=true, Verb="runas" }`；`AppConfig.StartGameWithCMD` 时拼 `cmd /c start`；`-use-d3d12`/`-popupwindow`/`thirdPartyTool` | `BuildStartInfo`：`.exe` → `wine` + `ArgumentList` + `WINEPREFIX=GamePrefixContext.CurrentPrefix`；原生可执行直接起；忽略 `StartGameWithCMD`（无 `cmd /c start` 等价物）；其余参数串（runas/thirdPartyTool/popupwindow/-use-d3d12/StartArgument/profile）逐字拼 | 无 UAC/runas，启动链是 Wine；WinUI 启动参数语义保留 | `Verb=runas` 在 Linux 无对应，已静默忽略（本仓库以 prefix 归属代替提权） |
| 8 | 同上 `GetRelativePathIfInRemovableStorage` / `GetFullPathIfRelativePath` | `DriveHelper.IsDeviceRemovableOrOnUSB` + `AppConfig.StarwardExecutePath/ConfigPath` | 逐字，仅在 `DriveHelper` 侧换成 Linux 实现（见 §六） | 上游同一 helper | — |
| 9 | `Features/HoYoPlay/HoYoPlayService.cs`（Lead 批准移植） | `SetupAPI` 枚举显卡（`GetGPUInfos`） | 枚举 `/sys/class/drm/card[0-9]*`：`device/product_name`、`uevent` 的 `DRIVER=`、`/sys/module/<driver>/version`，驱动名兜底映射成 AMD/Intel/NVIDIA | 无 SetupAPI/注册表显卡信息 | 若拿到 DXVK/Vulkan 信息可补 `driverVersion` |
| 10 | `Features/GameSetting/GameSettingService.cs` | 全部 static 同步方法，`Registry.GetValue/SetValue` | 同常量/同 JSON 结构/同 Clamp，方法一律 `async` + `Async` 后缀，注册表走 `WineRegistry`（`UnixProcessLauncher.Default` + `AppServices.Current.Log`，超时 20s） | Wine 注册表只能异步跑 `wine reg` 子进程 | 调用点已全部改成 `await`；后续可缓存 |
| 11 | `Features/GameLauncher/GameConfigIni.cs` | `Features/GameLauncher/GameConfigIni.cs` | 已在 `LunarWard.Games/GameConfigIni.cs`（内容与上游一致，另有 Ini 读写器） | 该类型早先被 Games 层占用 | 待将该文件并入 `Features/GameLauncher/`（需 Lead 统一搬迁，避免双份） |

### 7.2 UI 层（WinUI 控件 → Avalonia 控件）

| # | 位置 | 上游 | 本仓库 | 原因/待办 |
|---|---|---|---|---|
| 12 | 全部 `.xaml`→`.axaml` | `ContentDialog` | `Views/Starward/Controls/StarwardContentDialog` + `DialogService.ShowAsync(dialog, host)`，结果用 `PrimaryButtonClicked` 事件 | Avalonia 无 ContentDialog |
| 13 | 全部 | `XamlRoot`（设置 DragRect、取窗口 handle、`ShowAsync`） | 去掉；`WeakReferenceMessenger.Send(new MainWindowDragRectAdaptToGameIconMessage(...))` 保留给外壳处理 | 无 XamlRoot 概念 |
| 14 | `GameAccountSwitcher` | `AutoSuggestBox`（`InputScope=Number` + `BeforeTextChanging`） | `AutoCompleteBox` + `GotFocus` 填 `ItemsSource`（`GetSuggestionUids`）+ `TextInput` 里拦非数字 | 无 InputScope；上游手写的 `UpdateSuggestionUids` 删除（AutoCompleteBox 自带过滤） |
| 15 | `GameAccountSwitcher` | `FontIcon Glyph=E74E/E8FB/E70F/E74D` | `TextBlock Classes="icon" Text="&#xE74E;"` | Avalonia 无 FontIcon 控件，`Styles/Starward.axaml` 已备 `TextBlock.icon` |
| 16 | `GameAuthLoginButton` | `ToggleSwitch.IsOn`、`ThemeResource DateTimePickerFlyoutButtonStyle` | `ToggleSwitch.IsChecked`；样式去掉 | 控件/资源名不同 |
| 17 | `DX12IntroDialog` | `ContentDialog` + `CachedImage` | `StarwardContentDialog` + `Image`（`TODO(task-4)`：`Controls/CachedImage` 落地后换回） | `CachedImage` 归 task-4，本轮不用 Controls/ |
| 18 | 同上 | `[RelayCommand] Close()` 调 `Hide()` | `CloseDialog()` 调 `Close()` | `Close` 与基类 `StarwardContentDialog.Close` 同名会递归（CS0108） |
| 19 | `GameNoticeWindow` | `sf:WindowEx` + WebView2 + `ProgressRing` + `ContentDialog` 无 | `Frameworks.WindowEx` + `Frameworks.WebView`（WebKitGTK）+ `ProgressBar IsIndeterminate` | WebView2→WebKitGTK 见 §五；ProgressRing 无对应控件 |
| 20 | 同上 | `Activate()` 按 `ParentWindowHandle` 把窗口贴到父窗口右侧并算尺寸；`User32.SetForegroundWindow` 回焦 | `Activate()` = `Show()`，尺寸由调用方给；回焦用 `Window.Activate()` | 无跨窗口 handle 映射、无 User32 |
| 21 | 同上 | `webview.DefaultBackgroundColor = Transparent` + `BrushForThemeBackgroundColor` | 省略（WebKitGTK 透明由窗口透明级别处理） | API 缺失 |
| 22 | `GameBannerAndPost` | `FlipView` / `Pivot` / `ItemsRepeater`+`StackLayout` / `DispatcherQueueTimer` / `Hyperlink` | `Carousel` / `TabControl`（`ItemTemplate`=页签头、`ContentTemplate`=内容）/ `ItemsControl`+`StackPanel` / `DispatcherTimer` / `TextBlock`+`PointerPressed` 调 `TopLevel.Launcher` | Avalonia 无上述 WinUI 控件；PipsPager 文案新增 `CurrentBannerIndexText` 属性 |
| 23 | `GameSettingPage` | `NumberBox` / `InfoBar` / `DisplayArea` / `DisplayInformation`（HDR 探测） | `NumericUpDown` / `Border`+文字 / `TopLevel.Screens.Primary` / **HDR 一律「不支持」**（不显示警示条） | Linux 无 WinRT 高级颜色 API；`Button.accent` 代替 `AccentButtonStyle` |
| 24 | 同上 | `ComboBox.Items` 里 `AddedItems.FirstOrDefault()` | `AddedItems.Cast<object>().FirstOrDefault()` | Avalonia `AddedItems` 是非泛型 `IList` |
| 25 | `GenshinHDRLuminanceSettingDialog` | Win2D `CanvasSwapChainPanel`（R16G16B16A16Float swapchain + `LinearTransferEffect` 线性增益，`.jxr` HDR 贴图） | 摩拉预览用 `Image(Sign.png)` + `MoraPreviewOpacity`；场景区为占位 Border（`.jxr` 无法解码）；`HDREnabled` 恒 false（自动亮度按钮禁用） | Avalonia 无 Win2D/HDR swapchain，Skia 不解 `.jxr` | HDR 与 Wine/DXVK 同属本轮不做范围；后续可用 Skia 色彩矩阵近似 |
| 26 | 全部 | `x:Bind` / `x:DefaultBindMode` / `ThemeResource` | `{Binding}`（`Mode=TwoWay` 显式）/ `DynamicResource`（`StarTextSecondary`、`StarOverlayAcrylicBrush`、`StarCardBackgroundBrush`、`StarCautionBrush`） | 语法不同；资源键见 `Styles/Starward.axaml` |
| 27 | 全部 | `Visibility` 绑定（`ObjectToVisibilityConverter`、`BoolToVisibilityReversedConverter`） | `IsVisible` + `BoolReversed`/`ObjectToBool` 转换器，或 `{Binding !X}` | Avalonia `IsVisible` 直接吃 bool |

### 7.3 未移植 / 待后续（同节内追加）

| 上游文件 | 状态 | 原因 | 待办 |
|---|---|---|---|
| `Features/GameInstall/GameInstallService.cs` | **不移植** | 上游是 gRPC **客户端**（`RpcService` + `GameInstaller.GameInstallerClient` + `Starward.RPC.GameInstall.*` 契约 + 独立安装进程），本仓库 RPC 层不在范围内，安装逻辑已由 `LunarWard.Installer`（进程内 `GameInstallService`/`GameInstallController`，同名字段 `GameInstallContext`）承载 | 若将来引入 RPC 层，按上游文件原样落位；否则把 `LunarWard.Installer` 的 `GameInstallService` 视作等价实现 |
| `Features/GameInstall/GameInstallTaskStartedMessage.cs` | 已移植 | `InstallTask` 类型改指 `LunarWard.Installer.GameInstallContext`（字段集合与 RPC DTO 相同） | — |
| `Features/GameInstall/{InstallGameDialog,PreDownloadButton,PreDownloadDialog}` | 待移植 | 依赖上面的 RPC 任务模型与既有 `Views/Starward/` 旧实现（本轮不删旧壳） | 待安装层改造后逐行移植 |
| `Features/GameSelector/NoAnimationImageExStyle.xaml` | **不移植** | 目标类型 `Scighost.WinUI.ImageEx` 是 WinUI 专有控件（占位图/淡入状态机）；Avalonia 侧对应物是 `Image`/`Controls/CachedImage`（task-4） | CachedImage 落地后可用 Avalonia `Transitions` 复刻淡入效果，再决定是否提供同名样式 |
| `Features/GameSelector/GameSelector.xaml(.cs)` | 待移植 | 37KB+28KB，含 Win32 注册表、`GetFileInformationByHandleEx` 硬链接体积统计、XamlRoot 拖拽区、`FlyoutBase` 附加弹出层 | 需先把 `AutoSearchInstalledGames` 的注册表读取换成 `WineRegistry`、硬链接识别换成 `(st_dev, st_ino)` |
| `Features/GameLauncher/{GameLauncherPage.xaml(.cs),GameLauncherSettingDialog.xaml(.cs),CompactCheckBoxStyle.xaml}` | 待移植 | 本轮已有 `Views/Starward/` 旧实现（Lead 之后统一切壳）；`CompactCheckBoxStyle.xaml` 是 WinUI CheckBox 控件模板，需按 Avalonia 主题重画 | 切壳时逐行对齐；模板类样式单独评审 |

## 八、Controls + Features/Screenshot 移植（2026-10-26，dapper-db/task-4）

上游 `src/Starward/Controls/`（5 文件）与 `src/Starward/Features/Screenshot/`（23 文件）。
落点/命名规则同 `docs/12-XAML-PORT-GUIDE.md`：`Starward.*`→`LunarWard.*`、`.xaml`→`.axaml`、类名成员名不变。
本节逐条记录**被替换的 WinUI/Windows 专有实现**，以及**尚未落地的文件**（后者是明确缺口，不是「已完成」）。

### 8.1 Controls

| # | 位置 | 上游 | 本仓库 | 原因/待办 |
|---|---|---|---|---|
| 1 | `Controls/CachedImage.cs` | `ImageEx`（Scighost.WinUI）+ `ImageSource`/`BitmapImage`/`ImageThumbnail` 缩略图 | `public class CachedImage : Image`：`UriSource`(string) StyledProperty + `ProvideCachedResourceAsync(Uri, CancellationToken)`；`avares://`/`ms-appx:///` → `AssetLoader`；`file://` → `Bitmap`；网络图 → `Helpers/FileCache.GetFromCacheAsync` | Avalonia 无 `ImageEx`（缓存/占位/进度），无 WinRT `BitmapImage`；`IsThumbnail` 的缩略图解码待 `Features/Codec/ImageThumbnail`（task-9）落地后接回，当前直接加载原图 |
| 2 | `Controls/ColorRectChart.xaml(.cs)` | UserControl + Composition 动画裁剪 | **保留本地等价实现** `Controls/ColorRectChart.cs`（`readonly record struct ChartLegend(string? Legend, int Percent, IBrush Color)` + `Border` + 硬边 `LinearGradientBrush`，空/零值 `#60000000`） | Avalonia 无 Composition/`Rectangle` 裁剪动画；图表语义（硬边分段渐变）逐字保留，仅渲染方式换成 `Border.Background` |
| 3 | `Controls/ColorfulTextBlock.xaml(.cs)` | UserControl 包 `TextBlock`，`x:Bind` 透传全部文本属性 | 同名 `UserControl` + `TextBlock Name="ThisTextBlock"`，ctor 里把对齐/字体/前景/换行绑到自身；`Text` 的 `<cAARRGGBB>`/`<i>` 内联解析逐字保留 | `x:Bind` 无对应；上游 `IsTextSelectionEnabled="{x:Bind IsTextScaleFactorEnabled}"` 是上游笔误，未照抄 |

### 8.2 数据/支持层

| # | 位置 | 上游 | 本仓库 | 原因/待办 |
|---|---|---|---|---|
| 4 | `Features/Screenshot/ScreenshotItem.cs` | `BitmapImage` + `Windows.Storage` 属性 + WIC 取像素尺寸 | 保留 `TryParseCreationTime`/正则/`GetFileInfo`；`UpdatePixelSize` 改用 `Avalonia.Media.Imaging.Bitmap(stream).PixelSize` | 无 WIC/`StorageFile` |
| 5 | `Features/Screenshot/ScreenshotHelper.cs` | `Kernel32.CreateFile2` 独占打开探测文件占用；`avifdec.exe`/`djxl.exe`（随包 exe） | 占用判定改「能读 + 大小连续两次相同」轮询 30×100ms；avif/jxl 走 PATH 上的 `avifdec`/`djxl`，再退化 `ffmpeg -q:v 2`、`magick/convert -quality 90`，全缺则 `PlatformNotSupportedException` | Linux 无 `CreateFile2`、无随包 `.exe`；转换工具由发行版提供 |
| 6 | `Features/Screenshot/ScreenCaptureItem.cs` | Win2D `CanvasBitmap` + ICC `ColorPrimaries` + HDR 字段 | `Bitmap Bitmap` + `HDR`/`MaxCLL`/`SdrWhiteLevel`/`FrameTime` 字段（HDR 值仅承载，不做色彩管线） | Avalonia/Skia 无 Win2D swapchain 与 ICC 查询 |
| 7 | `Features/Screenshot/ScrollViewerExtensions.cs` | `ArePointerWheelEventsIgnored` 附加属性（XAML 用法不变） | Avalonia 附加属性 + `AddHandler(PointerWheelChangedEvent, Tunnel, handledEventsToo: true)` 后 `e.Handled = true` | 事件路由机制不同 |
| 8 | `Features/Screenshot/ZoomFactorPercentConverter.cs` | `IValueConverter` → `"P0"` | 同签名同输出（`Avalonia.Data.Converters.IValueConverter`） | 仅接口命名空间不同 |
| 14 | `Features/Screenshot/ScreenCaptureHelper.cs` | `Windows.Graphics.Capture`：`GraphicsCaptureItem` + `Direct3D11CaptureFramePool.CreateFreeThreaded` + `GraphicsCaptureSession` + `[GeneratedComInterface] IGraphicsCaptureItemInterop.CreateForWindow/CreateForMonitor`；WinRT `ApiInformation` 能力探测 | `internal static class ScreenCaptureHelper`：X11 用 `xdotool search --pid <pid> [--onlyvisible]` 定位 window id → `import -window <id> png:-`，无 import 时退 `xwd -silent -id` + `convert xwd:- png:-`；Wayland 用 `grim -`（区域 `grim -g "x,y wxh"`），按窗口抓取退化整屏；工具缺失抛 `PlatformNotSupportedException`（附缺失工具名），不静默出黑图。新增 `SupportsFullScreenFallback` 供上层判定是否允许整屏退化 | Linux 无 Windows.Graphics.Capture / WinRT COM / HMONITOR；外部工具调用统一 `ProcessStartInfo` + `ArgumentList`（`CreateNoWindow`，stdout 取 PNG 字节）；抓帧结果为 Avalonia `Bitmap` |
| 15 | `Features/Screenshot/ScreenCaptureContext.cs` | `GraphicsCaptureItem CaptureItem` + `Direct3D11CaptureFramePool? FramePool` + `GraphicsCaptureSession? CaptureSession` + `Task<Direct3D11CaptureFrame> CaptureAsync(DirectXPixelFormat, CancellationToken)` + `RecreateResource()` | 保留类名/`WindowHandle`/`CaptureWindowClosed` 事件/`CaptureAsync(CancellationToken)`/`RecreateResource()`/`IDisposable` 形状；`CaptureAsync` 每次调用外部工具重新取一帧（无帧池）；抓取异常时触发 `CaptureWindowClosed` | 无零拷贝帧池；`DirectXPixelFormat` 参数（SDR/HDR 像素格式）随 HDR 管线一并移除 |
| 16 | `Features/Screenshot/ScreenCaptureService.cs` | Win2D `CanvasBitmap`/`CanvasRenderTarget`/`ScaleEffect`/`ColorMatrixEffect`/`GammaTransferEffect`/`HistogramEffect`；`DisplayInformation`/`DisplayAdvancedColorInfo`/`ColorPrimaries`（ICC 解析）；`ImageSaver.SaveAsPng/Avif/Jxl/UhdrAsync`（含 ICC + XMP 嵌入）；`StorageFile` + `ClipboardHelper.SetStorageItems`；`DwmGetWindowAttribute`（客户区裁剪） | 保留 `Capture()`/`CaptureInternal()`/`CaptureAndProceedImageAsync`/`SaveImageAsync`/`BuildXMPMetadata(DateTimeOffset)`/`CopyToClipboardAsync(string?)`/`GetMaxCLL(Bitmap)`/`OnCaptureWindowClosed` 与截图目录、文件名 `{ProcessName}_{FrameTime:yyyyMMdd_HHmmssff}`、`ScreenCaptureSavedFormat` 0/1/2、`ScreenCaptureEncodeQuality` 映射；`GetMaxCLL` 按上游直方图算法（缩放≤2048/0.5、`(0.2126R+0.7152G+0.0722B)/125`、开方、500 bin、从高到低累计 ≥0.0001）在 `Bitmap` 像素上重算；PNG 用 Avalonia `Bitmap.Save`，AVIF/JXL 调 `avifenc`/`cjxl`（缺失回退 PNG 并记日志）；剪贴板仅 PNG/JPEG 走 `ClipboardHelper.SetBitmap(string)` | 无 Win2D/HDR/ICC：`HDR` 恒 false、`MaxCLL=-1`、`SdrWhiteLevel=0`、`SaveAsSdrAsync` 恒 null（HDR 元数据「只写不实采」）；`GetDisplayInformationFromWindowHandle` 与 `TryClipClient` 无 Windows 等价物——后者改为 `TryClipClient(nint, out int, out int, out int, out int)` 恒 false（`import -window` 本身只含窗口内容）；XMP 不再嵌入图像（`BuildXMPMetadata` 作为纯函数保留给 `ImageBatchConvertWindow` 使用）；`ScreenCaptureInfoWindow` 未移植，`Capture()` 的成功/失败反馈改为 `InAppToast` |

### 8.3 UI 层

| # | 位置 | 上游 | 本仓库 | 原因/待办 |
|---|---|---|---|---|
| 9 | `Features/Screenshot/ScreenshotFolderManageDialog.axaml(.cs)` | `ContentDialog` + `ListView` + `Launcher.LaunchFolderPathAsync` + `FileOpenPicker` | `StarwardContentDialog`（`DialogService.ShowAsync`）+ `ListBox` + `xdg-open` + `FileDialogHelper.PickFolderAsync`（StorageProvider）；`MenuFlyoutSeparator`→1px `Border`，`ProgressRing`→`ProgressBar IsIndeterminate`，模板内按钮提示用 `ContainerPrepared` + `ToolTip.SetTip` | Avalonia 无 ContentDialog/ListView 模板差异/WinRT Launcher |
| 10 | `Features/Screenshot/ScreenshotPage2.axaml(.cs)` | `ItemsView` + `UniformGridLayout` + `AnnotatedScrollBar`（含标签轨道/`ScrollController`/`PART_ToolTipRail` tooltip）+ Storyboard + `mxi:Interaction.Behaviors` | `ScrollViewer` + `ItemsControl` + `WrapPanel`（条目固定 204×115、间距 12）+ 原生 `ScrollBar`；悬停动画改 `Style :pointerover` + `Transitions`；保留「内容超过 3 屏才显示滚动条」判定；`DispatcherQueue.TryEnqueue`→`Dispatcher.UIThread.Post`；`DataPackage` 拖拽→`DataObject`+`DragDrop.DoDragDrop`；复制图片→`Clipboard.SetFilesAsync` | Avalonia 无 ItemsView/AnnotatedScrollBar/Storyboard/Behaviors；**年/月标签绘制省略**（滚动条保留），后续若要标签可自绘轨道 |
| 11 | `Features/Screenshot/ImageViewWindow2.axaml(.cs)` | `CanvasSwapChainPanel` + Win2D（HDR→SDR、显示器峰值亮度）、视频/GIF 播放、图库条、JxlSharp 导出、裁剪/EXIF 编辑 | 保留顶栏命令（复制/另存为/文件夹/删除/信息）、滚轮锚点缩放、按住拖动、双击还原、←/→ 翻页、删除后跳转并同步集合；渲染用 `Image` + `ScaleTransform`/`TranslateTransform` | Win2D/视频/GIF/图库条/编码导出本轮未落；**这是 1572 行上游文件的子集移植**，缺口登记如下 |
| 12 | `Features/Screenshot/ScreenshotPage.axaml(.cs)` | 分组 `GridView` + `GroupStyle`（sticky headers）+ `GridView.SelectionMode` None/Multiple 切换 + `DragItemsStarting` + `MenuFlyout` + `StorageFile`/`DataPackage` + `ClipboardHelper.SetStorageItems`/`SetBitmap` | 双层 `ItemsControl`（外层分组配 `WrapPanel`，条目 218×128 / 图 206×116，无吸顶）+ 自维护选中集合（条目 `selected` 类）+ `PointerPressed/Moved` 起拖 + `ContextFlyout`+`MenuItem` + `StorageProvider`/`DataObject(DataFormats.Files)` + `Clipboard.SetFilesAsync`/`ClipboardHelper.SetBitmap`；「打开方式」退化为默认应用打开 + 提示 | Avalonia 无 GridView/GroupStyle/ListViewSelectionMode/Behaviors；`ImageBatchConvertWindow` 未移植（§8.4），批量转换按钮先给提示 |
| 13 | `Features/Screenshot/ImageViewWindow.axaml(.cs)` | `WindowEx` + `AppWindow.TitleBar`（拖动区域/按钮颜色）+ `CenterInScreen` + `SW_SHOWMAXIMIZED` + `ScrollViewer.ZoomMode/ZoomFactor/ChangeView` + `CachedImage.Rotation` + 亚克力背景/`ThemeShadow` + `GWX UIScale` | `Window`（`ExtendClientAreaToDecorationsHint`，打开即 `WindowState.Maximized`）+ 可变换图片宿主（`ScaleTransform`/`RotateTransform`/`TranslateTransform`，自维护缩放与平移、按视口中心缩放）+ `RotateTransform` 旋转 + 半透明 `Border`；提示文案走 `AppStrings` | Avalonia 无 WinUI 内置缩放/亚克力/阴影；缩放率文本不再乘 `UIScale`（按 1.0 计）；`Ctrl+滚轮` 缩放改为显式实现 |
| 14 | `Features/Screenshot/*.axaml(.cs)`（Avalonia 12 API 适配，跨 4 个页面/窗口 + `ScreenCaptureService`） | 上游页面靠 `PageBase` 的 `[INotifyPropertyChanged]` 提供 `SetProperty`；`DataPackage`/`DataFormats.Files`/`DataObject`/`DragDrop.DoDragDrop`；`Window.Clipboard` 的 `SetStorageItems`；`InAppToast.Show("LangKey", text)` 式调用 | ①每个页面在类内实现 `public new event PropertyChangedEventHandler? PropertyChanged` + `protected bool SetProperty<T>(...)`（形状与上游一致，因为 Avalonia 的 `PageBase : UserControl` 不能再挂源生成特性）；②拖拽改 `DataTransfer` + `DataTransferItem.CreateFile(...)` + `DragDrop.DoDragDropAsync(PointerPressedEventArgs, ...)`（需保存按下事件的 `PointerPressedEventArgs`）；③复制文件走 `using Avalonia.Input.Platform;` 下的 `ClipboardExtensions.SetFilesAsync`；④`IStorageProvider.TryGetFileFromPathAsync` 在 12 里只接受 `System.Uri`（字符串重载是扩展方法，会被实例方法遮蔽），统一 `new Uri(path)`；⑤提示统一 `InAppToast.MainWindow?.Information/Success/Warning/Error(Lang.Xxx, msg)`（硬编码 Key 字符串改为 `Lang.*` 强类型成员） | 全是 Avalonia 12 的 API 形状差异，行为不变 |
| 15 | `Controls/ColorfulTextBlock`、`Features/Screenshot/*` 中的 `x:Name` 变换 | 上游把 `x:Name` 挂在 `<TransformGroup>` 内的 `<ScaleTransform>`/`<RotateTransform>`/`<TranslateTransform>` 上直接引用 | XAML 原样保留，但 Avalonia 12 的 XAML 生成器**不为 `TransformGroup` 子项生成字段**；改为代码侧取值属性：`private TransformGroup? _TransformGroup_Image => (TransformGroup?)_Image.RenderTransform;` 再按 `Children[0..2]` 取 Scale/Rotate/Translate | Avalonia 12 生成器限制（`x:Name` 不生效时不报错，只报字段缺失），XAML 结构不变 |
| 16 | `Features/Screenshot/ScreenCaptureInfoWindow.axaml(.cs)` | WinUI 无边框浮窗：`AppWindow.MoveAndResize` + `AppWindow.Show(false)`（不激活显示）、`CanvasImageSource` 缩略图（`CropEffect`/`ScaleEffect`，HDR 走 `HdrToneMapEffect`+`WhiteLevelAdjustmentEffect`+`GammaTransferEffect`）、合成器 `Vector3KeyFrameAnimation` 滑入/滑出、`GetClientRect`/`ClientToScreen`/`MonitorFromWindow` 定位、`FolderLauncherOptions.ItemsToSelect` 打开日志 | Avalonia `Window`（`SystemDecorations=None`、`Topmost`、`ShowInTaskbar=false`、`ShowActivated=false`、`Background=Transparent`）+ `Position` 定位（游戏客户区用 `xdotool getwindowgeometry --shell` 查询，Wayland 退回屏幕工作区右缘 + 25% 高，越界同样回退）+ `CroppedBitmap`+`RenderTargetBitmap` 画 72×72 缩略图（HDR 侧在解码阶段已转 SDR）+ `TranslateTransform`+`Transitions(DoubleTransition X, 0.6s, CubicEaseOut)` 做滑入/滑出（0.6s 节流照上游）+ 打开图片用本地 `ImageViewWindow2`、打开日志用 `xdg-open <目录>`（无选中文件等价物） | Avalonia 无 `AppWindow`/合成器动画/Win2D；`CaptureStart(hwnd, Bitmap, maxCLL)` 等公开签名保持上游形状（仅像素容器改 `Bitmap`），`DisplayId` 重载未移植（本仓库抓屏按窗口，不按显示器）；`SystemDecorations` 枚举在 Avalonia 12 为 `WindowDecorations` |
| 17 | `Features/Screenshot/ImageBatchConvertWindow.axaml(.cs)` | WIC `BitmapDecoder`/`BitmapEncoder`（JPEG/PNG 编码、`ImageQuality`/`JpegYCrCbSubsampling` 选项）、Starward.Codec 的 AVIF/JXL 编解码器、`avifenc/avifdec/cjxl/djxl` 走 `AppContext.BaseDirectory` 的 exe、`StorageFile`/`FolderLauncherOptions`/`DragEventArgs.DataView`、`HyperlinkButton` | 统一走本地 `Features/Codec` 的 `ImageLoader`（Skia + 外部 `avifdec`/`djxl`）与 `ImageSaver`（PNG 自写编码器 + 外部 `avifenc`/`cjxl`，均在 PATH 上查找）；JPEG 用 Skia 编码（浮点/16bit 源先 `ToRgba8888` 画到 8bit 画布）；JXL 的 0–100 质量按 `distance=(100-quality)*0.1` 线性映射；`HyperlinkButton` 换 `Button Classes="link"`、`ListView` 换 `ListBox`、`ProgressRing` 换 `ProgressBar`、`Run` 拼接换 `InfoText` 属性；拖拽用 `e.DataTransfer.TryGetFiles()`；打开文件所在目录用 `xdg-open` | 无 WIC/JxlSharp/ImageSharp；XMP 由 `ScreenCaptureService.BuildXMPMetadata` 提供；转换失败文案、跳过/覆盖/重命名三种模式与上游一致 |

### 8.4 尚未移植（明确缺口，非「已完成」）

本组（`Controls` 5 个文件 + `Features/Screenshot` 23 个文件）已全部落地，**无未移植文件**。

仍然存在的**行为级**缺口（不是文件缺口，验收时按此判断）：

| 项 | 现状 | 处理 |
|---|---|---|
| HDR / ICC 管线 | 一律按 SDR（BT.709）采集与展示；`SaveAsSdrAsync` 恒返回 null | `docs/09` §8.2 #14–#16 已登记；`EnableScreenshotColorManagement` 开关保留但只影响 PNG iCCP/cICP 写出 |
| 抓屏后端 | X11：`import`（ImageMagick）或 `xwd`+`convert`；Wayland：`grim`（可 `-g` 区域）；窗口定位靠 `xdotool`。三者缺任一时抛 `PlatformNotSupportedException` 并 toast 提示，不静默黑屏 | 同上；安装 `imagemagick`/`grim`/`xdotool` 即完整可用 |
| 剪贴板图片 | `ClipboardHelper.SetBitmap` 走 Avalonia 剪贴板位图；文件形式复制走 `ClipboardExtensions.SetFilesAsync` | 同上 |
| 「用其他应用打开」 | Linux 无 `SHOpenWithDialog` 等价物，退化为 `xdg-open` 打开默认应用并 toast 说明 | 同上（§8.3 #12） |

### 8.5 Features/Overlay（`RunningGame` / `RunningGameService`）

| # | 位置 | 上游 | 本仓库 | 原因/待办 |
|---|---|---|---|---|
| 1 | `Features/Overlay/RunningGame.cs` | `User32.IsWindow`/`IsWindowVisible` 校验后回退 `Process.MainWindowHandle`；`WinEventHook` 为 `User32.HWINEVENTHOOK` | `WindowHandle` 仅在为 0 时回退 `Process.MainWindowHandle`；`WinEventHook` 降级为 `nint` 字段 | Linux 无 `User32`；.NET/Linux 的 `MainWindowHandle` 恒为 0，窗口定位留给抓屏后端（X11 `xdotool search --pid`/`xwininfo`，Wayland 无等价物） |
| 2 | `Features/Overlay/RunningGameService.cs` | `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` + `WinEventProc` 维护「最近活跃游戏」；`_overlayWindow` 关闭；`GamepadController` 引导键处理；`OpenOverlayWindow()` 首行 `return false;` | 钩子整体移除，「最近活跃」= 最后加入的游戏；空列表时 `_timer.Stop()` + 归还引导键；`OpenOverlayWindow()` 仍恒 `false`；`OverlayWindow` 未移植 | 前台窗口事件在 Linux 无等价物（X11 需 `_NET_ACTIVE_WINDOW` 轮询，未做）；`OverlayWindow.xaml(.cs)` 属 §8.4 缺口，落地后把 `ShowActive` 接回 |

方法名 `AddRuninngGame` 保持上游拼写（`Runinng` 双 n），调用方 `GameLauncherPage` 已按此名接线。

## 九、Features/Background（2026-10-26，settings-migrate / task-6）

上游 `src/Starward/Features/Background/`（13 文件，`AppBackground` 637 行 + `BackgroundViewPage` 546 行 + 图库窗口）。
落点：`src/LunarWard/Features/Background/`，类名/成员名/文件名（`.xaml`→`.axaml`）与上游一致。
本节**逐条**记录渲染管线差异，不是「差不多的等价物」：

| # | 位置 | 上游（WinUI） | 本仓库（Avalonia/Skia） | 原因与判据 |
|---|---|---|---|---|
| 1 | `AppBackground.xaml` 的 `swi:ImageEx` + `AppBackgroundImageExStyle.xaml` | `Scighost.WinUI.ImageEx`：模板内 `PlaceholderImage`（Opacity 1）+ `Image`（Opacity 0）+ `VisualStateManager` 三态——`Loaded`：Image 0→1（0.3s，BeginTime 0）且 Placeholder 延迟 0.3s 后 1→0；`Failed`：Image=0/Placeholder=1；`Unloaded`：瞬时 1/0 | 两层普通 `Image`（`Classes="app-background-placeholder"` / `"app-background-image"`）+ `AppBackgroundImageExStyle.xaml` 里 `DoubleTransition(Opacity, 0.3s)`；三态由 `ApplyLoadedVisualState` / `ApplyFailedVisualState` / `ApplyUnloadedVisualState` 实现，参数逐字对应上游 Storyboard（占位图 300ms 延迟用 `Task.Delay(300)`；`Unloaded` 临时摘掉 `Transitions` 再赋值以做到「瞬时」） | Avalonia 无 ImageEx 控件、无 VisualStateManager；`ImageStretch`/`PlaceholderStretch` 折叠为 `Stretch="UniformToFill"`。判据：切换背景时旧图淡出、新图 0.3s 淡入、解码失败只留占位图，与上游一致 |
| 2 | `AppBackground.xaml.cs` `XamlRoot_Changed` | `XamlRoot.Changed` + `RasterizationScale` 变化时重取背景（按新物理像素解码） | `TopLevel.ScalingChanged` + `TopLevel.RenderScaling`（`GetUIScaleFactor()`），`_lastScale` 比较后 `UpdateBackgroundAsync()` | Avalonia 无 `XamlRoot`；同一个「缩放变化→按新尺寸重解码」语义 |
| 3 | `ChangeBackgroundImageAsync` | `BitmapDecoder` + Win2D `CanvasBitmap`/`CanvasRenderTarget`，按 `ActualWidth × RasterizationScale` 缩放取样后再 `WriteableBitmap` | `Bitmap.DecodeToWidth(fs, (int)(ActualWidth * RenderScaling))`（Skia 缩放解码），失败回退 `new Bitmap(fs)` 交给 `Stretch` | 无 Win2D/WIC；解码尺寸策略（不超过窗口物理像素）保持一致，避免 4K 背景整图常驻 |
| 4 | `UpdateBackgroundAsync` 图片解码异常分支 | `COMException 0x88982F8B`（WebP 解码失败）→ `InAppToast` + Microsoft Store「WebP 图像扩展」按钮；VP9 视频扩展提示 `SuggestToInstallVP9Decoder` | 删除该分支（Skia 内置 WebP/VP9 解码，无扩展安装概念）；解码失败走 `ApplyFailedVisualState()` + `ILogger.LogError` | 平台能力差异，非功能退化；`Lang.AppBackground_PleaseInstallTheWebPImageExtension` 等键保留未用 |
| 5 | `AppBackground.xaml` 视频层 | `MediaPlayer` + `CompositionVideoSurface` + `MediaPlayerElement` 硬件解码，`CanvasBitmap` 逐帧与 `Theme` 叠加图合成 | `VideoHost`（`ContentControl`）内嵌 `VideoBackgroundPlayer`：ffmpeg 子进程解帧 → `WriteableBitmap`（960×540 BGRA、24fps 上限、`-an` 无音轨、失焦 SIGSTOP）；视频叠加图（`gameBackground.Theme` 海报/logo）独立 `Image` 层 `UniformToFill` 叠在视频帧上，替代上游每帧 CPU 合成 | Avalonia 无 MediaPlayer/视频 surface（详细取舍见 §四）；独立叠层画面等价且省每帧合成开销 |
| 6 | `AppBackground.xaml.cs` 强调色 | 取背景主色后 `AccentColorHelper.ChangeAppAccentColor`；`AccentColorChangedMessage` 负责窗口重绘 | 保留 `AccentColorHelper`（取色算法逐字移植）+ `WeakReferenceMessenger` 消息；`AppBackground` 订阅 `AccentColorChangedMessage` 只做资源失效（Avalonia `DynamicResource` 自动刷新），不再翻转 `RequestedTheme` | 上游 `RequestedTheme` 翻转是 WinUI 的强制重绘 workaround，Avalonia 无此问题 |
| 7 | `BackgroundViewWindow.xaml(.cs)` | `WindowEx` + `AppWindow.TitleBar`（ExtendsContentIntoTitleBar/图标/按钮颜色）+ `DesktopAcrylicBackdrop` + `OverlappedPresenter`（不可缩放/居中）+ `DisplayArea.GetFromPoint`（光标所在屏） | 普通 `Window`（`WindowStartupLocation=CenterScreen`、`CanResize=false`）+ `ExtendClientAreaToDecorationsHint` + 32px 拖拽条；标题「LunarWard - Background Viewer」；`Escape` 关闭；`AccentColorChangedMessage` 回调留空 | Avalonia 无 AppWindow/Backdrop API；**多显示器时居中按主屏**（上游按光标所在屏），登记为差异 |
| 8 | `BackgroundViewPage.xaml(.cs)` 图库 | `GridView`+`UniformGridLayout`、`ItemClick`、`DragItemsStarting`+`StorageFile`+`DataPackage`、`RadioMenuFlyoutItem` 筛选/排序、`Launcher.LaunchUriAsync/LaunchFolderAsync/LaunchFileAsync`、`ClipboardHelper.SetStorageItems`、`StorageFile.DeleteAsync`、`ImageThumbnail`（WIC 缩略图） | `ListBox` + `WrapPanel`（160×90 缩略图）+ `DoubleTapped` 打开；每项 `ContextFlyout`（打开/复制路径/删除）；筛选排序 = `MenuItem ToggleType="Radio"`；打开文件/文件夹 = `Process.Start` + `xdg-open`；复制 = `Clipboard.SetTextAsync`（路径行文本）；删除 = `File.Delete`；缩略图 = `Bitmap.DecodeToWidth(stream, 320)` | Avalonia 无 GridView/UniformGridLayout、无 WinRT StorageFile/Launcher/DataPackage；**拖拽导出（DragItemsStarting）未实现**（需 StorageProvider，登记为缺口）；多选复制得到的是路径文本而非文件对象 |
| 9 | `BackgroundFileItem.cs` | `IsVideo` 由扩展名 + `FileIsSupportedVideo` 判定；缩略图由 `ImageEx` 的 `ImageThumbnail` 提供 | 保留 `IsVideo`/`FileSize`/`CreationTime`；新增懒加载 `Thumbnail`（`Avalonia.Media.Imaging.Bitmap.DecodeToWidth`，失败返回 null → 模板显示图标占位） | Avalonia 无 WIC 缩略图缓存；解码宽度 320 与上游缩略图尺寸接近 |

**未移植（明确缺口）**：`BackgroundViewPage` 的拖拽/多选复制为文件对象（`DataObject` + `IStorageItem`），以及 `BackgroundViewWindow` 的 Acrylic 背景——前者可用 `StorageProvider` 后续补，后者 Avalonia 无系统级 Acrylic。

---

## 十、Features/Setting（2026-10-26，settings-migrate / task-3）

上游 `src/Starward/Features/Setting/`（19 文件）与 `src/Starward/ViewModels/Starward/**` 无关：设置页逻辑在上游**就是 code-behind**。
本轮把本仓库早先拆出的 10 个 `Setting*ViewModel` 全部搬回页面类（`DataContext=this`），路径/类名/成员名与上游一致：

| 上游 | 本仓库 | 说明 |
|---|---|---|
| `SettingPage.xaml(.cs)` | `Features/Setting/SettingPage.axaml(.cs)` | 宿主页；9 个入口 + 分隔线；`NavigationView_ItemInvoked` → `SelectSection(nameof(XSetting))`；`--settings-section` / `LUNARWARD_SETTINGS_SECTION` 初始定位保留 |
| `AboutSetting` | `Features/Setting/AboutSetting.axaml(.cs)` | 版本/系统信息/链接/更新检查 |
| `GeneralSetting` | `Features/Setting/GeneralSetting.axaml(.cs)` | 关窗行为/自启动/语言/公告/录像背景/账号切换 |
| `DownloadSetting` | `Features/Setting/DownloadSetting.axaml(.cs)` | 并发/超时/重试/组件索引源 |
| `FileManageSetting` | `Features/Setting/FileManageSetting.axaml(.cs)` | 目录/缓存清理/数据库备份（含确认浮层） |
| `ScreenshotSetting` | `Features/Setting/ScreenshotSetting.axaml(.cs)` | 截图目录/数量/打开/复制 |
| `HotkeySetting` | `Features/Setting/HotkeySetting.axaml(.cs)` | 见下 #1 |
| `GamepadControlSetting` | `Features/Setting/GamepadControlSetting.axaml(.cs)` | 手柄策略页（原 `Settings/GamepadSetting` 已合并删除，见 #2） |
| `AdvancedSetting` | `Features/Setting/AdvancedSetting.axaml(.cs)` | 环境变量/URL 协议注册/日志级别 |
| `ToolboxSetting` | `Features/Setting/ToolboxSetting.axaml(.cs)` | 工具箱入口 |
| `HotkeyInput.xaml(.cs)` | `Features/Setting/HotkeyInput.axaml(.cs)` | 见下 #1 |
| `HotkeyManager.cs` | `Features/Setting/HotkeyManager.cs` | 见下 #1 |
| `LanguageChangedMessage.cs` | `Features/Setting/LanguageChangedMessage.cs` | `WeakReferenceMessenger` 消息（空类型），与上游同名同命名空间 |
| `ToolboxItem.cs` | `Features/Setting/ToolboxItem.cs` | 图库条目模型（`Icon`/`Image`/`Name`/`Description`/`Tag`/资源键 + `UpdateLanguage`） |

**多出的文件（为什么不是偏移）**：
- `SettingLocalization.cs`：把配置语言同步到上游 `Lang.Culture`（`AppConfig.SetLanguage` + `Lang.Culture`），等价上游启动时的 `AppConfig.SetLanguage`；只在页面构造时调用一次，不是基类。
- `SettingPageBase.cs` **已删除**（2026-10-26 第二轮）：设置页现在与上游一样直接继承 `Frameworks.PageBase`，无参构造，`DataContext=this`；服务用 `AppServices.Current` / `AppConfig.GetService<T>()`，不再有自造的页面基类与 `PageContext` 注入。
- `SettingShellActions.cs`：上游直接调 Win32/注册表/WinUI API 的三处设置动作（URL 协议注册探测、打开目录、桌面项定位）在本仓库抽成静态门面，供 Advanced/Hotkey 页复用同一份 Linux 实现，避免每页各写一遍。
- `SettingLanguageOption.cs`：语言下拉项（`Code`/`Display`），上游内联在 `GeneralSetting` 的 `x:Bind` 数据模板里；Avalonia 模板需要具名类型 + `x:DataType`。
- `HotkeyInput` 配套的 `HotkeyManager.cs`（见 #1）和 `LanguageChangedMessage.cs` 与上游一一对应，不是额外文件。

**第二轮基类/取服务对齐（2026-10-26）**：10 个页面全部改为 `: Frameworks.PageBase` + 无参构造函数，`SettingPageBase` 与 `PageContext` 注入移除。取服务改为上游口径：`AppServices.Current`（`Paths`/`Config`/`Log`/`LauncherService`…）、`AppConfig.GetService<T>()`、`AppConfig.GetLogger<T>()`；游戏上下文只认 `PageBase.CurrentGameId`/`CurrentGameBiz`（由 `Frameworks.Frame.Navigate(page, param)` 传入）。
具体三处：
- `ScreenshotSetting`：改为上游全局 `AppConfig.ScreenshotFolder`（默认 `Path.Join(AppConfig.UserDataFolder, "Screenshots")`），不再按档案推导游戏内截图目录；新增 `AppConfig.ScreenshotFolder` 属性（`AppConfig.Setting.cs`，另一 agent 已落地）。
- `AdvancedSetting`：删除本地旧壳独有的「环境变量基线（`LauncherService.BuildPlan`）」区块，上游 `AdvancedSetting` 没有这段；只保留 URL 协议探测 + 自检命令。
- `ToolboxSetting`：与上游一致改成三个窗口入口（`UIGF4GachaWindow`、`ZZZGachaInfoWindow`、`ImageViewWindow2`），`ToolboxItem` 也恢复上游形状（`Icon/Image/Tag/NameResourceKey/DescriptionResourceKey` + `UpdateLanguage`）；删除原先经 `Shell.Navigate` 的「游戏设置/诊断/环境 shell/截图」入口。

| # | 差异 | 上游 | 本仓库 | 原因 |
|---|---|---|---|---|
| 1 | `HotkeySetting` / `HotkeyInput` / `HotkeyManager` | Win32 `RegisterHotKey`（`ShowMainWindow` id 44444 `MOD_ALT+VK_S`、`ScreenshotCapture` id 44445 `MOD_ALT+VK_D`）全局注册；`HotkeyInput` 的 `WindowHandle`/PInvoke 键盘钩子；`HotkeyManager` 依赖 `Vanara.PInvoke` | `HotkeyInput` 保留完整控件结构与状态机（`HoykeyInputState`：Default/Editing/Warning/Success；`HotkeyEditing`/`HotkeyEditFinished`/`HotkeyDeleted` 事件；`GetHotkeyText`/`IsHotkeyAvaliable`/`GetHotkeyInfo` 静态 API 同名），但只做**应用内按键录制 + 落盘**（`AppConfig` 的 `show_main_window_hotkey` / `screenshot_capture_hotkey`）；`HotkeyManager.RegisterHotkey` 在 Linux 返回失败并让页面显示「全局热键不可用」提示（对应上游注册失败时的 Warning 态） | Linux 无 `RegisterHotKey`（X11 XGrabKey 需连接 X、Wayland 无全局热键协议）；保留「录制 + 持久化 + 状态提示」，只是不注册到系统。键位表删掉 `OemBracketLeft/Right`（Avalonia 无此枚举值，用 `OemOpenBrackets/OemCloseBrackets`） |
| 2 | `GamepadControlSetting` | 上游只有 `GamepadControlSetting`（手柄策略/按键映射空壳页） | 同路径同文件名；原 `Views/Starward/Settings/GamepadSetting.*` + `ViewModels/.../GamepadSettingViewModel.cs` 删除；`SettingSectionTags.Parse` 同时接受 `gamepad`/`gamepadcontrol`/`controllerenhancement`，旧深链不失效 | 上游无 `GamepadSetting` 文件，两个页面合并为一个，避免「本仓库多一个上游没有的页面」这类偏移 |
| 3 | 语言切换刷新 | `LanguageChangedMessage` + `WeakReferenceMessenger.Register` → `this.Bindings.Update()`（`x:Bind` 需手动刷新） | 保留消息类型（`GeneralSetting` 应用语言后广播，供以后移植的 `x:Bind` 页面用）；设置页自身用 `{Binding Strings[...]}` 运行期索引器，`AppStrings` 的 `Item[]`/`""` 通知已自动刷新，无需 `Bindings.Update()` | Avalonia 无 `Bindings.Update()`；运行期绑定天然重求值 |
| 4 | 语言/资源 | `x:Uid` / `Lang.*` 强类型 resx | `AppStrings.Instance["..."]` 索引器 + `Lang.*` 静态键并存；键名与上游 resx 一致（`SettingsPage_*`、`SettingPage_*`、`HotkeySetting_*` 等），新增键写入 `src/LunarWard.Language/Lang.resx` 并同步 `en-US` | Avalonia 无 `x:Uid` 资源注入 |
| 5 | 确认浮层 | `ContentDialog` | `SettingPageBase` 暴露 `Confirmation`（`IsConfirmOpen`/`Title`/`Body`/`ConfirmText`/`CancelText` + 命令），宿主在 `SettingPage` 叠加层画卡片 | Avalonia 无 ContentDialog；浮层行为与上游 `ContentDialog` 的「确认/取消 + TCS」一致 |
| 6 | 备份数据库 | `Microsoft.Windows.Storage.Pickers` | `TopLevel.StorageProvider.SaveFilePickerAsync` + `LunarWardDatabase.BackupTo`（`VACUUM INTO`） | 平台 API 替换 |

**删除清单（本轮已 git rm/移动）**：`src/LunarWard/Views/Starward/Settings/` 整目录（10 个 `Setting*View.axaml(.cs)` + `SettingSectionView.cs`）、`src/LunarWard/ViewModels/Starward/Settings/` 整目录（10 个 `Setting*ViewModel.cs`）、`ViewModels/Starward/SettingPageViewModel.cs`。
**未对齐（明确缺口）**：无（19 个上游文件全部有同名落点）。

## 十一、Features/Codec（图片编解码：Win2D + Starward.Codec → SkiaSharp + 自写 PNG + 外部 CLI）

上游 `Features/Codec/` 的全部能力压在三个 Windows-only 组件上：`Microsoft.Graphics.Win2D`（`CanvasBitmap`/`CanvasRenderTarget`/`CanvasEffect` 导出链）、`ComputeSharp.D2D1`（着色器）、`Starward.Codec` NuGet（AVIF/JXL/PNG/UltraHDR/ICC/VP9 解码器，源码不在仓库）。Linux 三者全不可用，本移植按「公开 API 名字/参数一字不改，只换类型」的原则重建：

| 位置 | 上游行为 | 本仓库行为 | 原因 | 待办 |
|---|---|---|---|---|
| `ImageInfo.CanvasBitmap` | 类型 `CanvasBitmap` | 类型 `SKBitmap`（属性名不变，上层只碰像素/尺寸） | Win2D 无 Linux 版；SkiaSharp 随 Avalonia 自带 | — |
| `DirectXPixelFormat` | Win2D 枚举 | `Codec/DirectXPixelFormat.cs` 同值枚举（全仓库只用到 5 个成员：`R8G8B8A8UIntNormalized`/`B8G8R8A8UIntNormalized`/`R16G16B16A16UIntNormalized`/`R16G16B16A16Float`/`R32G32B32A32Float`） | `ImageSaver.SaveAsPngAsync` 的公开签名要保留 | 若后续出现新成员需补映射 |
| `ImageLoader` AVIF 解码 | `Starward.Codec.avifDecoderLite`（libavif 静态链接） | 调外部 **`avifdec`**（`-d 8/16` 出 PNG），再用自写 `PngDecoder` 读像素；容器 `colr`(nclx/prof)/`pixi`/`ispe` box 由 `AvifContainer` 手写解析 | SkiaSharp 3.119.4 的 Skia 构建不带 AVIF 解码器（实测 `SKCodec.Create` 返回 null）；libavif 未随发行版提供 | 可换成 P/Invoke libavif（速度更好、免临时文件） |
| `ImageLoader` JXL 解码 | `Starward.Codec.JxlDecoderLite` | 调外部 **`djxl`** 解码成 PNG；色彩信息从 **`jxlinfo`** 文本解析（`Primaries`/`Transfer function`/`-bit`） | 同上，Skia 不带 JXL；libjxl 的 C API 需要绑定 | 同上 |
| `ImageLoader` PNG 解码 | `CanvasBitmap.LoadAsync` + 手写 `PngReader` 读色彩块 | **完全自写** `PngReader`（chunk 解析）+ `PngDecoder`（8/16-bit、5 种 filter、zlib 用 `System.IO.Compression.ZLibStream`） | Skia 对 16-bit PNG 解码返回 `InvalidConversion`（实测），而 HDR10 PNG 正是 16-bit；且上游要读 cICP/iCCP/sRGB/cHRM 色彩块 | 隔行 PNG 抛 `NotSupportedException`（消息指向本文档），按需补 Adam7 |
| `ImageLoader` JPEG 解码 | 同上 + `UhdrDecoder.GetIccData()` | Skia 解码 + `JpegMetadata.ReadIccProfile` 拼接 `ICC_PROFILE` APP2 段（14 字节头：`ICC_PROFILE\0` + seq + count） | UltraHDR 元数据读取器在 `Starward.Codec` 内 | gainmap 侧只读基图 ICC，暂不解 HDR gainmap 像素 |
| `HDR10GammaEffect`（`ScRGBToHDR10Effect`/`HDR10ToScRGBEffect`） | D2D `CanvasEffect` 图（ColorMatrixEffect + PQ OETF/EOTF 着色器） | 同名类 + 同名属性（`Source`/`BufferPrecision`）+ `SKBitmap Apply()`；PQ 常量（`c1=107/128`、`c2=2413/128`、`c3=2392/128`、`m=2523/32`、`n=1305/8192`、`maxLevel=125`）与 BT.709↔BT.2020 / BT.2020↔BT.709 矩阵与上游**逐位一致**，循环体是 `Span<float>` CPU 实现 | 无 D2D 着色器管线 | 大图可并行化（目前单线程，1080p 级数十毫秒） |
| `SrgbGammaEffect` | D2D 着色器 | 同名类 + 同名 `GammaMode`（EOTF/OETF）与 `Apply()`；公式（0.04045/0.0031308 阈值的 lerp）逐字一致 | 同上 | — |
| `UhdrPixelGainEffect`/`UhdrGainmapEffect` | D2D 着色器（`OFFSET=0.015625`、log2 归一化） | 同名类 + 同名属性（`SdrSource`/`HdrSource`/`MinContentBoost`/`MaxContentBoost`，`float3`→`Vector3`）+ `Apply()`，公式逐字一致 | 同上 | — |
| `ImageSaver.SaveAsPngAsync` | `BitmapEncoder` 编码后 `PngReader` 重写色彩 chunk | 自写 `PngEncoder`：IHDR + （cICP(9,16,0,1) / iCCP / sRGB / cHRM 择一）+ iTXt(XMP) + IDAT + IEND，CRC32 自算；8-bit→`Rgba8888`、16-bit→`Rgba16161616` | Skia 无法写 16-bit RGBA PNG（`SKImage.Encode` 返回 0 字节，实测） | — |
| `ImageSaver.SaveAsAvifAsync` | `avifEncoderLite`（YUV444、depth 8..12、nclx/ICC） | 先写临时 PNG → **`avifenc --yuv 444 --qcolor q --depth 8\|12`**，色彩用 `--cicp 9/16/9`（HDR10）、`1/13/6`（BT.709）、`12/13/0`（P3）或 `--icc`（自定义 primaries，ICC 由 `ICCHelper.CreateIccData` 生成） | 无 libavif 绑定 | 临时文件可换成管道 |
| `ImageSaver.SaveAsJxlAsync` | `JxlEncoder`（distance 0..25，0=lossless，HDR10 用 `JxlColorEncoding.HDR10`） | 临时 PNG → **`cjxl`**（`--lossless` 或 `-d`）；HDR10 追加 `-e 7` | 同上 | `cjxl 0.12` 没有写 CICP 的参数，**JXL 里的 PQ/BT.2020 标注暂时丢失**（解码端靠像素位深与 `jxlinfo` 兜底），需要换 libjxl API 绑定才能真正写入 |
| `ImageSaver.SaveAsUhdrAsync` | HdrToneMap + WhiteLevelAdjustment + SrgbGamma(OETF) + UhdrPixelGain + UhdrGainmap（D2D 图），`UhdrEncoder` 写 base JPEG + gainmap JPEG + `hdrgm` 元数据 | 同一公式的 CPU 实现（tone map 用 `max(hdr,0)*80/sdrWhiteLevel` 近似上游 `WhiteLevelAdjustmentEffect{InputWhiteLevel=80}`）；base/gainmap 各出一张 JPEG（Skia，quality 90），`hdrgm` metadata 写进 XMP（APP1），两张图用 **MPF（APP2，CIPA DC-007 TIFF IFD）** 串成多图 JPEG | 无 libultrahdr | ①MP Entry 的 Individual Image Attribute 暂填 0，未与 libultrahdr 位定义逐位核对；②未实现 ISO 21496-1 GContainer box 形式（Adobe/Android 15 的 UltraHDR 主格式）。两者都需要真实 UltraHDR 读取器回归 |
| `ImageThumbnail` 返回类型 | `BitmapSource`（WinRT） | `Avalonia.Media.Imaging.Bitmap` | Avalonia 无 WinRT 位图 | — |
| `ImageThumbnail` 缓存/尺寸/命名 | `XxHash64+MD5` 24 字节大写 hex、412×232 目标框、`ProcessorCount/2` 信号量、`Task.Delay(300)` | 完全一致（`GetCachedThumbnailName`/`GetThumbnailSize`/`SemaphoreSlim`/`Delay` 逐字移植） | — | — |
| `ImageThumbnail.AvifDecoderSupported`/`JxlDecoderSupported` | 用 1×1 内嵌样本试探 WIC/Skia 是否带解码器 | 探测 PATH 上是否有 `avifdec`/`djxl` | 平台解码器来自 CLI | 启动时可加一次性自检日志 |
| `ImageThumbnail.GetVideoThumbnailAsync` | WebM→`MediaPlayer` 取首帧（3s 超时）；其它→`StorageFile.GetThumbnailAsync` | **`ffmpeg -ss 0 -frames:v 1 -q:v 4`** 抽帧，失败抛异常（消息带 exit code 与 stderr） | 无 MediaPlayer/StorageFile | 可换 GStreamer（本机 `gst-launch-1.0` 在） |
| `VP9Helper` 的 EBML/VP9 profile 解析 | 纯托管 | **逐字一致**（`ReadSize=1<<20`、`SimpleBlock 0xA3`/`Block 0xA1`、syncCode `0x498342`、`colorSpace==7 → RGB`） | — | — |
| `VP9Helper` 的 MFT 注册/`IsVP9DecoderInstalled` | 注册 `VP9Decoder`/`Ogg Vorbis` MFT、查注册表 | 保留全部方法签名与状态位语义；`IsVP9DecoderInstalled()` 改为探测 PATH 中的 `ffmpeg`；Register/Unregister 只置位（注明“Linux 无 MFT 概念，解码由 libdav1d/libvpx 提供”） | Linux 没有 Media Foundation | 若以后接入 GStreamer，可换成 plugin 探测 |
| `ColorPrimaries`/`ICCHelper` | 在 `Starward.Codec` NuGet 内（`ColorPrimaries.Id/Red/Green/Blue/White/IsValid`、`GetColorTransferMatrix` 返回 `Matrix4x4`、`GetColorPrimariesFromIccData`、`CreateIccData`） | `Codec/ICC/ColorPrimaries.cs` 同名同成员自实现：ICC v2 matrix/TRC profile 生成（Bradford→D50、curv γ2.2）、rXYZ/gXYZ/bXYZ/wtpt 反解色度坐标、`GetColorTransferMatrix` 返回 `System.Numerics.Matrix4x4`（行主序，与 D2D `ColorMatrixEffect` 的转置布局一致，`Screenshot/ImageViewWindow2` 的 `ToMatrix5x4` 直接可用） | 该类在闭源 NuGet 里 | `IsValid` 故意不依赖 `Id`，否则 cHRM 自定义原色会被 `ImageLoader` 末尾的 BT.709 兜底覆盖 |

**性能/精度取舍**：AVIF/JXL 走外部进程会比上游慢一个数量级（进程启动 + 磁盘往返，典型 8K JXL 200~600ms/张），但只在打开图片/保存时发生，缩略图有磁盘缓存，可接受；HDR10↔scRGB、SRGB gamma、gainmap 归一化全部是单线程 Span 循环，精度与上游着色器同为 `float`，1080p 约 15~40ms、4K 约 60~150ms。

## 十二、更新与安装（Features/Update）

| 位置 | 上游行为 | 本仓库行为 | 原因 | 待办 |
|---|---|---|---|---|
| `UpdateService.DecompressOrPatchFileAsync` 的补丁分支 | `Snap.HPatch`（`HPatch.PatchZstandard`，win-x64 原生库） | `LunarWard.Installer.HdiffPatchPatcher`：按 `Patch.Offset/Length` 用 `FileSliceStream` 切出补丁片段，再调外部 **`hpatchz`** CLI；找不到 `hpatchz` 时抛 `InstallerCapabilityException`（消息指向本文档），**不静默失败、不假装成功** | `Snap.HPatch` 只有 win-x64 二进制 | 可换 `libhdiffpatch` 的 P/Invoke；`LUNARWARD_HPATCHZ` 可指定路径 |
| `SetupService.UpdateAsync` 启动安装器 | `Process.Start{ UseShellExecute=true, Verb="runas" }`（UAC 提权） | 去掉 `Verb="runas"`，其余参数（`update --InstallFolder ... --OldVersion ... --NewVersion ... --Preview ... --pid ...`）逐字一致 | Linux 没有 UAC；提权由包管理器/用户自己决定 | 若将来走 dnf/apt 包，改为调用包管理器 |
| `UpdateWindow` 外观 | `DesktopAcrylicBackdrop` + `ExtendsContentIntoTitleBar` | `TransparencyLevelHint=[Transparent]` + `StarAcrylicBrush` 背景 | Avalonia 无 DWM acrylic | 可用合成器 blur（KWin/GNOME 扩展）替代 |
| `UpdateWindow` 重启自身 | `RpcService.KeepRunningOnExited(false, noLongerChange:true)` + `StarwardPortableLauncherExecutePath` | 保留 launcher 重启逻辑；`RpcService` 调用标 `TODO(port)`（Features/RPC 尚未移植） | 该服务不在本批 | 移植 RPC 后接线 |
| `UpdateWindow` WebView2 异常分支 | catch `COMException` | catch `InvalidOperationException`（WebKitGTK 缺失时本仓库 WebView 抛这个） | WebView 后端不同 | — |
| 相关链接 | `https://github.com/Scighost/Starward/releases/tag/{version}` | `https://github.com/QiE2035/LunarWard/releases/tag/{version}` | 仓库地址不同 | — |
| `LogUploadService` 的 app 名 | `Starward` / `Starward.Debug` | `LunarWard` / `LunarWard.Debug` | 品牌 | 上报服务地址与请求结构**未改** |
| `LogUploadClient.DefaultBaseAddress` | `https://starward-log.scighost.com` | 同值（未改） | 上游日志服务仍可接收同结构请求 | 若要自建，改这一处即可 |

## 十三、手柄增强（Features/GamepadControl）

| 位置 | 上游行为 | 本仓库行为 | 原因 | 待办 |
|---|---|---|---|---|
| 输入读取 | `SharpGameInput.V0.IGameInput`（Windows GameInput）：`GetCurrentReading`、`RegisterDeviceCallback`、`RegisterSystemButtonCallback`、`SetFocusPolicy` | **SDL2**（`P/Invoke libSDL2-2.0.so.0`）：`SDL_GameControllerOpen`/`SDL_GameControllerGetButton`/`GetAxis` + `SDL_PollEvent` 翻译热插拔，Guide=`SDL_CONTROLLER_BUTTON_GUIDE`、Share=`MISC1` | 选 SDL2 而不是裸 evdev：SDL 自带覆盖绝大多数手柄的 mapping DB（与 Proton/游戏同一套）、热插拔与 Guide/Share 抽象，语义最接近 `Windows.Gaming.Input`；evdev 需要自己做 udev hwdb + 逐设备适配 | 后台焦点策略（`SetFocusPolicy`）在 SDL 下无等价：SDL 只在窗口有焦点时更新？实测 `SDL_GameControllerGetButton` 不依赖焦点，已按“始终读”实现 |
| 输入注入 | `H.InputSimulator`（Win32 `SendInput`） | **XTest**（`libXtst.so.6` + `libX11.so.6`）：`XTestFakeKeyEvent`/`XTestFakeButtonEvent`/`XTestFakeRelativeMotionEvent`；Win32 VK → X11 keysym 名映射表在 `X11Input.KeysymName` | XTest 是 X11/XWayland 的标准假输入通道，效果最接近 `SendInput` | 原生 Wayland 无全局注入；`libei` 需要合成器授权，未实现 |
| 降级表达 | `NeedInstallGameInputRedist`/`GameInputRedistInstalled`/`GameInputRedistOutdate`（检测 `GameInputRedist.dll`） | `SdlAvailable`/`InputInjectionAvailable`/`UnavailableReason`（可读原因给设置页） | Linux 没有 GameInput 运行时 | 设置页 `GamepadControlSetting` 目前写死“不可用”，接上这三个属性后可动态显示 |
| Xbox Game Bar 开关 | 改注册表 `HKCU\Software\Microsoft\GameBar\UseNexusForGameBarEnabled`、`...\GameDVR\AppCaptureEnabled` | 保留 `Disable/RestoreGamepadGuideButtonForGameBar*`、`Enable/DisbaleXboxGameBarCapture` 全部签名；方法体只写一次 Debug 日志（**明确 No-op，不伪造成功**） | Linux 没有 Xbox Game Bar | 若接 Steam Input，可改为写 Steam 配置 |
| Guide 截图模式（Mode=1） | `ScreenCaptureService.Capture()` | 记录 Warning + `TODO(port)`（Features/Screenshot 由另一移植任务负责） | 依赖尚未移植 | 屏幕捕获落地后接线 |
| 长按 Guide 呼出主窗口 | `MainWindow.Current.ShowByGamepad()` | `WindowEx.MainWindow.Show()/Activate()` | Avalonia 无 `ShowByGamepad`（Windows 下的“从后台唤醒”语义） | Wayland 不允许抢焦点，只能请求激活 |
| `RunningGameService.GetRunningGameCount()` | 真实运行中游戏数（Xbox Game Bar 分支用） | 返回 0（`RunningGameService` 属于 `Features/Overlay`，另一任务） | 作用域隔离 | Game Bar 分支在 Linux 本来无效果 |
| 提示窗口几何 | WinUI Composition `CompositionGeometricClip`（矩形挖圆孔）+ `User32.GetCursorPos`/`ScreenToClient`/`GetDpiForWindow` | Avalonia `CombinedGeometry(Exclude)` + `XQueryPointer` 取全局光标；LT 触发的 600ms KeyFrame 动画 → 16ms 定时器指数缓动 | Avalonia 无 Composition 几何动画 | 原生 Wayland 取不到全局光标，孔固定在窗口中心（Warning 日志） |
| 提示窗口显示方式 | `SW_SHOWNOACTIVATE` + `WS_EX_TRANSPARENT`（不抢焦点 + 点击穿透） | `Show()` + Topmost，**无点击穿透** | Avalonia 未暴露这两个窗口样式位 | 可用 X11 `XShapeCombineRectangles` 补 InputShape（未做） |
| 图标 | `Assets/Font/XboxFluent2.ttf`（E9CD/E992/…） | 统一用项目已有 Segoe Fluent Icons（`TextBlock Classes="icon"`） | 该字体不是本项目的 AvaloniaResource | 若要逐字形对齐，需把 ttf 加进 `Assets` 并注册 `FontFamily` |

## 十四、URL 协议（Features/UrlProtocol）

| 位置 | 上游行为 | 本仓库行为 | 原因 | 待办 |
|---|---|---|---|---|
| 注册 | 写 `HKCU\Software\Classes\starward`（`URL Protocol` + `shell\open\command`） | `UrlProtocolService.RegisterProtocol()` 写 `~/.local/share/applications/lunarward-url-protocol.desktop`（`Exec=<exe> %u`、`MimeType=x-scheme-handler/lunarward;`、`NoDisplay=true`），随后尽力跑 `update-desktop-database` 与 `xdg-mime default` | Linux 用 XDG .desktop + MIME 关联 | 需要 `xdg-utils`；失败时返回 false 并日志 |
| 注销 | 删注册表键 | 删 .desktop 文件 + 清 `~/.config/mimeapps.list` 的 `[Default Applications]` 中对应项 | 同上 | 同上 |
| scheme | `starward://` | `lunarward://`（`UrlProtocolService.Scheme` 常量） | 品牌 | 若要与上游共存可同时注册两个 scheme |
| 处理入口 | `App.OnLaunched` 解析激活参数 → 各功能 | `HandleUrlProtocolAsync` → `StartupCommands.RunAsync`（startgame/playtime），`test` 打开测试窗口 | 本仓库命令行入口已集中 | 新增协议动作时两处都要加 |

## 十五、抽卡与云游戏（2026-10-26，gacha-port / task-8）

对应上游 `Features/Gacha/`（40 文件）、`Features/CloudGame/`（3 文件）、`Features/SelfQuery/`（4 文件）。逐文件、逐成员移植；下列差异只出现在**无法等价**的 WinUI/Windows 能力处，其余部分（类名、成员名、SQL、导出格式、UIGF/UIAF 结构、卡池表）与上游逐字一致。

| 位置 | 上游行为 | 本仓库行为 | 原因 | 待办 |
|---|---|---|---|---|
| `IValueConverter` 签名（`RarityToGIBgConverter`、`RarityToSRBgConverter`、`RarityToZZZIconConverter`、`GachaPityProgressBackgroundBrushConverter`、`GenshinBeyondGachaPityProgressBackgroundBrushConverter`、`SelfQueryStatsNumberConverter`、`SelfQueryStatsNumberBrushConverter`） | `object Convert(object value, Type targetType, object parameter, string language)` | Avalonia 版 `object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)`，`ConvertBack` 抛 `NotImplementedException` | 两个框架的 `IValueConverter` 定义不同 | — |
| 稀有度图片路径 | `ms-appx:///Assets/Image/Rarity_5_Background.png` 等 | 字符串**保持上游原样**，由 `Controls/CachedImage.cs` 的 `ToAssetUri` 转 `avares://LunarWard/Assets/...` | `Starward.Assets` 包的 png 已进程序集 avares 资源 | — |
| 渐变画刷端点 | WinUI `Point(0,0)` / `Point(1,0)` | `new RelativePoint(0, 0, RelativeUnit.Relative)` / `(1, 0)` | Avalonia 用 `RelativePoint` | — |
| `GachaLogPage` 进度 + 取消 | `new InfoBar{...}` + `InAppToast.MainWindow?.Show(infoBar)`（可更新标题、带取消按钮） | `GachaProgressToast : Border`（`Features/Gacha/GachaProgressToast.cs`）：自绘标题/消息/取消按钮，`InAppToast.MainWindow.Children` 里增删，取消时 `CancellationTokenSource.Cancel()` | Avalonia 无 `InfoBar`，`Helpers/ToastItem` 的 `Message` 是 `init`-only 不能更新 | 若日后 Avalonia 提供等价控件可回退 |
| `GachaLogPage.xaml` 的 `SplitButton` 菜单 | `SplitButton.Flyout` + `MenuFlyoutItem` | Avalonia `SplitButton`+`MenuFlyout`；`MenuFlyoutItem`→`MenuItem`、`Text`→`Header` | 控件名不同 | — |
| `GachaLogPage.xaml` 的 `Pivot`/`Segmented`（统计卡 5★/4★ 切换） | `Pivot` + `CommunityToolkit.WinUI.Controls.Segmented` | `TabControl`（隐藏 `ItemsPresenter#PART_ItemsPresenter` 当容器）+ 两个 `ToggleButton Classes="segment"`，代码后置双向同步 | Avalonia 无 Pivot/Segmented | — |
| `GachaStatsCard` 的 `TextBlock.IsTextTrimmedChanged` | 事件 `IsTextTrimmedChanged` + `ResetGachaTypeTextFontSize()` | `ResetGachaTypeTextFontSize()` 保留，事件处理删除（上游页面从不调用二者） | Avalonia `TextBlock` 无 `IsTextTrimmedChanged` | 无（死代码） |
| `GridView`（材料网格、ZZZ 图鉴语言列表） | `GridView` + `ItemsWrapGrid`，`SelectionMode=Multiple` | `ItemsControl`+`WrapPanel`（只读网格）；`ListBox`+`WrapPanel`（多选） | Avalonia 只有 `ListBox`/`ItemsControl` | — |
| `x:Load` 延迟实例化 | `x:Load="{x:Bind EnableXxx}"` | `IsVisible`（元素常驻），集合空时不渲染项目 | Avalonia 无 `x:Load` | — |
| `Image.Source=BitmapImage` | `new BitmapImage(new Uri("ms-appx:///..."))` | `new Bitmap(path)`（`Assets/...` 本地路径） | Avalonia 无 `BitmapImage` | — |
| 打开文件夹并选中文件 | `StorageFolder.GetFolderFromPathAsync` + `FolderLauncherOptions.ItemsToSelect` + `Launcher.LaunchFolderAsync` | `Process.Start("xdg-open", 目录)`（导出后打开所在目录；不选中具体文件） | XDG 无“在文件管理器中选中”标准接口 | 可检测 `dbus` FileManager1 `ShowItems` 补上 |
| 保存/选择文件 | `FileDialogHelper.OpenSaveFileDialogAsync(XamlRoot, ...)` | `Helpers/FileDialogHelper.OpenSaveFileDialogAsync(this, ...)`（`TopLevel.StorageProvider`） | Avalonia 用 `TopLevel.StorageProvider` | — |
| 应用内对话框 | `new ContentDialog{...}.ShowAsync()` | `new StarwardContentDialog{...}` + `await DialogService.ShowAsync(dialog, this)`，`dialog.PrimaryButtonClicked += ...` 取结果 | Avalonia 无 `ContentDialog` | — |
| 应用内提示 | `InAppToast.MainWindow?.Success(title, message, 5000)` | 同签名（`Helpers/InAppToast.cs` 已按上游形态移植） | — | — |
| 窗口标题栏 | `AppWindow.TitleBar.ExtendsContentIntoTitleBar` + `DesktopAcrylicBackdrop` | 由窗口管理器绘制；`AdaptTitleBarButtonColorToActuallTheme()` 是空实现 | Avalonia 无 AppWindow API | — |
| 主题判定 | `RootGrid.RequestedTheme = ShouldAppsUseDarkMode() ? Dark : Light` | `RootGrid.RequestedThemeVariant = ... ThemeVariant.Dark : ThemeVariant.Light` | 属性名不同 | — |
| `CloudGameGachaWindow` 的 `WebView2` | `Microsoft.Web.WebView2.CoreWebView2`（`EnsureCoreWebView2Async`、`ExecuteScriptAsync`、`Source`） | `Frameworks/WebView/WebView.cs`（WebKitGTK 4.1），同形 API：`EnsureCoreWebView2Async()`、`CoreWebView2.ExecuteScriptAsync(string)`、`Source` | Linux 无 WebView2（见 §五） | 与 §五 的已知缺口一致 |
| `CloudGameGachaWindow` 的 `HttpUtility.HtmlDecode` | `System.Web.HttpUtility`（.NET Framework 命名空间） | 保留 `using System.Web;`，.NET 10 由 `System.Web.HttpUtility`（BCL 内置）提供 | — | — |
| `CloudGameButton.Flyout_Opened` 读注册表 | `HKCU\...\Uninstall\GenshinImpactCloudGame` 的 `DisplayIcon`；`HKLM\...\Genshin Impact Cloud` 的 `InstallPath`+`ExeName`；`HKCU\...\ZenlessZoneZeroCloud` 的 `DisplayIcon`；`Process.GetProcessesByName` + `SessionId` | 保留 `Microsoft.Win32.Registry` 调用（`~/.wine` 注册表由 `Services/WineRegistry.cs` 语义承载）；`SessionId` 判同会话在 Linux 恒为同一会话 | Wine 下云游戏本体可运行，注册表键由 Wine 前缀写入 | 若云游戏不通过 Wine 安装，改为扫描 `~/.local/share/applications` 的 .desktop `Exec=` |
| `CloudGameButton.StartGameAsync` | `Process.Start(exe)` → `RunningGameService.AddRuninngGame` → `GameStartedMessage` → 3 秒后拉 `lunarward playtime --biz ... --pid ...` | 同上（`RunningGameService` 属 `Features/Overlay`，由 dapper-db 移植；本簇文件按其落地后的签名接线） | — | 待 `Features/Overlay` 落地后编译验证 |
| `CloudGameButton.InstallGameAsync` | `Launcher.LaunchUriAsync(new Uri(url))` | `Process.Start(new ProcessStartInfo{FileName=url, UseShellExecute=true})`（走 `xdg-open`） | Avalonia `Launcher` 等价物 | — |
| `ZZZGachaInfoWindow` 取响应请求头 | `args.Request.Uri` + `args.Request.Headers`（`GetHeader("Cookie")`、`ToList()`，含 `x-rpc-lang`） | `Frameworks/WebView` 的 `CoreWebView2WebResourceResponseReceivedEventArgs` 只暴露 `Uri` 与 `Response`；请求头改由 `CoreWebView2.CookieManager.GetCookiesAsync(origin)` 重组 **Cookie** 一项 | WebKitGTK 的资源响应回调不带请求头（`WebKitWebViewCore.cs:259` 的 `ResourceResponseReceived` 只有 uri/status/contentType/body） | `x-rpc-lang` 因此拿不到：`GetAllLanguagesInfoAsync` 里对它的替换不生效（Cookie 的 `mi18nLang` 替换仍生效）。要对齐需扩 `Frameworks/WebView`（非本簇作用域） |
| `ZZZGachaInfoWindow` 的语言列表多选 | `GridView SelectionMode=Multiple` + `SelectionChanged` 读写 `SelectedItems` | `ListBox SelectionMode="Multiple"`，`SelectedItems` 语义相同 | 控件名不同 | — |
| `ZZZGachaInfoWindow` 的外链 | `Hyperlink NavigateUri` | `HyperlinkButton Content/NavigateUri` | 控件名不同 | — |
| `ZZZGachaInfoWindow` 的 `Launcher.LaunchUriAsync(new Uri(folder))` | 资源管理器打开导出目录 | `Process.Start("xdg-open", folder)` | 同前 | — |
| `SelfQueryPage` 的 `ProgressRing` | `ProgressRing Width=24 Height=24` | `ProgressBar IsIndeterminate="True" Width=24 Height=4` | Avalonia 无 `ProgressRing` | 可自绘圆环 |
| `SelfQueryPage` 的只读列表 | `ListView SelectionMode="None"` | `ListBox` + `ItemContainer` 的 `IsHitTestVisible=False`、`Background=Transparent` | Avalonia 无 `SelectionMode=None` | — |
| `SelfQueryStatsNumberBrushConverter` | `Application.Current.Resources["SystemFillColorSuccessBrush"/"SystemFillColorCriticalBrush"]` | `StarSuccessBrush`(#FF6CCB5F) / `StarErrorBrush`(#FFFF99A4) / `Brushes.Transparent` | 资源键不同 | — |
| `GachaLogItem` 的 `IsUp`（Core 模型放置差异） | 上游 `Core.Gacha.GachaLogItem` 只有 `[JsonPropertyName("is_up")]` 的 int 字段，UP 标记在 `GachaLogItemEx` 上计算 | `LunarWard.Core/Gacha/GachaLogItem.cs:53` 保留 `public int IsUp`（接口原始值）；`GachaLogItemEx.IsUp` 为 `public new bool IsUp`（派生标记），`new` 消 CS0108 | 本地 `GachaLogItem` 多存一份原始字段，`src/LunarWard.Launch` 仍有 `item.IsUp == 1` 用法 | endgame 阶段随旧服务层一起回退 |
| `GachaLogItemEx` 的 `IsUp` | `public bool IsUp` | `public new bool IsUp`（同上） | 同上 | 同上 |
| `GachaLogPage`/`GenshinBeyondGachaPage`/`SelfQueryPage` 的旧实现 | 上游只有 `Features/` 下这一份 | 旧 `Views/Starward/Pages/{GachaPage,GenshinBeyondGachaPage,SelfQueryPage}` 与 `ViewModels/Starward/*` 暂留（导航仍指向旧页），新页面在 `Features/` 下同形态就绪 | 外壳/导航收口由 lead 统一处理 | 切换导航后删除旧页与旧 ViewModel |
| `GachaLogPage` 的 `OnNavigatedTo` 参数 | 上游按 `CurrentGameBiz` | 同上游；`PageBase.OnNavigatedTo` 接受 `GameId` 或 `GameBiz` | — | — |
| `GachaLogPage.xaml` 的 `FontIcon` 字形（复制成功 √ / 复制图标） | `FontIcon Glyph="&#xE8FB;"` | `TextBlock Classes="icon" Text="&#xE8FB;"`（`Styles/Starward.axaml:133`，`StarIconFontFamily`） | Avalonia 无 `FontIcon` | — |
| `GachaLogPage.xaml` 的 `InfoBar` 面板 | `InfoBar` | 见本表 `GachaLogPage` 进度行（`GachaProgressToast`） | — | — |

## 十六、Features/GameRecord 移植（2026-10-26，webview-webkit / task-5）

上游 `src/Starward/Features/GameRecord/` 共 47 个文件（15724 行），逐个搬到 `src/LunarWard/Features/GameRecord/`（含 `DailyNote/`、`Genshin/`、`StarRail/`、`ZZZ/`），文件名/类型名/成员名/事件名保持不变。`GameRecordService.cs`（1433 行）**未改一行逻辑**：它的 `AppConfig.GetLogger<T>()` / `AppConfig.GetService<T>()` / `DatabaseService.CreateConnection()` / Dapper 裸 SQL / `Lang.*` 在本仓库全部已就绪（DI 注册见 `AppConfig.ServiceProvider.cs`）。

| 位置 | 上游行为 | 本仓库行为 | 原因 | 待办 |
| --- | --- | --- | --- | --- |
| `AvatarRankHelper.RankToVisibility(int)` | 返回 `Microsoft.UI.Xaml.Visibility`（`rank > 0 ? Visible : Collapsed`） | 返回 `bool`（`rank > 0`），调用方写 `IsVisible` | Avalonia 用 `IsVisible` 布尔值，没有 `Visibility` 枚举 | — |
| 各 `*Converter.cs`（`ColorToSolidColorBrushConverter`、`SpiralAbyssBattleAvatarsSelectConverter`、`TheaterStarIconVisibilityConverter`、4 个 `SimulatedUniverse*Converter`、`ZZZRarityToIconConverter`） | `IValueConverter.Convert(object value, Type targetType, object parameter, string language)` | `object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)`（`ConvertBack` 同签名） | Avalonia 转换器签名不同（无 `language`，末参是 `CultureInfo`） | — |
| 所有页面的 `OnNavigatedTo(NavigationEventArgs e)` | `protected override void OnNavigatedTo(NavigationEventArgs e)`，参数在 `e.Parameter` | `public override void OnNavigatedTo(object? e)`，参数直接是 `e`（`PageBase` 是 `public virtual`） | Avalonia 没有 `Frame`/`NavigationEventArgs`，`PageBase` 自己定义了同名钩子 | 外壳传到导航参数上时保持 `Activator.CreateInstance` + `page.OnNavigatedTo(role)` |
| `[ObservableProperty]` / `[INotifyPropertyChanged]`（31 处） | MVVM Toolkit 生成属性与 `OnXChanged` 分部方法 | 属性手写在 `.axaml.cs` 里；另加 `Features/GameRecord/ObservableSupport.cs`，为 17 个类各生成一个 partial 片段**显式再实现** `INotifyPropertyChanged`（`new event`，Avalonia 绑定走新事件）+ 提供 `SetProperty<T>(ref T, T, [CallerMemberName])`/`OnPropertyChanged(string?)` 形状 | 基类 `PageBase`/`AvaloniaObject` 已实现 `INotifyPropertyChanged`：`[INotifyPropertyChanged]` 报 MVVMTK0001；`[ObservableProperty]` 在派生类上不再生成 `On<Prop>Changed` 声明（手写实现报 CS0759）。用 partial 片段把差异集中到 1 个文件，页面本体保持上游形状 | 后续新页面同样在 `ObservableSupport.cs` 里加一段；若哪天 `PageBase` 换成非 `AvaloniaObject` 基类，可整体删掉这个文件 |
| `BitmapImage` + `SetSourceAsync(stream.AsRandomAccessStream())` | WinUI 图片，源为 `ms-appx:///...` 或网络流 | `new Avalonia.Media.Imaging.Bitmap(stream)`；`ms-appx:///Assets/...` 走 `GameRecordAssets.FromMsAppx()`（`AppContext.BaseDirectory` + 相对路径） | Avalonia 无 `BitmapImage`；`Starward.Assets` 内容包把图片铺到输出目录 | `Controls/CachedImage` 落地后优先换回（带缩略图缓存） |
| XAML 里的 `Source="ms-appx:///Assets/Image/x.png"` | WinUI 包 URI | `Source="avares://LunarWard/Assets/Image/x.png"`（`Assets/**` 已声明为 `AvaloniaResource`） | Avalonia 资源 URI 方案名是 `avares` | — |
| `KeyRoutedEventArgs` / `Windows.System.VirtualKey.Enter` | WinUI 键盘事件 | `Avalonia.Input.KeyEventArgs` / `Key.Enter` | 类型名不同 | — |
| `TextBlock.IsTextTrimmedChanged`（3 处） | 文本截断时机事件 | 事件处理删除 | Avalonia 无该事件 | 无（上游也没什么逻辑） |
| `ColorRectChart.ChartLegend`（3 处页面 6 处用法） | 嵌套类型 | 顶层 `LunarWard.Controls.ChartLegend`（`record struct`） | 本仓库移植时提为顶层类型 | — |
| `NavigationView` + `NavigationViewItem` + `ItemInvoked`（`GameRecordPage`） | NuGet/系统导航控件，左侧 Pane，`ItemInvoked` 里按 `Tag` 选页；两个特殊项（战斗记录、更新设备指纹）用 `Tapped` 事件以免焦点回收 | `SplitView`（`DisplayMode=Inline`、`IsPaneOpen`、`PanePlacement=Left`）+ Pane 内 `Grid RowDefinitions="Auto,*,Auto"`（头像区 / `ListBox x:Name="ListBox_Toolbox"` / 底部按钮）+ `SelectionChanged` 里按 `(item.Tag as string) switch` 选页；两个特殊项改成 `Button` + `Click` | Avalonia 没有 `NavigationView`；`SplitView` 是等价的双栏骨架 | 导航切页时若需要「选中态」，在 `ListBox` 上补 `SelectedItem` 同步 |
| `Frame`（`Frame x:Name="frame"` / `frame.Navigate(page, parameter)` / `frame.SourcePageType`） | WinUI 导航栈 | `<fw:Frame x:Name="Frame_Content"/>`（`LunarWard.Frameworks.Frame`，由外壳同一份实现提供）：`Frame_Content.Navigate(pageType, parameter)` 内部实例化 → 设置 `Content` → 调 `PageBase.OnNavigatedTo`；页面类型从 `Frame.Content` 反查 | Avalonia 无 `Frame`，本仓库在 `Frameworks/` 里提供了同名等价控件（与外壳 `MainView` 共用，避免页面级两套导航） | 需要返回栈时给 `Frameworks.Frame` 补 `Stack<Type>` |
| 协议对话框（`GameRecordPage` 的 5 秒倒计时） | `ContentDialog.ShowAsync()` + `PrimaryButtonText` 倒计时 + `ContentDialogResult` | `new StarwardContentDialog{...}` + `DialogService.ShowAsync(dialog, this)`；结果由 `dialog.PrimaryButtonClicked` 置 `accepted`；倒计时循环里用 `DialogService.IsDialogOpen` 判断是否已被用户关闭 | Avalonia 无 `ContentDialog`；本仓库 `DialogService` 是等价物 | — |
| 战斗记录窗口的生命周期判断 | `_battleChronicleWindow?.AppWindow is null` 视为已关闭 | `Closed` 事件里把字段置空，`Show()`/`Activate()` 二选一 | Avalonia 无 `AppWindow` | — |
| `BattleChronicleWindow` 的标题栏/主题/居中 | `AppWindow.TitleBar.ExtendsContentIntoTitleBar`、`RootGrid.RequestedTheme`、`DisplayArea` + `AppWindow.MoveAndResize` | `ExtendClientAreaToDecorationsHint = true`、`RootGrid.RequestedThemeVariant = ThemeVariant.Dark/Light`、`Screens.ScreenFromWindow(this).WorkingArea` 算 95% 高 16:9 再 `WindowEx.CenterInScreen(w,h)` | 属性名/API 不同 | — |
| `WebView2`（`LoginPage`、`BBSWebBridge`） | `Microsoft.Web.WebView2` 控件 | `LunarWard.Frameworks.WebView.WebView`（WebKitGTK 4.1，见 §五） | Linux 无 WebView2 | 见下表「WebView 已知缺口」 |
| `FontIcon Glyph="&#xE72C;"` | WinUI 图标控件 | `<fw:FontIcon Glyph="&#xE72C;" Classes="icon"/>`（`LunarWard.Frameworks.FontIcon : TextBlock`，`Glyph` 是 StyledProperty，ctor 里设 `StarIconFontFamily`/`FontSize=16`，`OnPropertyChanged` 同步 `Text`） | Avalonia 无 `FontIcon`；图标字形是 Segoe Fluent Icons 私用区码位，必须走图标字体而不是 `PathIcon` | — |
| `ProgressRing` | 环形进度 | `ProgressBar IsIndeterminate="True"` | Avalonia 无 `ProgressRing` | 可自绘圆环 |
| `ItemsRepeater` + `ItemsRepeater.Layout` + `StackLayout` | 虚拟化列表 | `ItemsControl`（默认 `StackPanel`），`Layout` 块删除 | Avalonia 用 `ItemsControl`/`ItemsRepeater`（社区包未引） | 大列表需要虚拟化时换 `ItemsRepeater` 社区包 |
| `x:Load` / `x:Phase` | 延迟实例化 | 元素常驻 + `IsVisible` | Avalonia 无 `x:Load` | — |
| `Grid` 上的 `Padding`/`CornerRadius`/`Background`/`BorderThickness` | WinUI `Grid` 支持这些属性 | 属性删除；需要背景/圆角时外包 `Border` | Avalonia `Grid` 没有这些属性 | 视觉细节待 UI 对齐时补 `Border` |
| `{ThemeResource X}` / `{StaticResource X}`（WinUI 资源键） | WinUI 主题资源 | `{DynamicResource X}`（同文件内定义的 `x:Key` 保留 `StaticResource`） | Avalonia 里 `DynamicResource` 找不到键只留空值，不会抛异常 | 想还原配色时把键映射到 `Styles/Starward.axaml` 的 `Star*` 资源 |
| 命名空间泄漏（验收脚本） | `using Microsoft.UI.Windowing;` / `Windows.Graphics;` / `Microsoft.UI;` | 删除，改 Avalonia 等价物 | 迁移规则要求不残留 WinUI 命名空间 | — |

### 全量重建（`-t:Rebuild`）后的 XAML 机械替换

增量构建会跳过未改动 `.axaml` 的 Avalonia XAML 编译，所以「增量 0 error」不可信；下面这些只有在全量重建时才会暴露，已按 `docs/16-XAML-MAPPING.md` 全部落地（提交 `cc5fecd`）：

| WinUI 写法 | 本仓库写法 | 原因 |
| --- | --- | --- |
| `{x:Bind X}` / `{x:Bind X, Converter=…}` | 根节点加 `x:CompileBindings="False"`，保持 `{Binding X}` 原样 | Avalonia 默认对 `{Binding}` 做编译绑定，需要 `x:DataType`；上游页面没有 `x:DataType`，关掉编译绑定等价于 WinUI 的运行时 `{Binding}` 语义 |
| `{x:Bind lang:Lang.Key}` / 其他 `{x:Bind Type.Member}` 静态成员 | `{x:Static lang:Lang.Key}` | 静态成员不是绑定路径，Avalonia 用 `x:Static` |
| `{x:Bind sfgr:AvatarRankHelper.RankToVisibility(Rank)}` | `IsVisible="{Binding Rank, Converter={StaticResource RankToVisibilityConverter}}"`（`Features/GameRecord/GameRecordConverters.cs`） | Avalonia 绑定表达式不能调用方法 |
| `{x:Bind linq:Enumerable.Range(0, N)}` | `ItemsSource="{Binding N, Converter={StaticResource RangeConverter}}"` | 同上；转换器生成 `0..N-1` 与 `Enumerable.Range` 一致 |
| `Converter={DynamicResource XxxConverter}` | `Converter={StaticResource XxxConverter}` | 转换器在 `App.axaml` 资源字典里，`StaticResource` 才能拿到实例 |
| `<BitmapImage x:Key="K" Source="ms-appx:///…"/>` + `{StaticResource K}` | 资源声明删除，引用处内联 `avares://LunarWard/Assets/…` | Avalonia 没有可在 XAML 里构造的 `BitmapImage`，图片是 `IImage` 资源 URI |
| `MenuFlyoutItem` / `MenuFlyoutSeparator` / `AppBarSeparator` / `MenuFlyoutItem.Icon` | `MenuItem` / `Separator` / `MenuItem.Icon` | Avalonia 只有 `MenuItem` |
| `Grid.Translation="x,y,z"` | `RenderTransform="translate(x,y)"` | Avalonia 没有 `Translation` 属性 |
| `TextBlock.IsTextScaleFactorEnabled`、`ScrollViewer.HorizontalScrollMode/VerticalScrollMode/ZoomMode` | 删除 | 无等价物（系统缩放跟随平台） |
| `StackPanel.VerticalSpacing` / `HorizontalSpacing` | `Spacing` | 属性名不同 |

### WebView 已知缺口（移植窗口时补）

| 缺口 | 上游用法 | 本仓库现状 | 影响的窗口 |
| --- | --- | --- | --- |
| 导航拦截 | `NavigationStarting` 里设 `args.Cancel = true` 拦站外跳转（**上游目前没有窗口真的这么做**，`LoginPage` 只用该事件换图标；全树 `args.Cancel` 只出现在 `GameAccountSwitcher`/`MainWindow` 的非 WebView 事件里） | `CoreWebView2NavigationStartingEventArgs.Cancel` 只记录 + 告警，不生效；真需要拦截时在 `WebKitWebViewCore` 的 `decide-policy` 信号里拒绝 `NAVIGATION_ACTION` 即可（互操作层已连上该信号） | 暂无人使用；`LoginPage` 行为与上游一致 |
| 资源嗅探只认 JSON | `WebResourceResponseReceived` 读任意响应体 | 只有 URI 含 `.json` 的资源会被抓取并提供 `GetContentAsync()` | `ZZZGachaInfoWindow` |
| 请求头 | `args.Request.Headers.GetHeader("Cookie")` / `x-rpc-lang` | 资源响应回调不带请求头，只能用 `CookieManager.GetCookiesAsync(origin)` 重组 Cookie | `ZZZGachaInfoWindow` |
| DevTools 协议 | `CallDevToolsProtocolMethodAsync("Page.captureScreenshot", ...)` | 只实现 `Page.captureScreenshot`（走 `webkit_web_view_get_snapshot`），其它方法抛 `PlatformNotSupportedException` | `BBSWebBridge`（截图分享功能已可用） |
| 深浅色 | `Profile.PreferredColorScheme = Dark/Light` | WebKitGTK 无同款 API，改设 GTK `gtk-application-prefer-dark-theme`；页面自身 `prefers-color-scheme` 跟随系统 | `UpdateWindow`、`ZZZGachaInfoWindow` |
| 虚拟主机映射 | `SetVirtualHostNameToFolderMapping` | 用自定义 scheme `appassets://<host>/<path>` + 主框架 `https://<host>/…` 重写；子资源里的绝对 https URL 不改写 | `LoginPage`（若后续要托管本地页面） |
| 新窗口 | `NewWindowRequested` 里 `Handled = true` 后外部打开 | 门面只抛事件（WebKit 侧用 `create` 信号挡掉弹窗），外部打开由调用方做 | `UpdateWindow` |

### 待办

- **运行验收（2026-10-26 已完成，树绿后）**：
  - 单测：`dotnet test tests/LunarWard.UnitTests --filter "FullyQualifiedName~GameRecord"` → 9/9 通过。
  - 页面渲染：`GameRecordPage` 在真实 `App`（`AppBuilder.Configure<LunarWard.App>()` + `AppConfig.CheckEnviromentAsync()`）里实例化并渲染，左侧 Pane 完整渲染出「登录」入口、13 个工具箱项（旅行者札记/深境螺旋/幻想真境剧诗/幽境危战/开拓日历/模拟宇宙/忘却之庭/虚构叙事/末日幻影/异相仲裁/绸网月报/式舆防卫战/危局强袭战）与「更新设备指纹」，证据 `artifacts/gamerecord/gamerecord-page.png`（1200x700）；`LoginPage` 渲染出工具条（前进/后退/刷新/地址栏/完成）与 WebView 宿主，证据 `artifacts/gamerecord/login-page.png`。
  - **自检路由已切新壳**：`--screenshot --page record` 现在直达 `Features/GameRecord/GameRecordPage`（`MainView.NavigateToForCapture` 直接 `MainView_Frame.Navigate(page)`，绕过 `CurrentGameFeatureConfig.SupportedPages` 判定；本机未安装游戏也能落游戏页）。实测 `dotnet run --project src/LunarWard -- --screenshot artifacts/gamerecord/record.png --page record --wait 800` → `SCREENSHOT_OK artifacts/gamerecord/record.png 1200x676`，画面为 NavigationView + 战绩工具箱（13 项）+「免责声明」`StarwardContentDialog`（含 5s 倒计时「接受」与「拒绝」按钮），说明 `StarwardContentDialog` + `DialogService` 链路在 Linux 上等价可用。
  - **历史限制（已被上面的路由修复解决）**：`dotnet run -- --screenshot <png> --page record` 在新外壳里会被 `MainView.NavigateTo` 的 `CurrentGameFeatureConfig.SupportedPages` 判定拦下并回落到启动器页（本机未安装游戏 → `CurrentGameId == null` → `GameFeatureConfig.Default` 只支持 `GameLauncherPage`），截图出来是启动器（`artifacts/gamerecord/record.png`）。要截到战绩页本体，需给自检入口注入 `CurrentGameId`（或在 `NavigateToForCapture` 里跳过该判定），属外壳 self-check 的后续项。
  - 另注：XID 嵌入的 WebView 是原生子窗口，`RenderTargetBitmap` 抓不到它的内容（WebView2 同样如此），所以上面的 LoginPage 截图里 WebView 区域是空的；WebView 画面本身的渲染证据见 §五 的 `artifacts/webview-selftest/avalonia-embed.png`。
- 旧架构 `src/LunarWard/Views/Starward/Pages/Record/*` + `ViewModels/Starward/*` 暂留（导航仍指向旧页），由 lead 在外壳收口时删除。
- `GameRecordService` 里若有需要 `AppConfig` 新配置项的路径，随 `AppConfig` 落地回填。

---

## 十一、Features/GameSetting（2026-10-26，settings-migrate / task-11）

上游 `src/Starward/Features/GameSetting/`（7 文件）在本仓库已同名落点，本轮把「页面未接线 + 旧 VM 三件套仍在服役」的偏移收口：

| 项 | 上游 | 本仓库 | 原因 |
|---|---|---|---|
| 页面基类 | `GameSettingPage : PageBase`（WinUI `Page`，Frame 负责 `OnNavigatedTo(GameId)`） | `GameSettingPage : StarwardPage`（ctor `PageContext`，无参 `OnNavigatedTo()`） | 宿主 `StarwardShell.CreatePage` 返回 `StarwardPage`；本仓库 `Frameworks/PageBase` 是 WinUI PageBase 的 Avalonia 等价物但**不是**宿主页基类。页面内部用私有 `CurrentGameId`/`CurrentGameBiz` 字段复刻上游 PageBase 语义（从 `Context.Profile.Profile` 经 `LauncherService.BuildGameInfo` 推导，`bh3_global` → 区域映射保留） |
| 初始化时机 | `PageBase.Loaded` → `InitializeGameSettingAsync` | 首次 `OnNavigatedTo()` 后 `InitializeAsync()`（`_initialized` 守卫，页缓存不重复初始化） | Avalonia 无 Frame 生命周期；宿主每次切页都会调 `OnNavigatedTo`，需与「只加载一次」对齐 |
| 绑定根 | `x:Bind`（编译期绑定页面自身） | `DataContext = this` + `{Binding}`（XAML 不改绑定名） | Avalonia 编译绑定需要 `x:DataType`；上游这个页面是 `x:Bind` 直连 code-behind |
| 屏幕分辨率 | `DisplayArea` | `TopLevel.Screens.Primary`（宽度/高度过滤 17 个常见分辨率） | 无 WinRT |
| HDR | `DisplayInformation` + `AdvancedColorInfoChanged` | HDR 一律「不支持」（`HDRNotSupported`，不显示警示条） | Linux 无 WinRT 高级颜色 API（同 §七 #25） |
| 语音/画质写入 | `GameSettingService` 直接改注册表 | 同名 `GameSettingService`，注册表走 `WineRegistry`（Wine 子进程） | 见 §七 表格 #10 |

**同批删除（消灭本地发明）**：`Views/Starward/Pages/GameSettingPage.axaml(.cs)`（48 KB，`x:DataType=vm:GameSettingPageViewModel`）、`ViewModels/Starward/GameSettingPageViewModel.cs`（1672 行）、`GameSettingProgramViewModel.cs`（585 行）、`GameSettingOptions.cs`。宿主 `StarwardShell.CreatePage` 的 `ShellPage.GameSetting` 分支改为 `Features.GameSetting.GameSettingPage`。
`Services/GameSettingRegistry.cs` 保留（单测 `WineRegistryTests` 覆盖其注册表键映射，与页面无关）。
**未移植（明确缺口）**：无（上游 7 个文件全部有同名落点）。

---

## 十二、Features/GameSelector、GameLauncherPage、GameLauncherSettingDialog、GameInstall 对话框（2026-10-26，launcher-port / task-7）

上游这四组 UI 在本仓库已同名落点（`Features/GameSelector/`、`Features/GameLauncher/`、`Features/GameInstall/`）。逐文件对应的平台替换如下（服务层见 §七）：

### 12.1 GameSelector（`GameSelector.xaml` 572 行 → `.axaml`）

| 项 | 上游 | 本仓库 | 原因 |
|---|---|---|---|
| 窗口尺寸变化监听 | `XamlRoot.Changed` + `XamlRoot.RasterizationScale` | `TopLevel.GetTopLevel(this).ScalingChanged` + `ILayoutRoot.RenderScaling` | Avalonia 无 XamlRoot |
| 标题栏拖拽区 | `XamlRoot.SetWindowDragRectangles(...)` | 空实现 + `WeakReferenceMessenger.Send(new MainWindowDragRectAdaptToGameIconMessage(false))` | 拖拽区由 `WindowEx.SetDragRectangles` 在宿主统一设置（§七 #7） |
| 图标区收起动画 | `Grid.Translation`（Vector3）+ OpacityTransition | `RenderTransform = new TranslateTransform(0,-100)` + `Opacity` + `IsHitTestVisible` | Avalonia 无 `Translation` 属性/Vector3 |
| 服务器弹出菜单 | `FlyoutBase.GetAttachedFlyout(ele)?.ShowAt(...)` + `VisualTreeHelper.GetOpenPopupsForXamlRoot` 关闭 | `FlyoutBase.ShowAttachedFlyout(ele)` + 自持 `_currentFlyout?.Hide()` | 无 RasterizationScale/XamlRoot 弹窗枚举 |
| 自动搜索已安装游戏 | 读 `HKCU\Software\miHoYo\HYP\1_1\{biz}` / `Cognosphere\HYP\1_0\{biz}` 的 `GameInstallPath` | `GameLauncherService.GetGameInstallPath(biz)` + `AppConfig.GetGameInstallPathRemovable` | Linux 无 HYP 注册表；安装路径由本仓库配置承载 |
| 硬链接占用去重 | `Kernel32.GetFileInformationByHandleEx<FILE_ID_INFO>` | `FileIdentity`（`libc stat` 的 `st_dev`/`st_ino`） | 无 Win32 API（§七 #8） |
| `TeachingTip` | CommunityToolkit `TeachingTip` | `Popup` + Border/文本/按钮 | Avalonia 无 TeachingTip |
| `mxi:Interaction.Behaviors` + Storyboard | PointerEntered/Exited 触发的 opacity 动画 | 删除（悬停过渡改用 Avalonia 样式自带反馈） | 无 Interactivity/Storyboard 触发器 |
| `sc:CachedImage` | Starward.Controls.CachedImage | `Image`（TODO：task-4 的 CachedImage 落地后换回） | 控件归 task-4 |

### 12.2 GameLauncherPage（`GameLauncherPage.xaml` 45 KB → `.axaml`）

| 项 | 上游 | 本仓库 | 原因 |
|---|---|---|---|
| 页面基类 | `PageBase`（WinUI Page） | `LunarWard.Frameworks.PageBase`（同名同语义） | 见文档 §一 |
| 未移植控件 | `sfp:PlayTimeButton`、`sfc:CloudGameButton`、`sfgd:DailyNoteButton` | 整块注释 + `<!-- TODO(跨任务) -->` 占位 | 分属 Features/PlayTime、CloudGame、GameRecord/DailyNote 三个并行任务，尚未落点 |
| 超链接 | `Hyperlink`（NavigateUri） | `Button Classes="link"` + `Click` → `TopLevel.Launcher.LaunchUriAsync` | Avalonia 无 Hyperlink 控件 |
| 定时器 | `DispatcherQueueTimer` | `DispatcherTimer` | 命名差异 |
| `ObservableProperty` 分部属性 | MVVM Toolkit 源生成 | 手写字段 + `SetProperty` + `OnXxxChanged` | Avalonia UserControl/PageBase 已实现 `INotifyPropertyChanged`，源生成器报 `MVVMTK0001` |
| 紧凑复选框 | `CompactCheckBoxStyle.xaml`（375 行整份 CheckBox 模板 + VisualStateManager + AnimatedIcon） | `Features/GameLauncher/CompactCheckBoxStyle.axaml`：`CheckBox.compact` 样式（尺寸/字号/前景色），不重写模板 | Avalonia 无 VisualStateManager/AnimatedIcon；Fluent 模板已是等价基线 |
| DX12 提示框 | `ContentDialog` | `StarwardContentDialog`（`DX12IntroDialog`） | §七 #16 |
| 游戏公告 | `GameNoticeWindow` + WebView2 | `WindowEx` + `Frameworks/WebView`（WebKitGTK） | §七 #17 |

### 12.3 GameLauncherSettingDialog（`GameLauncherSettingDialog.xaml` 42 KB → `.axaml`）

| 项 | 上游 | 本仓库 | 原因 |
|---|---|---|---|
| 对话框根 | `ContentDialog` + `ContentDialog.Resources`（MaxWidth/Padding/SmokeFill） | `controls:StarwardContentDialog`，删 Resources 块 | 遮罩/宽度由 DialogService 宿主提供 |
| 左侧导航 | `NavigationView`（PaneHeader + MenuItems + ItemInvoked） | `Grid ColumnDefinitions="200,*"` + 左 `ListBox`（4 个 `ListBoxItem Tag=0..3`）+ `SelectionChanged` | Avalonia 无 NavigationView |
| 右侧翻页 | `FlipView` | `Carousel`（保留 `FlipView_Settings` 名称） | Avalonia 无 FlipView；Carousel 语义等价 |
| 分段选择语音 | `cwc:Segmented`（Multiple） | `StackPanel` + 4 个 `ToggleButton`（`Tag=zh-cn/...`，`IsCheckedChanged` 收敛选择） | Avalonia 无 CommunityToolkit Segmented |
| 超链接 | `Hyperlink`（2 处） | `Button Classes="link" Tag=<url>` + `Click` | 同上 |
| `ObservableProperty` | 4 个分部属性 | 手写属性 + `OnXxxChanged` | 同 12.2 |
| 文本裁剪事件 | `TextBlock.IsTextTrimmedChanged` | 删除（Avalonia 无该事件） | 无等价 API |
| `MainWindow` 拖拽区 | `XamlRoot.SetWindowDragRectangles` | `MainWindowDragRectAdaptToGameIconMessage` | 同 12.1 |
| `GameInstall` 调用 | 上游同一份 `GameInstallService` | 由 rpc-port 的 `Features/GameInstall/GameInstallService.cs`（gRPC 版）承接 | 该文件归 task-10 |

### 12.4 GameInstall 三个对话框（`InstallGameDialog` / `PreDownloadButton` / `PreDownloadDialog`）

| 项 | 上游 | 本仓库 | 原因 |
|---|---|---|---|
| 对话框根 | `ContentDialog` | `controls:StarwardContentDialog` | §七 #16 |
| 音频语言分段 | `cwc:Segmented`（Multiple） | `ListBox SelectionMode="Multiple"` + `ListBoxItem Tag=zh-cn..`（`ItemsPanel` 横向 StackPanel） | Avalonia 无 Segmented；ListBox 保留 `SelectedItems` 语义 |
| `ObservableProperty` | 3+3 个分部属性（含 `NotifyPropertyChangedFor`） | 手写属性 + `OnPropertyChanged(nameof(...))` | 同 12.2 |
| 目录选择 | `FileDialogHelper.PickFolderAsync(this.XamlRoot)` | `FileDialogHelper.PickFolderAsync(this)`（`TopLevel.StorageProvider`） | 无 XamlRoot（§七 #16） |
| 文本裁剪事件 | `TextBlock.IsTextTrimmedChanged` | 删除 | 无等价 API |
| `RelayCommand Close` | 与 `ContentDialog.Hide()` 同名 | 改名 `CloseDialogCommand` → `this.Close()` | 基类同名递归 |
| 安装任务类型 | `Starward.RPC.GameInstall.*` | `LunarWard.RPC.GameInstall.*`（rpc-port 落点，`GameInstallContext`/`GameInstallState`/`GameInstallRequest`） | RPC 层归 task-10 |
| `FontIcon` 图标 | `FontIcon Glyph` | `TextBlock Classes="icon"` | 仓库无 FontIcon 类（§七 #18） |
| 阴影 | `cu:AttachedDropShadow` + `Scene3D.Color` | 删除 | Avalonia 无 WinUI 阴影合成效果（改用 BoxShadow/边框） |

### 12.5 明确未移植（同 §7.3）

- `Features/GameSelector/NoAnimationImageExStyle.xaml`：WinUI `ImageEx` 的样式，Avalonia 无该控件，`Image` 已无淡入动画，样式的存在前提消失。
- `Features/GameLauncher/CompactCheckBoxStyle.xaml` 的**模板本体**未逐行翻译（见 12.2），只保留紧凑样式语义。

---

## 十七、Features/Background 平台替换与验收（2026-10-26，settings-migrate / task-13）

承接 §九（逐条渲染差异表），本节只记 task-13 的**平台替换清单、去重结论与验收证据**，避免与 §九 重复。

### 17.1 平台替换清单（逐项）

| 上游 API | 本仓库替换 | 说明 |
|---|---|---|
| Win2D `CanvasControl` / `CanvasAnimatedControl` / `CanvasBitmap` / `CanvasRenderTarget` | `Bitmap.DecodeToWidth` + `WriteableBitmap` + Avalonia `Image` | 图片缩放解码；视频由 `Services/VideoBackgroundPlayer` 解帧写入 `WriteableBitmap`（960×540 BGRA、24fps 上限、`-an` 无音轨） |
| `MediaPlayer` / `MediaPlayerElement` / `CompositionVideoSurface` | ffmpeg 子进程 → `VideoHost`（`ContentControl`）内嵌播放视图 | Avalonia 无内置视频；`-an` + 失焦 `SIGSTOP` 控功耗 |
| `AppWindow` / `AppWindowTitleBar` / `OverlappedPresenter` | 普通 `Window` + `ExtendClientAreaToDecorationsHint` + `WindowStartupLocation=CenterScreen` | 图库窗口的标题栏/图标/居中等价 |
| `DesktopAcrylicBackdrop` / `MicaController` / `AcrylicBrush` | `Helpers/TransparentBackdrop` + 纯色/模糊降级 | Avalonia 无系统级 Acrylic/Mica；降级不出现透明失效窗口 |
| `CommunityToolkit.WinUI.Helpers`（`ColorHelper` / `SystemInformation`） | BCL（`System.Drawing.ColorConverter` 等）+ Avalonia API | 无 WinUI 依赖 |
| `FileOpenPicker` / `StorageFile` / `DataPackage` | `TopLevel.StorageProvider` / `IStorageFile` / `ClipboardHelper` | 选文件、复制路径按 Avalonia 习惯 |
| `BitmapDecoder`（视频/WebP 预校验） | 不再预校验；播放失败上层回退 | ffmpeg 能力运行时探测，避免误判 |

### 17.2 视频背景降级策略（不许静默黑屏）

`AppBackground.SetVideoBackgroundAsync`：`VideoBackgroundPlayer.StartAsync` 返回 `false`（如系统无 `ffmpeg`）时，`IsVideoPlaying=false`，**静态背景图保持可见**，并调用 `InAppToast.Show("视频背景不可用", "已回退为静态背景图（视频播放需要 ffmpeg）")` 给出可见提示。原先只回退不提示，本轮补齐。

### 17.3 `AccentColorHelper` 去重（防漂移）

上游 `src/Starward/Helpers/AccentColorHelper.cs`（197 行，`GetAccentColor` + `ChangeAppAccentColor`）与上游 `Features/Background/AccentColorHelper.cs` 是两份历史实现。本仓库此前同时存在：

- `src/LunarWard/Features/Background/AccentColorHelper.cs`（125 行，`GetAccentColor(byte[], …)` / `GetAccentColor(nint, …)` / `ChangeAppAccentColor`）——**唯一实现，保留**；
- `src/LunarWard/Helpers/AccentColorHelper.cs`（252 行，带 `(Back, Fore)` 元组与 `Bitmap`/`Stream` 重载）——**已删除**（`git rm`）。删除前 `grep -rn "AccentColorHelper\." src` 确认无外部调用方。

结论：强调色只有 `Features/Background/AccentColorHelper` 一份，与上游 Features 版对齐；`ChangeAppAccentColor` 仍通过 `AccentColorChangedMessage` 生效。

### 17.4 本轮顺手修掉的编译/行为点

- `BackgroundViewPage.MutliSelect`：Avalonia `SelectionMode` 没有 `None` → 非多选态用 `Single` 并在 `SelectionChanged` 中立即清空选中项（对齐上游 `ListViewSelectionMode.None`）。
- `BackgroundViewPage` 复制路径：`IClipboard.SetTextAsync`（Avalonia 12 无此成员）→ `Helpers/ClipboardHelper.SetText`。
- `AppBackground.ChangeBackgroundImageAsync`：`ActualWidth`（WinUI 属性）→ `Bounds.Width`。
- `BackgroundService.FileIsSupportedVideo(string?)` → `string`（与上游签名一致），调用点补 `filePath is not null` 判空。

### 17.5 DI 注册

`BackgroundService(ILogger<BackgroundService>, HoYoPlayService, HttpClient)` 已注册：`src/LunarWard/AppConfig.ServiceProvider.cs:55` → `sc.AddSingleton<BackgroundService>();`（无需新增）。

### 17.6 验收证据（2026-10-26）

- `dotnet build src/LunarWard/LunarWard.csproj`：`Features/Background` **0 error / 0 warning**；全树其余错误均在其他 agent 在途目录（`Features/GameRecord`、`Features/GameLauncher`、`src/LunarWard.RPC`）。
- `grep -rn "Starward\.\|Microsoft\.UI\|Windows\.UI\|Microsoft\.Graphics" src/LunarWard/Features/Background`：仅注释命中。
- 文件数与上游一致：13 个（含 `AppBackground.axaml`、`AppBackgroundImageExStyle.xaml`、图库 `BackgroundViewPage`/`BackgroundViewWindow`）。

### 17.7 仍存缺口（同 §九末）

`BackgroundViewPage` 的**拖拽导出**（`DragItemsStarting` → `DataPackage` 文件对象）未实现；多选复制得到路径文本而非文件对象。Avalonia `StorageProvider` 无法直接构造「文件对象」剪贴板负载，登记为缺口，不阻塞本轮。


## 十八、RPC 安装栈（2026-10-26，rpc-port / task-10）

上游 `src/Starward.RPC`（5251 行，gRPC + protobuf 独立进程）整体搬入新工程 `src/LunarWard.RPC`，主程序侧 `Features/RPC/RpcService.cs`（168 行）+ `Features/GameInstall/*`（GamePackageService 184 / GameInstallService 417 / 三个对话框）按上游逐行移植，命名空间 `Starward.*`→`LunarWard.*`，公开成员名保持上游拼写。

### 平台替换清单（逐点）

| 上游（Windows） | 本仓库（Linux） | 文件 |
| --- | --- | --- |
| `NamedPipeServerStream` + PipeSecurity（`RPC/{SessionId}/{Version}`） | Unix domain socket：Kestrel `ListenUnixSocket`，路径 `$XDG_RUNTIME_DIR/lunarward/rpc.sock`；`XDG_RUNTIME_DIR` 不可用时回退 `/tmp/lunarward-$UID/rpc.sock`。目录创建后 `chmod 700`，socket 文件 `chmod 600`。 | `src/LunarWard.RPC/AppConfig.cs`、`Program.cs` |
| 客户端 `NamedPipesConnectionFactory` | `UnixSocketConnectionFactory`（`SocketsHttpHandler.ConnectCallback` → `UnixDomainSocketEndPoint`），地址 `http://localhost`，与服务端共用同一份 `RpcClientFactory`（上游也是共享的） | `src/LunarWard.RPC/RpcClientFactory.cs`、`UnixSocketConnectionFactory.cs` |
| 命名 Mutex 单实例 | 独占打开锁文件 `rpc.lock`（`FileShare.None`）；`CheckRpcServerRunning` = socket 存在 + 锁获取失败 | `src/LunarWard.RPC/AppConfig.cs`、`RpcClientFactory.cs` |
| `Process.Start(..., Verb = "runas")` 提权启动 `Starward.RPC.exe` | 无 UAC：`Process.Start` 同一可执行文件，参数 `rpc <StartupMagic> <pid>`；主程序 `Program.Main` 增加 `rpc` 分发（上游 `App.xaml.cs` 同逻辑）。**主程序 `Program.cs` 是本次唯一动到 `Features/RPC|GameInstall` 之外的改动。** | `src/LunarWard/Features/RPC/RpcService.cs`、`src/LunarWard/Program.cs` |
| `Vanara.PInvoke.Kernel32.CreateHardLink` | libc `link(2)`（`NativeMethods.Link`，`SetLastError`）；同卷判断由「卷格式 == NTFS」改为 `DriveHelper.IsHardLinkSupported`（ext2/3/4、xfs、btrfs、zfs、tmpfs、overlay、f2fs 白名单） | `HardLink.cs`、`GameInstallHelper.cs`、`GamePackageService.cs`、`GameUninstallService.cs`、`Helpers/DriveHelper.cs` |
| `SharpSevenZip`（内置 7z.dll，仅 Windows native） | 外部 `7zz/7z/7za` 进程（`LUNARWARD_7Z` 可覆盖），zip 走 `ZipFile` 回退；元数据探测（`hdiffmap.json` / `hdifffiles.txt`）用 `7z l -slt` | `GameInstall/ArchiveExtractor.cs` |
| `Snap.HPatch.HPatch.PatchZstandard`（仅 win-x64 native） | 外部 `hpatchz/hpatch` 进程（`LUNARWARD_HPATCHZ` 可覆盖）；为主程序流提供临时文件桥接（`PatchStream`） | `GameInstall/DiffPatcher.cs`、`GameInstallHelper.cs` |
| `Vanara` 卷/USB 检测（`unsafe` + `DeviceIoControl`） | `DriveInfo` + `/sys/block/*/removable` + `/proc/self/mountinfo` 前缀判断，去掉 `unsafe` 与 `IsAdmin`（Linux 无提权语义） | `src/LunarWard.RPC/DriveHelper.cs`、`AppConfig.cs` |
| 缓存目录 `LocalApplicationData\Starward` | 便携模式 `<exe目录>/.cache`；否则 `~/.local/share/LunarWard`（主程序 AppConfig 已同名） | `src/LunarWard.RPC/AppConfig.cs` |

gRPC 服务定义、`.proto` 消息字段与调用侧代码未改（仅 `option csharp_namespace` / `package` 由 `Starward.RPC*` 改为 `LunarWard.RPC*`，两端一致）。

### 自测（已通过）

```
LUNARWARD_RPC_SELFTEST=1 dotnet run --project src/LunarWard.RPC -- rpc zb8L3ShgFjeyDxeA 1
# 输出：SELFTEST_OK pid=1721122 path=.../LunarWard.RPC
# 日志：Now listening on: "http://unix:/run/user/1000/lunarward/rpc.sock"
#       Application started.  → Application is shutting down.
```
自测走完整 DI 管线（Serilog、Kestrel、`RpcClientFactory`），客户端与服务端经同一个 Unix socket 通信，验证 `Env.GetRpcServerInfo` 往返；同时校验 socket 文件权限 600、目录 700。

### 已知缺口 / 待办

1. `7z`/`hpatchz` 为**运行时外部依赖**，发行包未内置；缺失时 `SevenZipArchiveExtractor` 抛 `InstallerCapabilityException`（安装/预下载会明确报错，不会静默跳过）。打包脚本需把 `7zz`/`hpatchz` 放进 PATH 或设置 `LUNARWARD_7Z`/`LUNARWARD_HPATCHZ`。
2. xunit 端到端测试未加：主工程当前被 `Features/GameRecord` 等他人任务拖红，测试工程无法整体编译；本轮以 `LUNARWARD_RPC_SELFTEST` 自测覆盖「服务端起 + 客户端 ping 通」。
3. 真实游戏包下载/安装全链路（Sophon 分块、断点续传、hpatch 实际打补丁）未在 CI 实测，仅静态对齐 + gRPC 往返验证。
4. `src/LunarWard.RPC` 为可执行工程（上游为 Library）：新增 `EntryPoint.cs` 与 `LUNARWARD_RPC_SELFTEST` 分支，便于独立启动与自测；上游 `Starward.slnx` 中它是库工程，此处属平台必要偏离。
5. `Features/RPC/` 目录只落 `RpcService.cs`（与上游一致）；`RpcClientFactory` 按上游同构放在 RPC 工程内、随 ProjectReference 共享，主程序不另存一份。
6. `GameInstallTaskStartedMessage` 现为 RPC 契约类型 `LunarWard.RPC.GameInstall.GameInstallContext`（上游原名），三个对话框直接发送，无需桥接；文件头注释仍称之为 `LunarWard.Installer.GameInstallContext`，后续清理注释即可（不影响行为）。
7. `docs/10-ALIGNMENT.md` 中 RPC 行仍写「本轮不建」，需由 lead/settings-migrate 更新；`scripts/check-alignment.py` 的 RPC 映射同理。

## 十九、Features/PlayTime（2026-10-26，misc-port / task-12）

对应上游 `Features/PlayTime/`（9 文件，含 `PlayTimeButton`/`PlayTimeStatsDialog` 各一对 xaml + xaml.cs）+ `Features/PlayTime/Charts/`（5 文件），共 14 个上游文件一一直译，逐文件移植：类名、成员名、SQL、状态机、统计口径、图表公开 API（`BarChart.Items`/`PlotHeight`/`MaxBarWidth`、`CalendarHeatmap.Days`/`CellSize`/`CellGap`/`ScaleMax`/`ShowMonthSplit`/`LevelBrush`、`HoverCard.Bind/Show/Hide`、`BarChartItem`/`HeatmapDayItem`/`StatCardItem`）与上游一致。

> 编号说明：任务原本分配「十六」，但该号已被 `Features/GameRecord` 占用（详见 §十六），为避免改动他人小节，本节落在「十九」。

| 位置 | 上游行为 | 本仓库行为 | 原因 | 待办 |
|---|---|---|---|---|
| `PlayTimeStatsDialog` 宿主 | `ContentDialog` + `ShowAsync()` + `XamlRoot` | `StarwardContentDialog` 子类 + `Services.DialogService.ShowAsync(dialog)`（宿主缺省回退主窗口 `ContentDialogHost`），关闭走 `Close()` | Avalonia 无 `ContentDialog` | — |
| `PlayTimeStatsDialog` 的 `Segmented`（15 天 / 12 周 / 12 月） | `CommunityToolkit.WinUI.Controls.Segmented` + `SelectionChanged` | 3 个 `ToggleButton`（`IsChecked` 互斥）+ 同语义的 `BarRange_Checked`，读选中项得到 `BarRangeIndex`（= 上游 `SelectedIndex`） | Avalonia 无 `Segmented` | — |
| `StatCards` 布局 | `ItemsRepeater` + `UniformGridLayout(MinItemWidth=156, ItemsStretch=Fill)` | `ItemsControl` + `UniformGrid Rows=1 Columns=6`（对话框内容区宽 960，6×160≥156，与上游同一行等宽排布）；卡片外壳由 `Grid` 改 `Border`（Avalonia 的 `Panel` 没有 `Background`/`BorderBrush`/`CornerRadius`） | Avalonia 无 `ItemsRepeater`/`UniformGridLayout` | 若日后需要自适应列数，改为代码里按实际宽度算列数 |
| 长文案分支 `UniformGridLayout_StatCards.MinItemHeight = 98` | 抬高卡片最小高度 | **未实现**（`UniformGrid` 无该属性；卡片高度由内容自适应），仅保留 `Grid_BarChartSwitcher.Margin` 与 `PlayTimeHeatmap.Margin` 两处调整 | 控件能力缺失，不影响数据 | 无（视觉差异 0，卡片本就随两行标题变高） |
| `BarChart` 绘制 | Win2D 无关，但用 WinUI `Grid` + 运行时构造 `Rectangle`/`TextBlock`；悬停用 `HoverCard`（WinUI `Canvas` 浮层） | 同构重建：`BarChart : Grid`（`Grid`+`Border`+`Rectangle`+`TextBlock` 运行时构造），`HoverCard : Canvas` 用 `TranslatePoint` 定位、`Measure` 量尺寸后 `SetLeft/SetTop` | 框架控件类型不同 | — |
| `CalendarHeatmap` 布局驱动 | `LayoutUpdated` 事件里做「标签列真实宽度」校正与月份标签定位 | 重写 `ArrangeOverride(Size)`：排列完成后同一套 reconcile（`_reconcileCount` 封顶 3 次，避免死循环），月份标签/月边界折线用 `TranslatePoint` 取槽位实测坐标做线性校准 | Avalonia 无 `LayoutUpdated` 语义保障；`ArrangeOverride` 是唯一的“已排列”时机 | — |
| `CalendarHeatmap` 尺寸与颜色 | `ActualWidth`、`Windows.UI.Color`、`Polyline.Points`（`PointCollection`）、`StrokeLineJoin`、`IsTextScaleFactorEnabled`、`Application.Current.RequestedTheme` | `Bounds.Width`、`Avalonia.Media.Color`、`IList<Point>`、`StrokeJoin`（删除文本缩放假开关）、`Application.Current.ActualThemeVariant == ThemeVariant.Dark` | API 同名不同物 | — |
| 热力图色阶资源 | `SystemAccentColorDark3…Light3`（WinUI 强调色 8 级） | 先 `TryFindResource("SystemAccentColor…")`，缺键时才用上游同款 GitHub 绿阶兜底 | 本仓库不引 WinUI 主题资源字典，但 `Styles/Starward.axaml` 已按 Starward 的 `#CBAD8E` 手工推导出 `SystemAccentColor`/`Light1..3`/`Dark1..3` 七个 `Color` 键，实际走强调色分支，绿阶兜底不触发 | 无（若要跟随系统动态强调色，需要 `IPlatformSettings.ColorValuesChanged` 重新推导这 7 个键） |
| Fluent 资源键 | `TextFillColorSecondaryBrush`、`DividerStrokeColorDefaultBrush`、`AccentFillColorDefaultBrush`、`CardBackgroundFillColorDefaultBrush`、`CustomOverlayAcrylicBrush`、`ControlStrongStrokeColorDefaultBrush` | `Charts/ChartHelpers.GetResource<T>` 内建别名表映射到 `StarTextSecondary`/`StarDividerBrush`/`StarAccentBrush`/`StarCardBackgroundBrush`/`StarOverlayAcrylicBrush`；映射表之外或按键缺失时抛 `KeyNotFoundException`（**不静默兜底**，便于早期发现漏配） | 资源键体系不同 | 新增图表用到新键时先补别名表 |
| `PlayTimeButton` 外壳 | `ThemeResource CustomOverlayAcrylicBrush` + `ThemeShadow` + `Translation="0,0,16"` + `FontIcon` | `Border` + `{DynamicResource StarOverlayAcrylicBrush}` + `Border.BoxShadow`（`#40000000`，OffsetY 4、Blur 16）+ `TextBlock` + `StarIconFontFamily` | Avalonia 无 `ThemeShadow`/`FontIcon` | — |
| 按钮时长文本 | `{x:Bind local:PlayTimeButton.TimeSpanToString(PlayTimeTotal)}`（直接绑定静态方法调用） | 新增 `PlayTimeText` 只读属性（`TimeSpanToString(PlayTimeTotal)`），`PlayTimeTotal` setter 里一并 `OnPropertyChanged(nameof(PlayTimeText))`；静态方法 `TimeSpanToString` 保留 | Avalonia 绑定不支持静态方法调用 | — |
| `[INotifyPropertyChanged]` 源生成器 | CommunityToolkit 生成 | 手写 `SetProperty` + 显式接口 `event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged`（避免与 `AvaloniaObject.PropertyChanged` 的 CS0108 冲突） | 生成器与 Avalonia 控件基类冲突（同 §十二 的 `UpdateWindow`） | — |
| 单实例（`PlayTimeRecordService`） | `AppInstance.FindOrRegisterForKey($"playtime_{pid}")` 判断该进程是否已有记录器 | 独占锁文件 `Path.Join(AppConfig.CacheFolder, "runtime", $"playtime-{pid}.lock")`（`FileShare.None`；拿不到即视为已有 watcher），与 `Services/StartupCommands.RunPlayTimeAsync` 同一约定 | Linux 无 `AppInstance`；锁文件是本仓库既有约定 | 若同机多用户共享缓存目录需改为 `$XDG_RUNTIME_DIR`（当前按用户隔离，风险低） |
| 记录器子进程 | `Process.Start(StarwardExecutePath, "playtime --biz ... --pid ...")` | 原样保留（`AppConfig.StarwardExecutePath` = 当前可执行文件） | — | — |
| 数据层 | `DatabaseService.CreateConnection()` + Dapper + `DatabaseSqls` | 完全一致（复用 `Features/Database/DatabaseService` 的 `PlayTimeItem`/`PlayTimeStats` 表与索引，`GameBizHandler` 处理 `hk4e_cn` 等字符串） | — | — |
| `PlayTimeButton` 挂载点 | 上游嵌在 `GameLauncherPage`/`MainWindow` | 只交付控件；挂载点在 `Features/GameLauncher` 等页面落地后由 Lead 接线（旧 `Views/Starward/PlayTimeStatsDialog` 与本簇并存） | 跨作用域 | Lead 接线后删除旧 `Views/Starward/PlayTimeStatsDialog` 与 `Controls/PlayTime*Chart` |
| DI | `AppConfig.GetService<PlayTimeStatsService>()`/`GetService<PlayTimeRecordService>()` | 同上；注册行由 Lead 加进 `AppConfig.ServiceProvider.cs`：`sc.AddSingleton<PlayTimeStatsService>();`、`sc.AddSingleton<PlayTimeRecordService>();`（构造签名 `PlayTimeRecordService(ILogger<PlayTimeRecordService>, HoYoPlayService, PlayTimeStatsService)`、`PlayTimeStatsService()` 无参） | 该文件归 Lead | 未注册时 `GetService` 会抛异常（与上游一致，不静默） |

## 二十、启动器簇行为级审计补丁（2026-10-26，launcher-port / task-15）

对应审计报告：`docs/14-LAUNCHER-AUDIT.md`。本节只记录本轮**新产生**的平台替换/补齐点（此前 §十二 的条目仍然有效）。

| 位置 | 上游行为 | 本仓库行为 | 原因 | 待办 |
|---|---|---|---|---|
| `Features/GameLauncher/GameLauncherService.BuildStartInfo` | `ProcessStartInfo{ FileName=exe, Arguments=arg, UseShellExecute=true, Verb=runas, WorkingDirectory=dir }`，由 Windows 负责解析命令行与提权 | `FileName = GamePrefixContext.CurrentWineBinary ?? "wine"`，`Arguments = "\"<exe>\" <arg>"`（**单字符串，不按空格拆**），注入 `WINEPREFIX`/`WINEARCH=win64`/`WINEDLLOVERRIDES=winemenubuilder.exe=d;mscoree,mshtml=`；ZZZ 且 `GetEnableDX12` 时追加 `DXVK_ENABLE_NVAPI=1`、`VKD3D_CONFIG=dxr`（复用 `Games.GameAdapter.BuildEnv`） | Linux 无 `runas`/`CreateProcess`；`.exe`/`.bat` 必须经 wine；按空格拆分会破坏带空格的启动参数 | 若接入 `LaunchPlanFactory` 的 profile 级计划（DXVK HUD/FSR/Wayland），把基线环境换成 `LaunchPlanFactory` 的 Baseline 层 |
| `Features/GameLauncher/GameLauncherService.BuildStartInfo` 可见性 | 上游无此方法（逻辑内联） | `public static`（文档注明「可测缝」） | 单测需要断言 wine 路径/命令行/环境，不能启动真实进程 | — |
| `Features/GameSelector.AutoSearchInstalledGames` | 同步方法，直接读 `HKCU\Software\miHoYo\HYP\1_1\{biz}` / `Cognosphere\HYP\1_0\{biz}` 的 `GameInstallPath`（REG_SZ） | `async Task AutoSearchInstalledGamesAsync()`；HYP 读取走新增的 `Services/WineRegistry.ReadStringAsync`（prefix 复用 `GamePrefixContext.CurrentPrefix`，wine 二进制复用 `CurrentWineBinary`），`C:\…` 盘符路径换算到 `prefix/dosdevices/<drive>:` | 注册表在 wine prefix 里；`wine reg` 是子进程，同步会冻 UI | — |
| `Services/WineRegistry` | 上游无对应（Windows `Registry.GetValue`） | 新增 `ReadStringAsync`（REG_SZ）+ 纯函数 `ParseString`（值名可带引号、数据可含空格、本地化「值未设置」→ `null`）；未改 DWORD/BINARY 路径与既有签名 | HYP `GameInstallPath` 是 REG_SZ，原有 API 只有 DWORD/BINARY | — |
| `Features/GameLauncher/GameLauncherPage` 拖放换背景 | `e.DataView.Contains(StandardDataFormats.StorageItems)` + `GetDeferral()` + `DataView.GetStorageItemsAsync()` + `StorageFile` | `e.Data.Contains(DataFormats.Files)` + `e.Data.GetFiles()` + `IStorageFile`（Avalonia 无 deferral） | Avalonia 拖放 API 不同 | — |
| `Features/GameLauncher/GameLauncherPage` 复制/保存背景 | `ClipboardHelper.SetStorageItems(DataPackageOperation.Copy, StorageFile)`；`Launcher.LaunchFolderAsync(FolderLauncherOptions{ ItemsToSelect })` | `TopLevel.Clipboard.SetFilesAsync([IStorageFile])`；保存后 `TopLevel.Launcher.LaunchDirectoryInfoAsync(所在目录)`（Linux 无「在文件管理器中选中」语义） | WinUI 专有 API | 如需「选中」语义，等 Avalonia 提供 `reveal` API |

---

## 二十一、Features/ViewHost（窗口外壳：MainWindow / MainView / 等价控件，2026-10-26，Lead）

上游 `Features/ViewHost` 共 20 个文件，本仓库全部落位（`src/LunarWard/Features/ViewHost/`），本节记录其中的平台替换点。
本章追加在文末：docs/09 的章节编号在前几轮并行开发中出现过「十一/十二」重复，收尾阶段统一重排（见 docs/10 §7 endgame 第 8 步）。

### 21.1 三个等价控件（`src/LunarWard/Frameworks/`）

| 上游（WinUI） | 本仓库 | 说明 |
| --- | --- | --- |
| `Microsoft.UI.Xaml.Controls.Frame` | `Frameworks/Frame.cs` | 只保留 `Navigate(Type, object?, NavigationTransitionInfo?)` + `SourcePageType`；**不写 `DataContext`**（本仓库页面在构造函数里 `DataContext = this`，写了会覆盖绑定），参数通过 `PageBase.OnNavigatedTo(object?)` 传递，语义与上游一致（GameId / GameBiz） |
| `NavigationView` | `Frameworks/NavigationView.cs` | 只实现上游用到的部分：`LeftCompact` 折叠栏、`MenuItems`（`NavigationViewItem` / `NavigationViewItemSeparator`）、`PaneCustomContent`、`SelectedItem`、`ItemInvoked`；栏宽 `CompactPaneLength`，条目 40×40 图标 + ToolTip。**未实现**：BackButton、PaneToggleButton、展开态、顶部模式（本项目不使用） |
| `FontIcon` | `Frameworks/FontIcon.cs` | 字体图标即「图标字体的 TextBlock」；`Glyph` 写法与上游一致，字体取 `StarIconFontFamily`（`Assets/Font/Segoe Fluent Icons.ttf`） |
| `NavigationTransitionInfo` / `SuppressNavigationTransitionInfo` | 同 `Frameworks/Frame.cs` | 仅保留类型以便上游代码原样编译；Avalonia 侧不做导航动画 |

### 21.2 MainView（`Features/ViewHost/MainView.axaml(.cs)`）

- `{ThemeResource CustomAcrylicBrush}` → `{StaticResource StarAcrylicBrush}`；`Border.OpacityTransition` → `Transitions/DoubleTransition`。
- 上游 `[INotifyPropertyChanged]` **不能**用在 `UserControl`（AvaloniaObject）派生类上（MVVMTK0001），改为手写 `INotifyPropertyChanged` + `OnPropertyChanged`；`CurrentGameFeatureConfig` 因上游类型是 `internal`，属性也改 `internal`（同程序集可见，XAML 不绑定它）。
- `XamlRoot.GetWindowHandle()` → `Helpers/XamlRootExtension` 的 `FindTopLevel()` + `TryGetPlatformHandle()`。
- `DispatcherQueue` → `Dispatcher.UIThread`；`Visibility` → `IsVisible`；`ToolTipService.SetToolTip` → `ToolTip.SetTip`。
- `MainView_Frame.SourcePageType` 由 `Frameworks.Frame` 提供（与上游同名）。

### 21.3 MainWindow（`Features/ViewHost/MainWindow.axaml(.cs)`）

| 上游 | 本仓库 | 说明 |
| --- | --- | --- |
| `AppWindow.TitleBar.*` / `OverlappedPresenter(IsMaximizable=false, IsResizable=false)` | `WindowEx`（`AdaptTitleBarButtonColorToActuallTheme` / `CanResize=false`） | 无 Win32 标题栏 API，尺寸/装饰由 Avalonia 窗口承担 |
| `WindowSubclassProc`（`WM_HOTKEY` 44444/44445、`WM_WTSSESSION_CHANGE`、`WM_DEVICECHANGE`） | **不移植** | X11 没有等价的窗口子类化钩子；全局热键在 Linux 返回不支持（`HotkeyManager`），锁屏/设备插拔消息保留类型但无发送源。`RunningGameService.OpenOverlayWindow()` 与 `ScreenCaptureService.Capture()` 的调用入口留在注释里，等 Overlay/截图热键有 Linux 后端再接 |
| `WTSRegisterSessionNotification` | 删除 | 仅 Windows 桌面会话 |
| `AppWindow.Closing` + `ContentDialog` | `Closing` + `DialogService.ShowAsync(MainWindowCloseDialog)` | `ContentDialogResult` → `PrimaryButtonClicked` 事件 + `confirmed` 布尔；`DefaultButton` 无等价物（StarwardContentDialog 固定样式） |
| `App.Current.EnsureSystemTray()` | `App.EnsureSystemTray()`（新建 `SystemTrayWindow`） | 旧 `Services/TrayService` 与新的 `TrayIcon` 并存，endgame 删除旧者 |
| `App.Current.Exit()` | `IClassicDesktopStyleApplicationLifetime.Shutdown()` | 退出前仍执行 `DatabaseService.AutoBackupToAppDataLocal`（最多等 30s） |
| `User32.SetCursorPos`（`ShowByGamepad` 把光标移到窗口中心） | **不移植** | Avalonia 无跨平台光标定位 API；只做 `Activate()` |
| `RequestedTheme` 抖动刷新强调色 | `Application.Current.RequestedThemeVariant` 抖动 | 与上游同一思路（强制重新解析主题资源） |

### 21.4 运行验收（待树绿）

- `Features/ViewHost` 全目录编译 0 error（含 `MainWindow` / `MainView` / 三个 Frameworks 等价件）。
- **未做**：真机启动 `MainWindow` 的视觉验收（新外壳尚未接入 `Program.cs` 启动路径，见 docs/10 §7 第 5 步）；`--screenshot` 自检路由仍走旧 `Views/Starward` 页面。
- 已知阻塞：`MainView.NavigateTo` 里 `GameSettingPage` 仍是旧 `Views/Starward/StarwardPage` 基类（`PageContext` 构造），`Frameworks.Frame` 只处理 `PageBase`；task-11 切壳后统一。

> 构建基础设施（2026-10-26，dapper-db）：`src/LunarWard/Frameworks/WebView/SelfTest.Avalonia/XProbe.axaml` 是嵌套子工程，主工程会把它当 XAML 项，导致 `AXN0001`（找不到 `SelftestAvalonia.XProbe`）并使**整个程序集**的 `x:Name` 字段生成失败（表现为各页 CS0103）。已在 `src/LunarWard/LunarWard.csproj` 补 `<AvaloniaXaml Remove="Frameworks\WebView\SelfTest*\**" />`（此前只有 `AvaloniaResource Remove`，Avalonia 12 的 XAML 编译项是 `AvaloniaXaml`）。另外 `Features/Codec/ImageLoader.cs`、`PngCodec.cs` 的编译错误已顺手修：`ProcessResult`→`ProcessRunner.Result`（record 嵌套在同类 `ProcessRunner` 里）、`WritecHRM` 的 Span 捕获 lambda 改为显式传参（上游该函数由 Win2D 完成，无对应代码）。

## 二十二、全量 XAML 重编译暴露的共性问题（2026-10-26，dapper-db / task-4 收尾）

> **教训（对全组适用）**：`dotnet build` 是增量的，只有改动过的 `.axaml` 才会重新走 Avalonia 编译器；换别人文件/换机器后**不完全重新编译就看不到自己 XAML 的错误**。验收前必须做一次 `dotnet build src/LunarWard/LunarWard.csproj -t:Rebuild`（错误行前缀是 `Avalonia error AVLNxxxx`，不是 `error CS`，`grep ": error"` 会漏掉）。

`Controls` + `Features/Screenshot` 组在 `-t:Rebuild` 下暴露并修掉 6 类问题（均已提交）：

| # | 现象（全量编译才出现） | 根因 | 修法 |
|---|---|---|---|
| 1 | `AVLN2100: Cannot parse a compiled binding without an explicit x:DataType` | csproj 开了 `<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>`，`{Binding}` 必须有 `x:DataType` | 6 个移植窗口/页面根节点加 `x:CompileBindings="False"`（与本仓库 `GameLauncherPage.axaml`、`InstallGameDialog.axaml` 一致；避免给 code-behind 每个属性补 `x:DataType`） |
| 2 | `AVLN2000: Unable to resolve ... property Name on type ScaleTransform` | Avalonia 12 不给 `TransformGroup` 里的变换生成 `x:Name` 字段 | XAML 去掉三个 `x:Name`；代码侧用 `(TransformGroup)Image.RenderTransform` 取 `Children[i]`（docs/09 §8.3 #15） |
| 3 | `AVLN2000: Unable to resolve ... ExtendClientAreaChromeHints` | Avalonia 12 删除了该属性 | 删掉 `ExtendClientAreaChromeHints="NoChrome"`，保留 `ExtendClientAreaToDecorationsHint="True"` + `ExtendClientAreaTitleBarHeightHint="48"` |
| 4 | `AVLN2000: Unable to resolve ... property Padding on type Grid` / `Background on type CachedImage` | `Grid` 无 `Padding`；`Image` 无 `Background`（上游 `ImageEx` 是 `TemplatedControl`） | `ScreenCaptureInfoWindow` 的 `Padding="10"` 移到外层 `Border`；两处缩略图底色把 `Background` 移到外层 `Border`（`Border.thumb-host` / `Border.tile`），`CachedImage` 保持纯 `Image` |
| 5 | `AVLN2000: Unable to resolve ... AllowDrop / DragOver / Drop on ListBox` | 这三个是 `DragDrop` 的附着属性/事件 | 改 `DragDrop.AllowDrop="True"`、`DragDrop.DragOver="…"`、`DragDrop.Drop="…"` |
| 6 | `AVLN3000: Unable to find public constructor for type ImageViewWindow2()` | Avalonia XAML 编译器要求 `x:Class` 窗口有公共无参构造；上游两个构造都带参 | 补 `public ImageViewWindow2() { InitializeComponent(); }`（仅满足 XAML 编译器，业务入口仍是两个带参构造） |

## 二十三、跨切面 XAML 收口（2026-10-26，Lead）

全量重建（`--no-incremental`）把主程序 XAML 错误从 3454 压到 156 的过程中，Lead 统一改了以下几类**跨文件机械项**（提交 `d31404b`）：

| # | 现象 | 根因 | 修法 |
|---|---|---|---|
| 1 | `AVLN2000: Unable to resolve type Lang from namespace clr-namespace:LunarWard.Language`（1496 条的主因） | `clr-namespace:` 不写 `assembly=` 时只在**当前程序集**解析，跨程序集（Language/Core）解析不到 | 全仓 `clr-namespace:LunarWard.Language` → `using:LunarWard.Language`，`clr-namespace:LunarWard.Core.*` → `using:LunarWard.Core.*`；同程序集命名空间保留 `clr-namespace:` |
| 2 | `AVLN2000: ... property Text on type MenuItem` / `Name on type MenuFlyout` | Avalonia 的 `MenuItem` 用 `Header`（不是 `Text`）；`Flyout` 不生成 `x:Name` 字段 | `MenuItem Text=` → `Header=`；`MenuFlyout Name=` → `x:Name=`（`Flyout` 上的 `x:Name` 仍需删） |
| 3 | `AVLN2000: ... HorizontalSpacing / Spacing / VerticalSpacing on type WrapPanel` | Avalonia 12 的 `WrapPanel` 用 `ItemSpacing` + `LineSpacing` | `HorizontalSpacing` → `ItemSpacing`；`VerticalSpacing` → `LineSpacing`；只有 `Spacing` 时两者都写同值 |
| 4 | `AVLN2000: Unable to resolve type UIElementExtensions from namespace clr-namespace:CommunityToolkit.WinUI` | `CommunityToolkit.WinUI` 的附着属性在 Avalonia 无对应 | `cw:UIElementExtensions.ClipToBounds="True"` → `ClipToBounds="True"`，删 `xmlns:cw` |
| 5 | `File doesn't contain valid XAML: 'ItemSpacing' is a duplicate attribute` | 上面第 3 条批量替换与既有 `ItemSpacing` 撞车 | 标签内同名属性去重（保留首个），顺手修掉历史遗留的重复属性 |

### 23.1 WinUI 资源键的兼容别名（`Styles/Starward.axaml`）

上游 XAML 大量直接引用 WinUI 主题键，移植文件里保留原名可以少改 XAML，因此在 `Styles/Starward.axaml` 里登记了一批**别名资源**（`Star*` ← → WinUI 名），值指向本地已有的 `StarTextPrimary/Secondary/Tertiary`、`StarControlFillHoverBrush`、`StarAcrylicBrush`、`StarCautionBrush`：

`StarTextPrimaryBrush`、`StarTextSecondaryBrush`、`StarTextTertiaryBrush`、`AccentStarTextPrimary`、`StarAccentBackgroundBrush`、`TextFillColorPrimaryBrush`、`TextFillColorSecondaryBrush`、`TextFillColorTertiaryBrush`、`AccentTextFillColorPrimaryBrush`、`ControlOnImageFillColorDefaultBrush`、`ButtonPointerOverBackgroundThemeBrush`、`SystemFillColorCautionBrush`、`CustomAcrylicBrush`、`SegoeFluentIcons`、`SymbolThemeFontFamily`。

> 本应用固定深色主题（`App.axaml` 的 `RequestedThemeVariant="Dark"`），因此这些别名取深色主题下 WinUI 的固定色值（`#FFFFFFFF` / `#C5FFFFFF` / `#87FFFFFF` 等），**不做浅色映射**；将来若支持浅色需改成 `ThemeVariantScope` 下的两套字典。
> `BoolReversed`、`ObjectToVisibility` 两个短键也在 `App.axaml` 里补了同名转换器别名（上游 `App.xaml` 的键是全名，个别移植件引用了短名）。

## 二十四、启动器簇 XAML 全量重建修复（2026-10-26，launcher-port）

全量重建（`--no-incremental`）暴露此前增量构建掩盖的问题：Avalonia XAML 编译器在 `.axaml` 未变时跳过整段编译，导致页面级错误长期不可见。本轮按 `docs/16-XAML-MAPPING.md` 把 6 个目录的错误从 3420 行（去重 88 项）清零，其中属于「平台能力不等价」的项目登记如下。

| # | 位置 | 上游（WinUI/Starward） | 本仓库（Avalonia/LunarWard） | 原因 | 待办 |
|---|---|---|---|---|---|
| 1 | `Features/GameLauncher/GameLauncherPage.axaml` 背景轮播页码 | `PipsPager` 控件 + `PipsPagerItem` 模板（含 `PipsPagerItemsRepeater`/`StackLayout`） | `ItemsControl` + 水平 `StackPanel` 的 6×6 圆点（`CornerRadius=3`），绑定 `BackgroundImages` | Avalonia 无 `PipsPager` | 若要还原页码器，需自写 `PipsPager` 控件（`Controls/` 由 task-4 负责） |
| 2 | `Features/GameSelector/GameSelector.axaml` 图标区 | `ListView` + 400 行 `ListViewItemPresenter`/`DefaultListViewItemStyle` 模板 | `ItemsControl`（水平 `StackPanel`）+ 原 `ItemTemplate` | Avalonia 无 `ListViewItemPresenter`，且上游模板是为触摸选中态定制 | 若需要选中态，改用 `ListBox` + Avalonia 主题模板 |
| 3 | `Features/GameSelector/GameSelector.axaml` 图标圆角/背景 | `sc:CachedImage` 的 `CornerRadius`、`Image` 的 `Background` | 删除这两个属性（图片为方形、无底色） | Avalonia `Image` 无 `CornerRadius`/`Background`；`CachedImage` 虽然支持 `CornerRadius`（task-4 已加），但此处仍是 `Image` | `CachedImage` 落地后改用它即可恢复圆角 |
| 4 | 全部 6 个目录 | `ThemeShadow` + `Translation="0,0,16"` | `Border.BoxShadow="0 0 16 0 #40000000"` | Avalonia 无 WinUI 的 `ThemeShadow` 语义 | 深色主题下视觉等价 |
| 5 | `Features/GameLauncher/GameLauncherPage.axaml`、`GameLauncherSettingDialog.axaml` 等 | `Grid.Padding`/`CornerRadius`/`Translation`、`StackPanel.Padding` | 外包一层 `Border` 承载这些属性 | Avalonia 的 `Grid`/`StackPanel` 无 `Padding`/`CornerRadius` | 无 |
| 6 | 拖动区域 | `AllowDrop`/`DragOver`/`DragLeave`/`Drop` 事件属性 | `DragDrop.AllowDrop` 等附加属性 + `DragEventArgs` | Avalonia 用附加属性提供拖放 | 无 |
| 7 | `GameLauncherSettingDialog.axaml` 页签名 | `Pivot` + `DefaultPivotHeaderItemStyle` + `PivotItemMargin` | `Frameworks.Pivot`（`TabControl` 派生）/`PivotItem`（`TabItem` 派生），删除 `Pivot.Resources` 里的样式 | Avalonia 无 `Pivot`；`Pivot.Resources` 的 `Style` 在 XAML 编译器里引发 `KeyValuePair` 解析错误 | 页签高度/字号由主题默认值决定 |
| 8 | 受绑定控件（`StartGameButton`/`GameBannerAndPost`/`GameAccountSwitcher`/`GameAuthLoginButton`/`PreDownloadButton`/`PlayTimeButton`/`CloudGameButton`/`DailyNoteButton`） | 普通 CLR 属性 + `{x:Bind}` | 改 `StyledProperty<T>`（含 `Changed.AddClassHandler` 复刻原 setter 副作用） | Avalonia 中作为**绑定目标**的属性必须是 `AvaloniaProperty` | 无 |
| 9 | `GameLauncherPage.axaml.cs` 的 `Hyperlink_*_Click` | `(Button sender, RoutedEventArgs)` | `(object? sender, RoutedEventArgs)` | Avalonia 事件委托是 `EventHandler<RoutedEventArgs>` | 无 |
| 10 | `GameLauncherSettingDialog`/`PreDownloadDialog` 的 `Close` 命令 | `[RelayCommand] Close()` 调用 `Hide()` | 方法改名 `CloseDialog()`，XAML 绑 `CloseDialogCommand` | `Close` 与 `StarwardContentDialog.Close()` 同名会递归 | 无 |

> 同批修掉：`ToolTipService.ToolTip` → `ToolTip.Tip`、`ToggleSwitch.IsOn` → `IsChecked`、`AppBarSeparator` → `Separator`、`Style` 资源键 → `Classes="accent"`、`BitmapImage` → `Image.Source` 直接绑定、`IsSpellCheckEnabled`/`IsTextScaleFactorEnabled`/`DesiredPlacement` 删除、`FontIcon` → `Frameworks.FontIcon`、`x:Bind` → `Binding`（`{x:Static lang:Lang.X}` 单独二次置换）。

## 二十五、应用入口（`App.axaml.cs` / `Program.cs` / 单实例，2026-10-26，Lead）

P6 收尾把启动骨架重写成上游 `src/Starward/App.xaml.cs` 的结构（本仓库 `src/LunarWard/App.axaml.cs`），
`OnFrameworkInitializationCompleted` 等价上游 `OnLaunched`，其余成员一一对应：

| # | 上游 | 本仓库 | 说明 |
|---|---|---|---|
| 1 | 构造函数 `InitializeComponent()` / `RequestedTheme = Dark` | `Initialize()` 里 `AvaloniaXamlLoader.Load(this)` + `RequestedThemeVariant = ThemeVariant.Dark` | Avalonia 的初始化入口是 `Initialize()` |
| 2 | `Microsoft.UI.Xaml.UnhandledException` | `Dispatcher.UIThread.UnhandledException` | 崩溃日志格式与上游逐字节一致（`App Crash:` 前缀 + `Exception.Data` 全打印） |
| 3 | `System.Timers.Timer` 60s GC | `DispatcherTimer` 60s GC | UI 线程计时器，行为相同 |
| 4 | `OnLaunched(LaunchActivatedEventArgs)` | `OnFrameworkInitializationCompleted()` | 参数改从 `Environment.GetCommandLineArgs()` 读（与上游 `HandleStartupAsync` 同源） |
| 5 | `AppInstance.GetCurrent/FindOrRegisterForKey("main")/RedirectActivationToAsync` | `Frameworks/SingleInstance`（`$TMPDIR/lunarward-{user}.sock`，Unix socket + `activate` 一行） | 无 Windows 激活重定向；第二次启动把消息发给主实例后退出，主实例 `EnsureMainWindow()` |
| 6 | `--hide` → `new SystemTrayWindow()`；否则 `new MainWindow()` | 同（`SystemTrayWindow(mainWindow)` 用 Avalonia `TrayIcon` + `NativeMenu`） | 托盘菜单项与上游一致（`Lang.MainPage_Launcer` / `Lang.Common_Exit`） |
| 7 | `HandleStartupAsync`：`rpc` / `playtime` / `startgame` / `starward://` | 同四个分支，协议前缀改 `lunarward://` | `RpcRunner.Run(args)` 在 app 进程内跑宿主（上游同），`Program.cs` 不再提前拦截 |
| 8 | `EnsureMainWindow` / `EnsureSystemTray` / `Exit` / `FindInstanceForKey` | 前三个同名同语义；`FindInstanceForKey` 不移植（Linux 单实例走 socket，没有按 key 枚举的等价物） | `Exit()` 保留 `GamepadController.RestoreGamepadGuideButtonForGameBar()` 前置调用 |
| 9 | `App.Current` | `public static new App Current => (App)Application.Current!` | 同 |

自检工具（`--screenshot` / `--page` / `--settings-section` 等）不属于上游结构，集中在 `App.RunCaptureTooling` 一处，
不参与正常启动路径；`--window settings|account|dailynote` 旧窗口自检随旧 UI 层一起删除。

`Program.cs` 现在只做三件上游 `Program.cs` 也在做的事（崩溃兜底、`--version/--help`）+ 本仓库 CLI 工具
（`--scan-games`、自检参数解析）；`rpc`/`startgame`/`playtime` 子命令不再由 `Program` 拦截，
统一由 `App.HandleStartupAsync` 处理（与上游一致）。
