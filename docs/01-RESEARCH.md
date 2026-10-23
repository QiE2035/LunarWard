# 01 — 调研事实记录

本文件保存调研阶段得到的**可验证事实**，避免后续会话重复调研或凭猜测行事。
每条都标注了证据来源。

参考项目根目录：
- `/home/qie2035/projects/cpp/hk4e-touch`
- `/home/qie2035/projects/cpp/ZZZ-touch-unlocker`
- `/home/qie2035/projects/csharp/Starward`
- `/home/qie2035/projects/rust/an-anime-team`（含 `anime-launcher-sdk` / `an-anime-game-launcher` / `sleepy-launcher`）

---

## 一、启动顺序不变式（最重要的发现）

### 1.1 症状

游戏启动后**无窗口、无报错、约 5 秒后进程静默消失**，Unity 一个字节日志都不写。

### 1.2 根因

HoYoKProtect（`mhypbase.dll` + `HoYoKProtect.sys`）的驱动加载路径对
**"谁是这台 wineserver 的第一个客户程序"** 极其敏感：

- 只有当**游戏本身是该 prefix 里 wineserver 启动后的第一个 wine 客户程序**时，
  `ZwLoadDriver` 才会真正把保护驱动拉起来（能看到 `winedevice.exe` 被创建、
  游戏正常进 Unity）。
- 一旦在此之前有**任何** wine 进程连上同一台 wineserver（另一个程序、
  `wine cmd /c exit`、甚至只是一个 wineserver 客户），驱动就不会被 spawn，
  游戏随后调用 `NtRaiseHardError 0x50000018`，而 Wine 把这个 API 实现成 stub。

### 1.3 受控实验记录

来源：`ZZZ-touch-unlocker/wine/README.md` §1（在原神 `HoYoKProtect.sys` 上做了 10 组）

| 组 | wineserver 状态 | 结果 |
|---|---|---|
| 9 | 清空后冷启动，游戏是第一个客户 | 正常：`winedevice.exe` 出现，DXVK 初始化，进入加载界面 |
| 10 | 先用 `wine cmd /c exit` 占住 server，再启动游戏 | 5 秒后 `NtRaiseHardError 0x50000018` 退出 |

### 1.4 `CreateProcessW` 路径同样致命

来源：`Hk4eTouch/README.md`（原神 7.0 国服实测）

> 用 `CreateProcessW`（哪怕完全不注入任何 DLL）启动 `YuanShen.exe`，mhypbase 的
> 驱动装载会失败并自杀：`ZwLoadDriver ... HoYoProtect: c0000355` →
> `NtRaiseHardError 0x50000018` → 进程以 `0xC0000005` 退出，`driverError.log`
> 写着 `initDriver Failed`。由 Unix 侧直接 `wine YuanShen.exe` 启动则完全正常。

**推论**：启动器**必须**从 Unix 侧 `wine <game>.exe` 拉起游戏，不能通过
Wine 内部的 `CreateProcessW`。注入必须在游戏跑起来之后以**第二个**客户端身份接入。

### 1.5 第二客户在保护期之后接入是安全的

来源：同上

> 被测试过的场景里，游戏启动完成后再 attach 第二个 wine 程序，游戏进程不受影响、
> 日志继续正常输出。

但**注入太早仍然会死**：

> 第 5 步是这次移植的核心保护：**注入得太早 = 在保护期里当第二个客户 = 游戏自杀**。
> 所以先等信号，信号没来就等 grace，然后再注入。

### 1.6 进程发现的正确方式

来源：`ZZZ-touch-unlocker/wine/README.md` §2

> 第 4 步不能看进程名：runner 的 steam stub 会把 unix 进程名固定成 `steam.exe`，
> 真正的游戏是被它 fork 出来的子进程，只有镜像映射是可靠的"游戏已加载"信号。
> 同理，Linux pid ≠ Windows pid，所以注入器用 Toolhelp 按镜像名找目标（`--name`），
> `--pid` 只在明确知道 Windows 侧 pid 时才用。

**实现要求**：
- 用 `/proc/<pid>/environ` 含 `WINEPREFIX=<本 prefix>` 判定"是本 prefix 的客户"
- 用 `/proc/<pid>/maps` 含镜像名判定"游戏已加载"
- 排除僵尸进程（`state == "Z"`）与自身 pid
- 按 `WINEPREFIX` **精确匹配**，不要误杀其他 prefix 的 `wineserver`

### 1.7 触摸送达是 runner 的属性，与启动器无关

