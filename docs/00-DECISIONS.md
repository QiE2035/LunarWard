# 00 — 已定决策

本文件记录**已经拍板、不再讨论**的决策及其理由。改动任何一条都需要需求方重新确认。

---

## D1. 定位：Starward 的 Linux 重实现

**决策**：本项目是 Starward 在 Linux 的重实现，不是 AGL 的分支或改造。

**理由**：需求方明确表述——"我需要的是 Starward 风格的启动器，甚至可以说是 Starward
在 Linux 的移植/重实现"。

**否决的方案**：
- ❌ Fork AGL 并在其上做增量。AGL 是一个**差距较大的同类项目**，改造它不等于实现
  Starward。且 AGL 缺少本项目要的核心能力（见 D2）。
- ❌ 引入 AGL 的 `agl-core` / `agl-runtime` / `agl-games` 作为依赖。

---

## D2. 复用 `Starward.Core`，不重写

**决策**：把 `Starward.Core` 作为独立项目原样引用，只做最小改造。

**核查证据**：

| 事实 | 证据位置 |
|---|---|
| 目标框架 `net10.0`（**非** `net10.0-windows`） | `Starward.Core/Starward.Core.csproj` |
| **零** `<PackageReference>`，纯 BCL | 同上 |
| 236 个 `.cs` 文件 | `Gacha` 28 / `GameRecord` 154 / `HoYoPlay` 18 / `Metadata` 8 / `SelfQuery` 11 / `JsonConverter` 5 / `GameNotice` 3 |
| **仅 1 处**真正调 Windows 注册表 | `Gacha/GachaLogClient.cs:299` `Registry.GetValue(...)` |
| 其余"Windows"只是路径**字符串常量** | `GameBiz.cs:146-154` 引用 `GameRegistry.GamePath_*` |
| 已用 `System.Text.Json` source-gen | 6 个 `*JsonContext.cs`，AOT 友好、无反射 |

**改造点（仅此三处）**：
1. `GachaLogClient.cs:299` → 抽出 `IGameInstallPathProvider` 接口；
   Linux 实现读 Wine prefix 的 `user.reg`（或返回 `null`）
2. 移除未使用的 `using Microsoft.Win32;`
3. 其余**一行不改**

**理由**：这是"不重复造轮子"在此项目上的最大兑现点——直接继承抽卡、便笺、公告、
HoYoPlay、UIGF、SelfQuery 的全部 API 客户端与数据模型。

---

## D3. UI 框架：C# + Avalonia

**决策**：C# + .NET 10 + **Avalonia 12.1.4**（`Avalonia` / `Avalonia.Desktop` /
`Avalonia.Themes.Fluent`）。

### 修订记录

本决策曾一度定为 **GirCore（GTK4 绑定）**，后被推翻。推翻的理由如下，
记录在此以免重蹈：

**原错误理由**："Avalonia 外观是 Fluent/Windows 风格，与 Linux 桌面割裂。"

**为什么错**：本项目定位是 **Starward 的 Linux 重实现**，而 Starward 就是
WinUI 3 + Fluent Design。实测其 XAML：`AcrylicBrush` 92 次、`ThemeShadow` 62 次、
`CornerRadius` 371 次。**Avalonia 的默认主题正是 Fluent**，设计语言天然对齐；
GTK4/libadwaita 的 Adwaita 语言反而与 Starward 不匹配。

### 数据对比

| | Avalonia | GirCore |
|---|---|---|
| Stars | **31,698** | 524（差 60 倍） |
| Forks | 2,863 | 54 |
| 最后推送 | **2026-10-22** | 2026-10-16 |
| 稳定版 | **12.1.4**（2026-10-09） | 0.8.1 |
| 历史版本数 | 163 | 22 |
| 许可 | MIT | MIT |

### Avalonia 消解的两个原本风险

| 问题 | GirCore 方案 | Avalonia 方案 |
|---|---|---|
| 环境 shell 的终端 | `GirCore.Vte-2.91` **在 NuGet 不存在**，需手写 ~300 行 `libvte` P/Invoke | `Iciclecreek.Avalonia.Terminal` 4.1.0 / `RoyalApps.RoyalTerminal.Avalonia` 0.6.3 / `AvaloniaTerminal` 1.0.0 |
| 循环视频背景 | `gtk::MediaFile` 依赖 `gtk4paintablesink`（独立包 `gst-plugin-gtk4` 提供） | `LibVLCSharp.Avalonia` 3.10.2，不依赖系统 GStreamer 插件 |

