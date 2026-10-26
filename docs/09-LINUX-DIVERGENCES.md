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

### 8.3 UI 层

| # | 位置 | 上游 | 本仓库 | 原因/待办 |
|---|---|---|---|---|
| 9 | `Features/Screenshot/ScreenshotFolderManageDialog.axaml(.cs)` | `ContentDialog` + `ListView` + `Launcher.LaunchFolderPathAsync` + `FileOpenPicker` | `StarwardContentDialog`（`DialogService.ShowAsync`）+ `ListBox` + `xdg-open` + `FileDialogHelper.PickFolderAsync`（StorageProvider）；`MenuFlyoutSeparator`→1px `Border`，`ProgressRing`→`ProgressBar IsIndeterminate`，模板内按钮提示用 `ContainerPrepared` + `ToolTip.SetTip` | Avalonia 无 ContentDialog/ListView 模板差异/WinRT Launcher |
| 10 | `Features/Screenshot/ScreenshotPage2.axaml(.cs)` | `ItemsView` + `UniformGridLayout` + `AnnotatedScrollBar`（含标签轨道/`ScrollController`/`PART_ToolTipRail` tooltip）+ Storyboard + `mxi:Interaction.Behaviors` | `ScrollViewer` + `ItemsControl` + `WrapPanel`（条目固定 204×115、间距 12）+ 原生 `ScrollBar`；悬停动画改 `Style :pointerover` + `Transitions`；保留「内容超过 3 屏才显示滚动条」判定；`DispatcherQueue.TryEnqueue`→`Dispatcher.UIThread.Post`；`DataPackage` 拖拽→`DataObject`+`DragDrop.DoDragDrop`；复制图片→`Clipboard.SetFilesAsync` | Avalonia 无 ItemsView/AnnotatedScrollBar/Storyboard/Behaviors；**年/月标签绘制省略**（滚动条保留），后续若要标签可自绘轨道 |
| 11 | `Features/Screenshot/ImageViewWindow2.axaml(.cs)` | `CanvasSwapChainPanel` + Win2D（HDR→SDR、显示器峰值亮度）、视频/GIF 播放、图库条、JxlSharp 导出、裁剪/EXIF 编辑 | 保留顶栏命令（复制/另存为/文件夹/删除/信息）、滚轮锚点缩放、按住拖动、双击还原、←/→ 翻页、删除后跳转并同步集合；渲染用 `Image` + `ScaleTransform`/`TranslateTransform` | Win2D/视频/GIF/图库条/编码导出本轮未落；**这是 1572 行上游文件的子集移植**，缺口登记如下 |

### 8.4 尚未移植（明确缺口，非「已完成」）

| 上游文件 | 阻塞原因 | 计划 |
|---|---|---|
| `Features/Screenshot/ScreenCaptureHelper.cs`、`ScreenCaptureContext.cs`、`ScreenCaptureService.cs` | 依赖 `Windows.Graphics.Capture` + `Direct3D11CaptureFramePool` + Win2D + WinRT `DisplayInformation`/HDR；Linux 需外部抓屏后端（X11 `xdotool`+`import`/`xwd`，Wayland `grim`） | Lead 已定「B 后接 A」：先 UI，后抓屏后端；落地时 `BuildXMPMetadata`/`GetMaxCLL` 按上游签名给纯函数实现（HDR 元数据只写不实采），抓屏 API 不可用时 `PlatformNotSupportedException` 且 UI 显示「截图功能不可用」 |
| `Features/Screenshot/ScreenCaptureInfoWindow.xaml(.cs)` | 同上，且依赖 `Features/Overlay/RunningGame`（由 misc-port 负责，尚未落地） | 抓屏后端完成后一并移植 |
| `Features/Screenshot/ScreenshotPage.xaml(.cs)` | 695 行；`GridView` + 分组 `CollectionViewSource` + 多选 + 右键菜单（信息/打开/复制文件/复制为 JPG/在文件夹中/打开方式/删除）+ 调用 `ImageBatchConvertWindow` | 下一步优先（导航按 `nameof(ScreenshotPage)` 注册，是真实入口）；现有旧实现 `Views/Starward/Pages/ScreenshotPage.axaml(.cs)` 可作行为参照，但需按上游结构重排 |
| `Features/Screenshot/ImageViewWindow.xaml(.cs)` | 上游早期单图查看器（被 `ImageViewWindow2` 取代），当前仓库无调用方 | 低优先；最后补形状 |
| `Features/Screenshot/ImageBatchConvertWindow.xaml(.cs)` | 909 行；依赖 JxlSharp/ImageSharp/WIC 编码器与 `ScreenCaptureService.BuildXMPMetadata` | 与图片编解码（task-9 `Features/Codec`）一起做 |