来源：`ZZZ-touch-unlocker/wine/README.md` §9（本机实测）

> **触摸能不能用，取决于 runner 的 `win32u` 有没有实现触摸窗口 API，和本项目无关。**
> 上游 Wine 的 `NtUserIsTouchWindow` 是 `@ stub -syscall`，`WM_TOUCH` 只有宏定义
> 没有发送方；部分下游构建（CachyOS、dwproton 系）补了这套实现，TkG staging 没有。

| runner | `WM_TOUCH` | 结论 |
|---|---|---|
| `spritz-wine-cachyos-wow64-10.0-11` | 送达（`IsTouchWindow -> 1`，一次 300ms 触摸收到 ~20+ 条 `WM_TOUCH`） | ✅ 用这个 |
| `spritz-wine-tkg-staging-wow64-11.19-1` | 不送达（`RegisterTouchWindow` fixme stub，`NtUserIsTouchWindow` 未实现会直接崩进程） | ❌ 别用 |
| `wine-dwproton` / `wine-staging-tkg` / `soda` | 未实测 | 装后先跑探针 |

**推论**：不要承诺"本项目让触摸可用"——这是 runner 的职责。UI 可提供 runner
能力提示，但不该为此写代码。

---

## 二、注入器的自包含性

来源：`Hk4eTouch/src/injector/main.cpp` 实测

`hk4e-touch.exe` 的完整流程：

```
解析参数（game.exe / --delay-ms N / --timeout-ms N）
  → FindRunningGame(game_name, find_timeout_ms)   默认 find 120000ms
  → OpenProcess(SYNCHRONIZE | QUERY_INFORMATION | CREATE_THREAD | VM_OPERATION | VM_WRITE | VM_READ)
  → Sleep(inject_delay_ms)                        默认 10000ms
  → GetConsoleWindow() + ShowWindow(SW_HIDE)      隐藏控制台
  → InjectDll(process, dll_path)                  远程线程 LoadLibraryW
  → WaitForSingleObject(process, INFINITE)        前台等待整场
  → GetExitCodeProcess → 回传游戏退出码
```

关键点：
- **DLL 必须与注入器同目录**：`const std::wstring dll_path = Join(injector_dir, kDllName);`
  （按注入器自身模块路径找，不是游戏目录）
- 默认游戏名：`kGameNames[][32] = {L"YuanShen.exe", L"GenshinImpact.exe"}`
- 注入失败不杀游戏，只写日志
- 日志：注入器同目录 `hk4e-touch.log`，不可写则退到 `%TEMP%`

**结论**：启动器只需把它当普通程序拉起，把参数写进 `Args`。
不需要专门的"注入"数据结构（见 `00-DECISIONS.md` D5）。

### 2.1 另一种补丁的模式文件机制

来源：`ZZZ-touch-unlocker/wine/README.md` §2

`zzz-touch-unlocker` 用 `<dll 目录>/zzz-touch-mode.txt` 传递模式（`0`/`1`），
因为"附着过程不需要导入环境变量"。环境变量优先级更高，但注入器不便设置它。

- 文件写在 **DLL 所在目录**（prefix 之外），**不写进游戏目录**
- hook 侧读顺序：环境变量 → 模式文件

---

## 三、区服切换（B 站渠道）

### 3.1 机制：`config.ini`

来源：`anime-launcher-sdk/src/games/genshin/env_emulation.rs`（需求方 fork 的实现，
已有单元测试）

渠道由**游戏目录下的 `config.ini`** 决定，格式：

```ini
[General]
channel=14
cps=bilibili
game_version=5.0.0
sub_channel=0
```

完整映射：

| 渠道 | channel | cps | sub_channel |
|---|---|---|---|
| 官方国服 | 1 | `mihoyo` | 0 |
| Bilibili | **14** | **`bilibili`** | **0** |
| Epic | 1 | `pcseaepic` | 3 |
| Google Play | 1 | `pcgoogle` | 6 |

已有单元测试断言（`env_emulation.rs` 的 `mod tests`）：

```rust
assert_eq!(
    Environment::Bilibili.generate_config("5.0.0"),
    "[General]\nchannel=14\ncps=bilibili\ngame_version=5.0.0\nsub_channel=0"
);
```

### 3.2 缺失插件会**静默回落**官服

来源：`env_emulation.rs:142-144` 注释

> The game silently falls back to the official (HoYoverse) server when this
> plugin is missing, so the launcher verifies its presence before launching.