### 新增的两个风险（如实记录）

1. **Avalonia 12 尚新**：2026 年的新大版本（`12.0.0` → `12.1.4` 已 12 个补丁版）。
   必要时退到 `11.3.22`（仍在维护）。
2. **Acrylic/模糊在 Linux 无法真实现**：X11/Wayland 各组合器对 backdrop blur
   支持不一致，Avalonia 无跨平台 API。需分级降级（见 `04-FEATURES.md` §5.6）。

### 否决的方案

- ❌ **Rust + relm4**。技术上完全可行且是我最初的推荐，但会导致 `Starward.Core`
  的 236 个文件零复用——那样项目就变成"用 Rust 从零写一个 Starward"，与本项目定位
  直接冲突。
- ❌ **GirCore（GTK4 绑定）**。见上面的修订记录。唯一占优处是"原生 GTK 观感"，
  但那不是本项目目标。

### 核验记录

| 项 | 值 |
|---|---|
| `Avalonia` | 稳定版 **12.1.4**（2026-10-09） |
| `Avalonia.Desktop` / `Avalonia.Themes.Fluent` | 12.1.4 |
| `Avalonia.Controls.ItemsRepeater` | 12.0.0 |
| `LibVLCSharp.Avalonia` | 3.10.2 |
| 许可 | **MIT** |
| 维护 | GitHub `AvaloniaUI/Avalonia` 31,698★，**2026-10-22 仍在推送** |
| 目标框架 | `net10.0` 原生支持 |

### XAML 迁移工作量（实测）

| 项 | 数量 |
|---|---|
| XAML 文件 | 82 |
| XAML 行数 | **21,508** |
| `{x:Bind}` | **2,392 处**（`{Binding}` 仅 56） |
| code-behind（`.xaml.cs`） | 79 |
| 纯逻辑 `.cs` | 153 |

常用控件 `TextBlock`(1009)、`StackPanel`(491)、`Grid`(351)、`Button`(225)、
`Image`(189)、`Border`(143)——**Avalonia 全有对应**。

需绕的 WinUI 专有项：

| WinUI | 次数 | Avalonia 对策 |
|---|---|---|
| `AcrylicBrush` | 92 | `BlurEffect` + 半透明层（分级降级） |
| `ThemeShadow` | 62 | `DropShadowEffect` |
| `SystemBackdrop` | 15 | 手动实现 |
| `InfoBar` | 9 | 自拼 |
| `ItemsRepeater` | 14 | 官方移植版 |
| `NavigationView` | 10 | 社区移植版 |

**结论**：XAML 是"系统性改写 21.5k 行"，不是"从零重写"。
GirCore 路线没有 XAML 对应物，视图层等于全部重写，还丢掉 Fluent 观感。

---

## D4. 多线程下载：不做

**决策**：**不实现**新的分段并发下载器。直接复用 Starward 已有的并行下载。

**理由（已实测核查）**：需求方质疑"Starward 是不是已经按 diff chunk 并行下载了"，
核查后确认——**是的，已经并行了**：

```csharp
// Starward.RPC/GameInstall/GameInstallService.cs:362  (Chunk 模式)
await Parallel.ForEachAsync(context.TaskFiles ?? [], cancellationToken,
    async (GameInstallFile file, CancellationToken token) =>
{
    await _polly.ExecuteAsync(async token =>
        await _gameInstallHelper.DownloadChunksToFileAsync(context, file, false, token), token);
    file.IsFinished = true;
});
```

配套机制：
- **文件级并行**：`Parallel.ForEachAsync` 无显式上限（默认 = CPU 核数）
- **文件内 chunk 串行**，按 `chunk.Offset` 定位写入
- **`Polly` 重试**：`WaitAndRetryAsync(3, i => TimeSpan.FromSeconds(i))`
- **HTTP**：`SocketsHttpHandler` + `EnableMultipleHttp2Connections` +
  `EnableMultipleHttp3Connections` + `PooledConnectionLifetime = 5min`
- **免下载优化**：`HardLinkAsync`（同盘硬链接）+ `CheckFileMD5Async`（已存在则跳过）
- 另有 `GameInstallHelper.cs:742/792` 用 `Parallel.ForEach` 做 diffmap 与文件校验

