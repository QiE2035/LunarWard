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