**这是最坑的失败模式**：用户以为切到了 B 站，实际进了官服，且无任何提示。

**三个必要条件**（缺一不可）：
1. `edition == China`（游戏是 `YuanShen.exe`，不是 `GenshinImpact.exe`）
2. `YuanShen_Data/Plugins/PCGameSDK.dll` 存在且非空
3. 下载该 DLL 时需要**浏览器 UA + `Referer`**

### 3.3 插件下载的两个陷阱

来源：`env_emulation.rs`

**陷阱一：CDN 403**

```rust
const BILIBILI_USER_AGENT: &str = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) \
    AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
const BILIBILI_REFERER: &str = "https://open.biligame.com/";
```

> The SDK package CDN returns 403 without a browser-like User-Agent
> — and these headers must be set for the **initial HEAD request** as well.

这正是 `anime-game-core` 那条 `JohnTheCoolingFan@downloader-features` fork 分支
存在的**唯一理由**（给 `Downloader` 加自定义 header 支持）。

**陷阱二：zip 条目名是 GBK 编码**

```rust
/// Entry names in the package are GBK-encoded (so `64位` is stored as
/// `64\xce\xbb`), but they also contain a Unicode path extra field with a
/// UTF-8 name which the `zip` crate prefers. Both representations start with
/// `64`, so the entry is matched by that prefix and doesn't depend on which
/// one of them is used.
const BILIBILI_PLUGIN_ENTRY_SUFFIX: &str = "/PCGameSDK.dll";
const BILIBILI_PLUGIN_ENTRY_ARCH_PREFIX: &str = "64";
```

匹配逻辑：`name.EndsWith("/PCGameSDK.dll") && name.Split('/').Any(p => p.StartsWith("64"))`

**做法**：下载到 `.part` 后再 rename（缓存永远完整）；安装也写 `.part` 再 rename。

### 3.4 ZZZ 没有 B 站渠道

来源：`anime-launcher-sdk/src/games/zzz/env_emulation.rs`（77 行）

只有 `PC` / `Epic` / `Android` 三个变体，**没有** `Bilibili`。
UI 必须按游戏隐藏该选项。

### 3.5 Starward 的 `config.ini` 模型

来源：`Starward/src/Starward/Features/GameLauncher/GameConfigIni.cs`（40 行）

Starward 用 `Microsoft.Extensions.Configuration.Ini` + `[ConfigurationKeyName]`，
字段全集：`channel` / `sub_channel` / `cps` / `game_version` / `sdk_version` /
`game_biz` / `uapc` / `downloading_mode`。

**本项目直接照搬这个类与这个库。**

---

## 四、Starward 的可移植性核查

### 4.1 `Starward.Core` —— 高可移植（核心结论）

| 核查项 | 结果 | 证据 |
|---|---|---|
| TargetFramework | **`net10.0`**（非 `-windows`） | `Starward.Core.csproj` |
| PackageReference | **0 个** | 同上 |
| 文件总数 | 236 | `find . -name "*.cs" \| wc -l` |
| 目录分布 | Gacha 28 / GameRecord 154 / HoYoPlay 18 / Metadata 8 / SelfQuery 11 / JsonConverter 5 / GameNotice 3 / Localization 1 | 同上 |
| 真正调 Windows API | **1 处** | `Gacha/GachaLogClient.cs:299` |
| 其余 Windows 痕迹 | 注册表路径**字符串常量** | `GameBiz.cs:146-154` |
| JSON | 6 个 source-gen context | `*JsonContext.cs` |
| Sqlite/Dapper | 0 | grep 结果 |

那唯一的一处：

```csharp
// Gacha/GachaLogClient.cs:299
var launcherPath = Registry.GetValue(regKey, "InstallPath", null) as string;
// 上方 line 1: using Microsoft.Win32;
```

### 4.2 `Starward.RPC` —— 中等可移植

| 项 | 值 |
|---|---|
| TargetFramework | `net10.0-windows` ⚠️ |
| Windows 相关包 | `Vanara.PInvoke.Kernel32 5.0.7`、`Starward.NativeLib 0.3.1` |
| 下载相关包 | `Grpc.AspNetCore`、`Microsoft.Extensions.Http.Polly`、`Polly 8.7.0`、`ZstdSharp.Port 0.8.8` |

**下载逻辑本身是纯 .NET**，障碍只在 gRPC 层与 P/Invoke：