**结论**：Starward 的 chunk 模型本身就按 offset 切片，文件级并行已吃满带宽。
单文件多连接分段属于重复劳动。

> ⚠️ 注意：`agi-core`（AGL）与 `anime-game-core` 的下载器**确实是**单连接。
> 如果将来需要参考"不并行的反例"，看这两个；但本项目不用它们。

---

## D5. 外部程序模型：只有一种东西

**决策**：后启动与补丁注入**是同一个概念**，不做区分。统一为 `ExternalProgram`。

**理由**：需求方原话——"后启动可以手动启动，一个类似于『伴随启动』的开关，
打开后就是补丁，关闭后也可以充当临时禁用补丁"。

**模型**：

```csharp
public enum Trigger
{
    Auto,    // 伴随启动：游戏 Ready 后自动拉起 —— 这就是"补丁注入"
    Manual,  // 手动：用户在 UI 里点"启动"才拉起 —— 这就是"后启动"
}
```

| 用户意图 | 配置 |
|---|---|
| 后启动（手动） | `Trigger.Manual` |
| 补丁注入（随游戏启动） | `Trigger.Auto` |
| 临时禁用补丁 | `Enabled = false`（保留配置，不删除） |

**否决的设计**：❌ `InjectSpec` 类型。实测 `hk4e-touch.exe` 源码确认注入器**自成一体**：

```
解析参数 → 扫描目标进程(find_timeout) → OpenProcess
       → Sleep(inject_delay) → 隐藏控制台 → 远程线程 LoadLibraryW
       → WaitForSingleObject(INFINITE) → 回传游戏退出码
```

它自己完成全部流程，启动器只需按普通程序拉起并（可选）等待。所以"注入"不需要
专门的数据结构——把参数写进 `Args` 即可。

---

## D6. 不预置任何具体项目

**决策**：交付版本**不包含**空荧酒馆、BetterGI、Hk4eTouch、ZZZ-touch-unlocker 等
任何具体程序的集成。全部由用户手动选文件、配参数。

**理由**：需求方明确——"后启动和补丁只是例子，实际交付时不包括具体的项目"。

**保留的只是在用户选完文件后的启发式提示**（非强制、非阻止）：
- 文件名匹配 `*inject*` / `*touch*` → 提示"注入类程序建议设为『伴随启动』"
- `Trigger.Auto` + `WindowsExe` → **警告**"这会是该 prefix 的第二个 wine 客户端，
  可能破坏游戏保护期初始化"。仅告知，不阻止。

---

## D7. 环境变量三态：覆盖与删除必须可区分

**决策**：env 合并用三态，`Override("")` 与 `Remove` **语义不同**，不能等价。

```csharp
public enum EnvPatchKind { Inherit, Override, Remove }
```

**理由**：对 wine 而言"变量存在但为空"与"变量不存在"是**不同的行为**。
真实案例：`winewayland` 模式需要 `DISPLAY=""`（存在且为空），而不是删除 `DISPLAY`。

**UI 设计**：
- 三态控件：`继承 / 覆盖 / 删除`
- 输入框清空 → 默认视为 `Remove`（更符合直觉）
- 另提供"设为空值"勾选，用于表达 `Override("")`

> 待确认：见 `06-OPEN-QUESTIONS.md` Q1。

---

## D8. 区服切换用官方 Ini Binder，不硬拼字符串

**决策**：用 `Microsoft.Extensions.Configuration.Ini`（Starward 自己就在用这个库）
序列化/反序列化 `config.ini`，**不手写** `format!` 拼串，也不手写解析。

**理由**：需求方要求"不要硬拼 ini，考虑引入个序列化/反序列化库"。而且
Starward 的 `GameConfigIni.cs` **正是**用这个库 + `[ConfigurationKeyName]` 特性，
直接照搬即可保证逐字节一致。

**配置结构**（原样搬自 `Starward/Features/GameLauncher/GameConfigIni.cs`）：