| 文件 | 用途 | 移植难度 |
|---|---|---|
| `GameInstall/GameInstallService.cs` | 主流程（并行下载） | 低（去 gRPC 调用） |
| `GameInstall/GameInstallHelper.cs` | chunk 下载、MD5、硬链接 | 低 |
| `GameInstall/FileSliceStream.cs` | 分片流 | 无（纯 .NET） |
| `GameInstall/DiffMap.cs` | 差量映射 | 无 |
| `GameInstall/GamePackageService.cs` | 包列表 | 低 |
| `DriveHelper.cs` | 磁盘空间 | 低（换 `DriveInfo`） |

### 4.3 Starward 主工程 —— 不可移植

`net10.0-windows10.0.26100.0` + `UseWinUI=true` + 大量
`CommunityToolkit.WinUI.*` / `Microsoft.Graphics.Win2D` / `ComputeSharp.D2D1` /
`Vanara.PInvoke.DwmApi`。视图层必须重写，但 **ViewModel 与 Service 大多可复用**。

### 4.4 Starward 各功能模块规模

| 目录 | .cs 文件数 | 说明 |
|---|---|---|
| `Features/GameRecord` | 29 | 便笺/深渊/虚构等（Core 侧 154） |
| `Features/Gacha` | 32 | 抽卡（Core 侧 28） |
| `Features/GameLauncher` | 18 | 启动 |
| `Features/Screenshot` | 16 | 截图 |
| `Features/Setting` | 14 | 设置 |
| `Features/ViewHost` | 13 | WinUI 特有，**不移植** |
| `Features/PlayTime` | 12 | 时长 |
| `Features/Background` | 9 | 背景 |
| `Features/Codec` | 8 | 编解码 |
| `Features/Update` | 7 | 自更新 |
| `Features/GameInstall` | 6 | 安装 |
| `Features/GameSetting` | 5 | 画质 |
| `Features/GameAccount` | 4 | 账号 |
| `Features/GameSelector` | 4 | 选择器 |
| `Features/Overlay` | 3 | **不做** |
| `Features/SelfQuery` | 3 | 抽卡查询辅助 |
| `Features/UrlProtocol` | 2 | URL 协议 |
| `Features/Database` | 2 | 数据库 |
| `Features/GamepadControl` | 2 | **不做** |
| `Features/CloudGame` | 2 | 云游戏 |
| `Features/HoYoPlay` | 1 | |
| `Features/RPC` | 1 | Discord RPC |

---

## 五、Starward 的公告与背景

### 5.1 公告 API

来源：`Starward.Core/GameNotice/GameNoticeClient.cs`

**红点查询**（原神国服）：
```
https://hk4e-ann-api.mihoyo.com/common/hk4e_cn/announcement/api/getAlertAnn
  ?bundle_id=hk4e_cn&channel_id=1&game=hk4e&game_biz=hk4e_cn
  &lang={lang}&level=60&platform=pc&region=cn_gf01&uid={uid}
```

响应模型 `AlertAnn`：
```csharp
[JsonPropertyName("alert")]        bool Alert;
[JsonPropertyName("alert_id")]     int AlertId;
[JsonPropertyName("remind")]       bool Remind;
[JsonPropertyName("extra_remind")] bool ExtraRemind;
```

**完整公告页**（WebView 打开）：
```
https://sdk.mihoyo.com/hk4e/announcement/index.html?auth_appid=announcement...
```

### 5.2 背景/版本亮点 API

来源：`Starward.Core/HoYoPlay/HoYoPlayClient.cs:129,146`

```
BuildUrl("getAllGameBasicInfo", launcherId, language)
  → 也支持追加 &game_id={id}
```

响应：`List<GameBackgroundInfo>`，其中 `GameBackground` 字段：

```csharp
[JsonPropertyName("background")] GameImage Background;   // 背景图
[JsonPropertyName("icon")]       GameImage Icon;         // 版本亮点图标
[JsonPropertyName("video")]      GameImage Video;        // 仅 Url 有效
[JsonPropertyName("theme")]      GameImage Theme;        // 视频上的叠加图
[JsonPropertyName("type")]       string Type;

const string BACKGROUND_TYPE_UNSPECIFIED = "BACKGROUND_TYPE_UNSPECIFIED";
const string BACKGROUND_TYPE_VIDEO       = "BACKGROUND_TYPE_VIDEO";
const string BACKGROUND_TYPE_POSTER      = "BACKGROUND_TYPE_POSTER";
const string BACKGROUND_TYPE_CUSTOM      = "BACKGROUND_TYPE_CUSTOM";
```

### 5.3 视频背景的两种实现路径（取决于 UI 框架）

**GTK4 路线（已否决）**：`gtk::MediaFile` 走 GStreamer，需要 `gtk4paintablesink`，
由**独立包** `gst-plugin-gtk4` 提供。

本机状态（2026-10-23 实测，需求方已安装）：

```
$ gst-inspect-1.0 gtk4paintablesink
Factory Details:
  Long-name   GTK 4 Paintable Sink
  Klass       Sink/Video
$ pacman -Q gst-plugin-gtk4
gst-plugin-gtk4 0.15.4-1.1
```

非 Arch 发行版（Debian 系为 `gstreamer1.0-gtk4`）可能未装，仍需软探测。

**Avalonia 路线（采用）**：用 `LibVLCSharp.Avalonia`（3.10.2）的 `VideoView`，
只依赖 `libvlc`，**不依赖系统 GStreamer 插件**。这套方案与 UI 框架解耦，
也不受某发行版是否打包 `gst-plugin-gtk4` 的影响。

---

## 六、an-anime-team 生态现状（作为对照，不作为依赖）

### 6.1 两个 SDK 均已停滞

| 项目 | 最后更新 | 状态 |
|---|---|---|
| `anime-launcher-sdk` | 2026-09-23 | README 自述：**"Current implementation is considered legacy. No deep changes will be made."** |
| `anime-game-core` | 2026-09-23 | 同上 |
| `wincompatlib` 0.7.7 | 2025-04-08 | **18 个月未更新**，仓库 `krypt0nn/wincompatlib` 仅 12★ |

### 6.2 组件生态仍然健康

| 仓库 | 最后推送 |
|---|---|
| `an-anime-team/components` | 2026-10-03 |
| `NelloKudo/spritz-wine` | 2026-10-15 |

**当前 wine 组件分组**（来自 live `components.json`）：
`wine-dwproton` / `spritz-wine-tkg` / `spritz-wine-cachyos` / `wine-staging-tkg` / `soda`

> ⚠️ **Kron4ek / Wine-GE / Proton-GE 已不在索引中。** 早期资料若提到它们是过时的。

**dxvk 分组**：`vanilla` / `vanilla-git` / `gplasync`（`DXVK_ASYNC=1`）/
`async`（`DXVK_ASYNC=1`）/ `sarek`

### 6.3 组件索引的文件结构

```
<components>/
├── components.json        # {"wine":[{name,title,features?}], "dxvk":[...]}
├── wine/<group>.json      # 该组的版本数组
└── dxvk/<group>.json
```

单个 wine 版本条目：
```json
{
  "name": "spritz-wine-cachyos-wow64-10.0-11",
  "title": "Spritz-CachyOS 10.0-11",
  "uri": "https://github.com/NelloKudo/Wine-Builds/releases/download/wine-cachyos-aagl-v10.0-11/spritz-wine-cachyos-wow64-10.0-11-x86_64.tar.xz",
  "files": {
    "wine": "bin/wine", "wine64": "bin/wine64",
    "wineserver": "bin/wineserver", "wineboot": "bin/wineboot"
  },
  "features": { "arch": "...", "command": "...", "env": {}, "recommended": true }
}
```

`features.command` / `features.env` 支持关键字替换：
`%build%` `%prefix%` `%temp%` `%launcher%` `%game%`

### 6.4 AGL（anime-games-launcher）—— 只作对照

- v2.2.1，485★，GPL-3.0-or-later，2026-10-21 仍在推送
- 技术栈：**relm4 0.11 + gtk4 0.11 + libadwaita 0.9**（与本项目原选的 Rust 路线一致）
- 架构：Rust 宿主 + **Luau 脚本游戏集成**（`mlua`），游戏定义在 `game-integrations` 仓库
- **已支持原神与绝区零**
- 运行时用 `lua-proton` + **umu-launcher**

**为什么仍然不采用（记录以防后人重复讨论）**：
1. 定位不同——需求方要的是 Starward 的重实现，不是另一个 launcher 的分支
2. AGL **没有** B 站渠道切换
3. AGL **没有**"首个 wine 客户"的顺序保证；`lua-proton` + umu 多一层进程，
   与不变式冲突
4. AGL **没有**通用外部程序模型
5. 采用它等于放弃 `Starward.Core` 的 236 个文件复用

### 6.5 AGL 与 anime-game-core 的下载器都是单连接（反例）