```csharp
public class GameConfigIni
{
    [ConfigurationKeyName("channel")]          public int Channel { get; set; }
    [ConfigurationKeyName("sub_channel")]      public int SubChannel { get; set; }
    [ConfigurationKeyName("cps")]              public string Cps { get; set; }
    [ConfigurationKeyName("game_version")]     public Version? GameVersion { get; set; }
    [ConfigurationKeyName("sdk_version")]      public Version? SdkVersion { get; set; }
    [ConfigurationKeyName("game_biz")]         public GameBiz GameBiz { get; set; }
    [ConfigurationKeyName("uapc")]             public string Uapc { get; set; }
    [ConfigurationKeyName("downloading_mode")] public string DownloadingMode { get; set; }
}
```

**额外要求**：必须**保留未知键**再写回（游戏版本更新可能加字段，不能丢），
写入用原子替换（`.tmp` → `rename`）。

---

## D9. 运行时的 Proton 默认模式：WineBinary

**决策**：默认模式下直接调用 `Proton/dist/bin/wine`，不走 `proton run` 脚本。

**理由**：`proton run` 会多一层 python → wine 的进程链，**可能破坏"游戏是首个
wine 客户端"这条不变式**（见 D10）。在实测验证之前，`ProtonScript` 只能作为
实验开关并标注风险。

---

## D10. 启动顺序不变式不可妥协

**决策**：**游戏必须是该 wine prefix 的第一个 wine 客户端。** 这是架构级约束，
不是建议，不是优化项。

**实证来源**（三个独立项目交叉验证，详见 `01-RESEARCH.md`）：
- `ZZZ-touch-unlocker/wine/README.md`：非首个客户 → `NtRaiseHardError 0x50000018`
  → **无窗口、无日志、约 5 秒静默消失**
- `Hk4eTouch/README.md`：用 `CreateProcessW` 启动（哪怕完全不注入）→
  `ZwLoadDriver ... HoYoProtect: c0000355` → `NtRaiseHardError` → `0xC0000005`
- 需求方自己的 `wine/zzz-touch-unlocker` 脚本已实现正确模型

**推论**：
1. 启动前必须清空 prefix 并**确认清零**
2. 任何 `Trigger.Auto` 的外部程序只能在 `AwaitReady` 相位之后拉起
3. **不提供"游戏启动前运行程序"的能力**——那正是破坏不变式的行为。
   `Trigger` 只有 `Auto` / `Manual` 两个值（见 `06-OPEN-QUESTIONS.md` Q9）
4. Proton 默认走 `WineBinary`（D9）

---

## D11. 不打包任何补丁二进制

**决策**：不随发行包分发 `Hk4eTouch` / `ZZZ-touch-unlocker` 等补丁程序或 DLL。

**理由**：
- `Hk4eTouch` 是 **AGPL-3.0**（网络传染性更强）
- `ZZZ-touch-unlocker` 仓库**没有 LICENSE 文件**
- 保持"独立程序、子进程调用"的边界，用户自备即可规避分发风险

与 D6（不预置具体项目）一致。

---

## D12. 删除 `lunarward-download` 独立模块

**决策**：原计划的独立下载器项目取消。

**理由**：见 D4。Starward 的下载已并行且质量足够（Polly 重试、HTTP/2+3 多连接、
硬链接去重、MD5 跳过）。

**保留的移植工作**：把 `Starward.RPC/GameInstall` 的下载逻辑搬到 Linux，
去掉 gRPC/protobuf 层（改为进程内调用）与 `Vanara.PInvoke.Kernel32`
（`DriveHelper` 换 `DriveInfo`）。

---

## D13. 解决方案文件用 `.slnx`

**决策**：用 `LunarWard.slnx`（XML 格式），**不用**传统的 `LunarWard.sln`。

**理由**：
1. **Starward 自己就是 `Starward.slnx`**，与上游工程习惯一致
2. XML 可读、可 diff；传统 `.sln` 是自定义文本格式，合并冲突难解
3. .NET 10 SDK 原生支持

**已实测验证**（本机 `dotnet 10.0.302`）：

```bash
$ dotnet new sln --format slnx -n Probe     # → <Solution></Solution>
$ dotnet sln Probe.slnx add Lib/Lib.csproj  # → <Project Path="Lib/Lib.csproj" />
```

创建与添加项目均正常，**无需手写 XML**。

**注意**：本项目只面向 Linux，`Configurations` 保留 `Any CPU` 即可，
不需要 Starward 那样的 x86/x64/ARM64 矩阵。

详见 `02-ARCHITECTURE.md` 的「解决方案文件格式」一节。