| 下载器 | 行数 | 并行 |
|---|---|---|
| `anime-game-core/src/installer/downloader.rs` | 398 | **单连接**（minreq 同步 + Range 续传） |
| `agl-core/src/network/downloader.rs` | 451 | **单连接流**（reqwest，`while let Some(chunk) = response.chunk()`） |

而 **Starward 是并行的**（见 `00-DECISIONS.md` D4）。这是本项目选择复用
Starward 而非 an-anime-team 的又一理由。

---

## 七、环境核查（本机实测，2026-10-23）

| 项 | 值 | 备注 |
|---|---|---|
| `dotnet` | **10.0.302**（`/usr/share/dotnet/sdk`） | ✅ 需求方已装 |
| `rustc` / `cargo` | 1.99.1 | 路线 A 不用 |
| `cmake` | 4.4.4 | 集成测试需要 |
| `x86_64-w64-mingw32-g++` | 16.2.0 | 集成测试合成"游戏"用 |
| `gtk4` | 4.24.1 | 备选路线才需要（已否决） |
| `libadwaita` | 1.10.0 | 同上 |
| `vte4` | 0.84.1（`vte-2.91-gtk4`） | 同上 |
| `gstreamer` | 1.28.8 | 同上 |
| `gst-plugin-gtk4` | 0.15.4 | 同上（需求方已装） |
| **`libvlc`** | **3.0.24**（`/usr/lib/libvlc.so.5`） | ✅ Avalonia 路线视频背景依赖，已满足 |
| `node` / `pnpm` | 26.11.1 / 有 | 无关 |

已有的 launcher 安装（仅作参考，不依赖）：
`~/.local/share/sleepy-launcher/`（含 `config.json`、`runners/`、`dxvks/`、`prefix/`）

---

## 八、UI 框架版本核查

### 8.1 Avalonia（采用）

| 包 | 版本 | 备注 |
|---|---|---|
| `Avalonia` | **12.1.4**（2026-10-09） | 163 个历史版本 |
| `Avalonia.Desktop` | 12.1.4 | |
| `Avalonia.Themes.Fluent` | 12.1.4 | 91 个版本；**Fluent 与 Starward 的 WinUI 风格对齐** |
| `Avalonia.Themes.Simple` | 12.1.4 | |
| `Avalonia.Controls.ItemsRepeater` | 12.0.0 | 对应 WinUI 的 `ItemsRepeater` |
| `Avalonia.Controls.ColorPicker` | 12.1.4 | |
| `Avalonia.LinuxFramebuffer` | 0.7.x | 嵌入式场景，本项目不用 |

- 许可：**MIT**
- 仓库：`github.com/AvaloniaUI/Avalonia`，**31,698★ / 2,863 forks**，
  **2026-10-22 仍在推送**
- 保守选项：`11.3.22`（2026-09-11，仍在维护）

### 8.2 视频与终端

| 包 | 版本 | 用途 |
|---|---|---|
| `LibVLCSharp.Avalonia` | **3.10.2** | 循环视频背景 |
| `LibVLCSharp` | 3.10.2 | 配套 |
| `Iciclecreek.Avalonia.Terminal` | 4.1.0 | 环境 shell（候选） |
| `RoyalApps.RoyalTerminal.Avalonia` | 0.6.3 | 环境 shell（候选，有跨平台 PTY 抽象） |
| `AvaloniaTerminal` | 1.0.0 | 环境 shell（候选，最轻量） |

### 8.3 GirCore（已否决，仅存档）

| 包 | 最新稳定 | 备注 |
|---|---|---|
| `GirCore.Gtk-4.0` | 0.8.1 | 0.9.0-preview.1 在测（22 个版本） |
| `GirCore.Adw-1` | 0.8.1 | 21 个版本 |
| `GirCore.WebKit-6.0` | 0.8.1 | 18 个版本 |
| `GirCore.Vte-2.91` | **不存在** ⚠️ | 这正是放弃该路线的直接原因之一 |

- 许可 MIT；`gircore/gir.core`，524★，2026-10-16 推送

**否决理由**（完整论述见 `00-DECISIONS.md` D3 的修订记录）：
1. 设计语言不匹配——Starward 是 Fluent，Avalonia 默认就是 Fluent，GTK 是 Adwaita
2. 社区规模差 60 倍（524 vs 31,698）
3. 无 XAML 对应物，视图层要从零重写（Starward 有 21,508 行 XAML）
4. `GirCore.Vte-2.91` 不存在，终端要手写 ~300 行 `libvte` P/Invoke
