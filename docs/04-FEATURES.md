# 04 — 功能设计

本文件覆盖除启动引擎外的全部功能模块。

---

## 一、外部程序（后启动 + 补丁注入）

### 1.1 核心抽象：只有一种东西

**后启动与补丁注入是同一个概念的两种用法**，不做类型区分。

```csharp
public sealed class ExternalProgram
{
    public required string Id { get; set; }
    public string Name { get; set; } = "";          // 用户自由命名
    public bool Enabled { get; set; } = true;

    // ── 程序 ──
    public ProgramKind Kind { get; set; } = ProgramKind.WindowsExe;
    public required string Path { get; set; }       // 用户手动选，绝对路径或 %keywords%
    public List<string> Args { get; set; } = [];
    public string? WorkingDirectory { get; set; }   // null = 继承游戏目录

    // ── 环境变量（三态合并）──
    public List<EnvEntry> Env { get; set; } = [];

    // ── 触发 ──
    public Trigger Trigger { get; set; } = Trigger.Manual;
    public bool Wait { get; set; }                  // 是否前台等待

    // ── 生命周期 ──
    public ExitPolicy OnGameExit { get; set; } = ExitPolicy.Kill;
}

public enum ProgramKind
{
    WindowsExe,     // 走 wine 拉起
    UnixBinary,     // 直接 exec
}

public enum Trigger
{
    Auto,       // 伴随启动：游戏 Ready 后自动拉起 —— 这就是"补丁注入"
    Manual,     // 手动：用户点"启动"才拉起 —— 这就是"后启动"
}

public enum ExitPolicy
{
    Keep,       // 游戏退出后保留
    Kill,       // 游戏退出后终止（默认，避免污染下次启动）
}
```

### 1.2 用户意图对照表

| 用户想做的事 | 配置 |
|---|---|
| 后启动一个工具（手动） | `Trigger.Manual` |
| 补丁随游戏自动生效 | `Trigger.Auto` |
| 临时禁用补丁但保留配置 | `Enabled = false` |
| 工具在游戏退出后继续用 | `OnGameExit.Keep` |

### 1.3 环境变量三态合并

```csharp
public sealed class EnvEntry
{
    public required string Key { get; set; }
    public EnvPatchKind Kind { get; set; } = EnvPatchKind.Inherit;
    public string? Value { get; set; }              // Kind == Override 时生效，允许 ""
}

public enum EnvPatchKind
{
    Inherit,    // 不写，继承游戏启动时的值
    Override,   // 设成 Value（空字符串也是合法值）
    Remove,     // 从继承结果里删掉这个键
}
```

**合并算法**：

```csharp
var env = new Dictionary<string, string>(plan.Env, StringComparer.Ordinal);

foreach (var entry in program.Env)
{
    switch (entry.Kind)
    {
        case EnvPatchKind.Inherit:
            break;
        case EnvPatchKind.Override:
            env[entry.Key] = entry.Value ?? "";
            break;
        case EnvPatchKind.Remove:
            env.Remove(entry.Key);
            break;
    }
}
```

### 1.4 为什么 `Override("")` ≠ `Remove`

对 wine 而言"变量存在但为空"与"变量不存在"是**不同的行为**。

真实案例（来自 `anime-launcher-sdk`）：
```rust
// winewayland 模式需要 DISPLAY 存在但为空
if config.game.wine.winewayland {
    command.env("DISPLAY", "");
}
```
这里如果是 `Remove`，wine 会退回 X11；只有 `Override("")` 才是"不用 X11"。

**UI 设计**：
- 三态控件：`继承 / 覆盖 / 删除`
- 输入框清空 → 默认 `Remove`（符合直觉）
- 另给"设为空值"勾选框（小字），勾上则该行为 `Override("")`

### 1.5 补丁模式文件（只提供机制，不预置）

某些注入器不用环境变量传参，而读取自身目录下的模式文件
（如 `zzz-touch-unlocker` 的 `zzz-touch-mode.txt`，内容 `0` 或 `1`）。

**启动器不做特例**。若用户需要，可以在外部程序的 `Args` 里用关键字，
或由引擎在 `Prepare` 相位写一个文件——为此提供一个**通用**能力：

```csharp
public sealed class FileWriteSpec
{
    public required string Path { get; set; }       // 支持 %keywords%
    public required string Content { get; set; }    // 支持 %keywords%
    public FileWriteWhen When { get; set; } = FileWriteWhen.Prepare;
}

public enum FileWriteWhen { Prepare, AfterGameReady }
```

挂在 `ExternalProgram` 上：

```csharp
public List<FileWriteSpec> PreWrites { get; set; } = [];
```

**约束**：默认禁止写到游戏目录（警告），因为参考实现刻意不碰游戏文件。
路径含 `%game%` 时给出明示警告。

### 1.6 安全提示（非阻止）

启动前检查，发现问题时在 UI 显示**警告**（可继续）：

| 条件 | 提示 |
|---|---|
| `Trigger.Auto` + `WindowsExe` | "该程序会在游戏就绪后被拉起，成为该 wine prefix 的**第二个**客户端。若它需要破坏游戏的保护期初始化，可能导致游戏闪退。" |
| `Trigger.Auto` + `Wait=true` + 短超时 | "该程序前台等待且无超时，游戏退出前不会返回。" |
| 路径不存在 | "文件不存在"（标红） |
| 路径含 `%game%` 且有 `PreWrites` | "正在向游戏目录写入文件，可能被视为修改游戏文件。" |

**这些都不阻止启动**——用户手动挑选的文件，我们尊重其选择。

### 1.7 启发式建议（仅 UI，不落盘）

用户选完 `.exe` 后：

| 文件名匹配 | 建议 |
|---|---|
| `*inject*` / `*touch*` / `*hook*` | "看起来是注入类程序，建议设为『伴随启动』" |
| `*map*` / `*wiki*` / `*tool*` | "看起来是辅助工具，建议设为『手动启动』" |

只是建议，用户可忽略。

### 1.8 UI

面板结构：

```
外部程序
├── [+ 从文件添加]         ← 文件选择器（按 ProgramKind 过滤 .exe / 可执行）
├── ─────────────────────
├── ▸ (名称)               ← 可折叠
│   ├── [x] 启用
│   ├── 触发：[伴随启动 ▾]
│   ├── 文件：[路径] [浏览…]
│   ├── 参数：[........] [+]
│   ├── 工作目录：[继承游戏目录 ▾] [浏览…]
│   ├── 环境变量：
│   │   ├── KEY1   [继承 ▾] [值...]      [-]
│   │   ├── KEY2   [覆盖 ▾] [值...] [设为空值] [-]
│   │   └── KEY3   [删除 ▾]              [-]
│   ├── 写入文件：[+]（高级）
│   ├── [ ] 前台等待
│   ├── 退出策略：[终止 ▾]
│   └── [立即启动]  [删除]
```

`[立即启动]` 仅在游戏 `Running` 相位可见（否则禁用 + 提示"需先启动游戏"）。

---

## 二、区服切换

### 2.1 机制

渠道由**游戏目录下的 `config.ini`** 决定（见 `01-RESEARCH.md` §3）。

| 渠道 | channel | cps | sub_channel |
|---|---|---|---|
| 官方国服 | 1 | `mihoyo` | 0 |
| **Bilibili** | **14** | **`bilibili`** | **0** |
| Epic | 1 | `pcseaepic` | 3 |
| Google Play | 1 | `pcgoogle` | 6 |

本项目只暴露 **官方国服 / Bilibili** 两个选项（另两个是历史遗留，UI 不显示，
但保留序列化能力以免抹掉别人写过的值）。

### 2.2 实现：用 `Microsoft.Extensions.Configuration.Ini`

**不硬拼字符串，不手写解析。**

```csharp
using Microsoft.Extensions.Configuration;

public sealed class GameConfigIni
{
    [ConfigurationKeyName("channel")]          public int Channel { get; set; }
    [ConfigurationKeyName("sub_channel")]      public int SubChannel { get; set; }
    [ConfigurationKeyName("cps")]              public string Cps { get; set; } = "";
    [ConfigurationKeyName("game_version")]     public Version? GameVersion { get; set; }
    [ConfigurationKeyName("sdk_version")]      public Version? SdkVersion { get; set; }
    [ConfigurationKeyName("game_biz")]         public GameBiz GameBiz { get; set; }
    [ConfigurationKeyName("uapc")]             public string Uapc { get; set; } = "";
    [ConfigurationKeyName("downloading_mode")] public string DownloadingMode { get; set; } = "";
}
```

这个类**原样搬自** `Starward/src/Starward/Features/GameLauncher/GameConfigIni.cs`。

### 2.3 必须保留未知键

游戏版本更新可能新增字段。读取时：

```csharp
var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

new ConfigurationBuilder()
    .AddIniFile(path, optional: true)
    .Build()
    .GetSection("General")
    .AsEnumerable()
    .Where(kv => kv.Value is not null)
    .ToList()
    .ForEach(kv => dict[StripPrefix(kv.Key)] = kv.Value);
```

写回时先按已知字段写，再补上 `dict` 里未消费的键。**这样不会丢字段**。

### 2.4 原子写

```
1. 序列化到 config.ini.tmp
2. FileStream.Flush(flushToDisk: true)
3. File.Move(tmp, target, overwrite: true)
```

### 2.5 B 站三条件（缺一静默回落）

**这是最危险的失败模式**：用户以为切到了 B 站，实际进了官服，无任何提示。

启动前必须逐条校验：

| # | 条件 | 失败时 |
|---|---|---|
| 1 | `edition == China`（`YuanShen.exe`） | **硬拒绝**，说明"B 站渠道仅国服可用" |
| 2 | `YuanShen_Data/Plugins/PCGameSDK.dll` 存在且 `Length > 0` | **硬拒绝** + 给"下载插件"按钮 |
| 3 | `config.ini` 已正确写入 | 重写 |

### 2.6 PCGameSDK.dll 下载

```
URI:  https://pkg.biligame.com/games/PCGameSDK5.1.0/051985/PCGameSDK5.1.0.zip
UA:   Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36
      (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36
Ref:  https://open.biligame.com/
```

**两个陷阱**（`01-RESEARCH.md` §3.3）：

1. **CDN 对无 UA/Referer 的请求返回 403**，且**HEAD 请求也必须带**。
   如果用 `HttpClient` 先探测大小，必须给那个请求也加头。
2. **zip 条目名是 GBK 编码**：`64位/PCGameSDK.dll` 存成 `64\xce\xbb/PCGameSDK.dll`。
   匹配规则：
   ```csharp
   name.EndsWith("/PCGameSDK.dll", StringComparison.OrdinalIgnoreCase)
       && name.Split('/').Any(p => p.StartsWith("64", StringComparison.Ordinal))
   ```
   同时 zip 可能带 Unicode 扩展字段，两种表示都以前缀 `64` 开头，所以规则通用。

**下载与安装**：
- 缓存到 `$XDG_CACHE_HOME/lunarward/bilibili/PCGameSDK5.1.0.zip`
- 下载到 `.part` 再 rename（缓存永远完整）
- 解压到 `PCGameSDK.dll.part` 再 rename（避免半写）

### 2.7 ZZZ 无 B 站渠道

UI 必须按 `game` 隐藏该选项。ZZZ 的 `env_emulation` 只有 PC/Epic/Android。

### 2.8 渠道是 profile 级状态

- 存在 `profile.channel`
- 切换后**下次启动生效**
- 同一安装目录共用一份游戏时，切渠道即改写 `config.ini`，**无需重装**
- 若两个渠道各自独立安装目录，按 `edition` 选路径，互不影响

---

## 三、运行时分发

### 3.1 四级作用域

```csharp
public abstract record Scoped<T>
{
    /// 本 profile 独占
    public sealed record PerGame(T Value) : Scoped<T>;

    /// 引用具名共用运行时
    public sealed record Shared(string Name) : Scoped<T>;

    /// 跟随启动器全局默认
    public sealed record Default : Scoped<T>;

    /// 行内自定义，不落具名表
    public sealed record Custom(T Value) : Scoped<T>;
}
```

解析顺序：`PerGame | Custom` > `Shared(name)` > `Default` > 系统兜底。

**结果必须可解释**：UI 显示

```
生效：shared-cachyos › spritz-wine-cachyos-wow64-10.0-11
      （profile 未覆盖，跟随默认运行时）
```

### 3.2 Runner 来源

```csharp
public abstract record RunnerSource
{
    /// 托管组件（可下载）
    public sealed record Managed(string Component) : RunnerSource;

    /// PATH 里的 wine
    public sealed record System : RunnerSource;

    /// Steam 的 Proton
    public sealed record Proton(string SteamLibraryPath, string ProtonName) : RunnerSource;

    /// 用户指定路径
    public sealed record Custom(string Path, WineLoader Loader) : RunnerSource;
}
```

| 来源 | 解析为 | 注意 |
|---|---|---|
| `Managed` | `<data>/runners/<component>/bin/wine` | 组件索引无此名时报 `RuntimeNotFound` |
| `System` | `which wine` | 必须 `wine --version` 探测可用性 |
| `Proton` | `<lib>/steamapps/common/<name>/dist/bin/wine` | 见 §3.4 |
| `Custom` | 用户路径 | 校验 `bin/wine` 或文件本身可执行 |

### 3.3 托管组件

复用 an-anime-team 的组件索引格式（`01-RESEARCH.md` §6.3）：

```
<data>/components/
├── components.json
├── wine/<group>.json
└── dxvk/<group>.json
```

从 `https://github.com/an-anime-team/components` 用 `git` 同步
（`clone` / `fetch` + `reset --hard`）。

**注意**：这是唯一还依赖 an-anime-team 生态的地方。若该仓库将来停止更新，
用户可以手动往 `<data>/components/` 放索引文件，或改用 `System`/`Custom` 来源。

### 3.4 Proton 两种模式

| 模式 | 做法 | 状态 |
|---|---|---|
| **`WineBinary`（默认）** | 直接用 `<proton>/dist/bin/wine`，自建 prefix | ✅ 保住不变式 |
| `ProtonScript` | `proton run <exe>` + `STEAM_COMPAT_DATA_PATH` 等环境 | ⚠️ 实验性，**未验证** |

**为什么默认不是脚本**：`proton run` 会多一层 python → wine 的进程链，
**可能破坏"游戏是首个 wine 客户端"这条不变式**（`03-LAUNCH-ENGINE.md` §1）。

`ProtonScript` 在 UI 中标注"实验性，可能导致游戏无法启动"，并在启用时
额外校验（启动后若 `AwaitImage` 超时，提示"尝试改用 WineBinary 模式"）。

### 3.5 Steam 库发现

```
~/.steam/steam/steamapps/libraryfolders.vdf
~/.local/share/Steam/steamapps/libraryfolders.vdf
```

解析每个库路径，在 `<lib>/steamapps/common/` 下找含 `dist/bin/wine` 的目录
（`Proton - Experimental`、`GE-Proton*` 等）。

**不手写 VDF 解析器** → 见 `06-OPEN-QUESTIONS.md` Q2。

### 3.6 Prefix 作用域

```csharp
public abstract record PrefixScope
{
    public sealed record Own(string Path) : PrefixScope;
    public sealed record Shared(string Name) : PrefixScope;

    /// 复用 Steam 的兼容数据目录
    public sealed record ProtonCompatData(string Path) : PrefixScope;
}
```

`ProtonCompatData` 强制 `prefix = <path>/pfx`，并对该 profile 禁用重装操作，
避免污染 Steam 的记录。

### 3.7 DXVK 来源

```csharp
public abstract record DxvkSource
{
    public sealed record Managed(string Component) : DxvkSource;
    public sealed record System : DxvkSource;      // 用 wine 自带
    public sealed record Builtin : DxvkSource;     // 不装任何东西
    public sealed record Custom(string Path) : DxvkSource;
}
```

**安装方式**（沿用 wincompatlib 的做法，`01-RESEARCH.md` 无此节，见下）：

1. 校验 prefix 有效（`system.reg` 存在）
2. `wineboot -u` 刷新 prefix
3. 拷贝 `x64/<name>.dll` 到 `drive_c/windows/system32/`，原文件留 `.dll.old`
4. 写注册表 override：`HKCU\Software\Wine\DllOverrides\<name> = native`
5. 涉及的 DLL：`dxgi`、`d3d9`、`d3d10core`、`d3d11`

**状态检测**：读 `<prefix>/user.reg` 文本，找 `"dxgi"` 行且含 `"native"`。
（这是 an-anime-team 的做法，简单可靠，本项目沿用）

> ⚠️ an-anime-team **从不使用 `WINEDLLOVERRIDES`**，全靠注册表。
> 本项目在需求 5 中补上 `WINEDLLOVERRIDES` 支持，但**默认仍用注册表**，
> 以保持一致；UI 说明二者关系（环境变量优先级更高，会覆盖注册表）。

---

## 四、参数与环境变量

### 4.1 分层

见 `03-LAUNCH-ENGINE.md` §6（十层优先级）。

### 4.2 相对 an-anime-team 的扩展

| 项 | an-anime-team | LunarWard |
|---|---|---|
| `WINEDLLOVERRIDES` | **0 处使用** | ✅ 支持 |
| `VKD3D_CONFIG` | 无 | ✅ |
| `VKD3D_FEATURE_LEVEL` | 无 | ✅ |
| `DXVK_CONFIG_FILE` | 无 | ✅ |
| `DXVK_HUD` 细节 | 固定串 | ✅ 全量可配 |
| `PROTON_*` | 无 | ✅ |
| `MANGOHUD_CONFIG` | 无 | ✅ |
| 开机自启 | 注册表 `Run` 键 | ✅ XDG autostart（`autostart/lunarward.desktop`，`--start-hidden` 进托盘） |
| 包装器 | `gamemode` 布尔 + `gamescope` 结构 | ✅ 有序包装器链 + 游戏设置页「Linux 性能工具」开关（GameMode / gamescope / MangoHud，顺序固定；gamescope 参数单独可编辑，`--` 自动补） |

### 4.3 UI

三个层级，避免一次展示十层：

1. **常用**：分辨率/全屏（`borderless`）、HUD、FSR、同步模式
2. **高级**：环境变量表格（键/值/来源列）、包装器链、自定义命令模板
3. **诊断**：`launch-report.json` viewer，展示完整十层与每键来源

---

## 五、Starward 风格 UI

### 5.1 布局

```
┌─────────────────────────────────────────────────────┐
│ [游戏图标] 原神 ▾        [公告●]  [设置]  [关于]     │  ← 顶部栏
├─────────────────────────────────────────────────────┤
│                                                      │
│   ┌────────────────────────────────────────────┐    │
│   │                                            │    │
│   │         背景（视频/图片，循环）              │    │
│   │                                            │    │
│   │   ┌──────────────────────────┐             │    │
│   │   │  版本亮点轮播             │             │    │
│   │   └──────────────────────────┘             │    │
│   └────────────────────────────────────────────┘    │
│                                                      │
│        ┌──────────────────────┐                     │
│        │      启 动            │  ← 状态驱动         │
│        └──────────────────────┘                     │
│        [外部程序▾] [环境shell] [诊断]                │
│                                                      │
└─────────────────────────────────────────────────────┘
```

### 5.2 启动按钮状态机

按钮文案直接映射 `Phase`：

| Phase | 按钮 | 可点击 |
|---|---|---|
| 空闲 | 启动 | ✅ |
| Prepare | 准备中… | ❌ |
| Launch | 启动中… | ❌ |
| AwaitImage | 等待游戏… | ❌ |
| AwaitReady | 等待就绪… | ❌ |
| PostLaunch | 启动辅助… | ❌ |
| Running | 停止游戏 | ✅ |
| Cleanup | 收尾中… | ❌ |
| 错误 | 重试 | ✅ |

### 5.3 双主题

两者都用 Avalonia 的 `Styles` / `ControlTheme` 实现，不是 CSS。

| 主题 | 特征 | 实现 |
|---|---|---|
| `Modern` | Fluent 卡片流，内容居中，圆角卡片 | 默认；`Avalonia.Themes.Fluent` + 自定义卡片样式 |
| `Classic` | 全屏背景 + 悬浮控件，无卡片边界 | 覆盖 `Window`/`Button` 的 `ControlTheme`，隐藏卡片边框 |

**视觉基调对齐 Starward**：`CornerRadius`（Starward 用 371 次）、
`AcrylicBrush`（92 次）、`ThemeShadow`（62 次）。Avalonia 的对策见 §5.6。

### 5.4 动态背景（视频/图片轮播）

**用 `LibVLCSharp.Avalonia` 的 `VideoView`**，不依赖系统 GStreamer 插件。

```csharp
var videoView = new VideoView
{
    HorizontalAlignment = HorizontalAlignment.Stretch,
    VerticalAlignment   = VerticalAlignment.Stretch,
    MediaPlayer = _mediaPlayer,
};

_mediaPlayer = new MediaPlayer(_libVlc)
{
    Media = new Media(_libVlc, new Uri(videoPath)),
    Mute  = true,          // 静音
    EnableHardwareDecoding = true,
};
_mediaPlayer.EndReached += (_, _) =>
    Dispatcher.UIThread.Post(() => _mediaPlayer.Play());   // 循环
```

> ⚠️ **VLC 的 `EndReached` 回调在线程池线程上触发，必须 `Post` 回 UI 线程再重播**，
> 否则会死锁。不能依赖 `MediaPlayer` 自带的 loop 标志。

**播放控制**：
- 游戏 `Running` 相位时 `Pause()`（省 GPU / 省解码器）
- 窗口失焦时降帧或暂停
- 始终静音（`Mute = true`）

**静态图回退**：`video_background = false`、或 VLC 初始化失败时，
用 `Image` + `Stretch="UniformToFill"` 显示背景图，逻辑完全相同。

**运行时依赖**：
- `libvlc`（Arch: `vlc`；Debian: `libvlc5`；Fedora: `vlc-libs`）
- 探测失败 → 回退静态图 + UI 提示安装命令，**不崩溃**

**缓存**：`$XDG_CACHE_HOME/lunarward/background/`，md5 比对，未变不重下。
`video_background = false` 时**跳过视频下载**（省带宽）。

### 5.5 公告三源

| 源 | API | 用途 |
|---|---|---|
| **官方红点** | `getAlertAnn` | 顶栏 ● 提示 |
| **版本亮点** | `getAllGameBasicInfo` | 初始页轮播 |
| **应用内公告** | LunarWard 自己的 JSON | 版本更新说明、迁移提示 |

统一为：

```csharp
public sealed record Announcement(
    AnnouncementSource Source,
    string Title,
    string Body,            // Markdown
    string? ImageUrl,
    DateTimeOffset? PublishedAt,
    Uri? ExternalLink,
    bool IsRead
);

public interface IAnnouncementService
{
    Task<IReadOnlyList<Announcement>> FetchAsync(string gameBiz, CancellationToken ct);
    Task MarkReadAsync(string announcementId);
}
```

红点状态持久化到 `$XDG_DATA_HOME/lunarward/announcements.json`。

### 5.6 Acrylic / 窗口模糊在 Linux 上的落地

**问题**：Starward 大量使用 `AcrylicBrush`（92 次）与 `ThemeShadow`（62 次），
这是 Fluent 观感的核心。但 **Linux 的 X11/Wayland 各组合器对 backdrop blur
的支持不一致**，Avalonia 没有跨平台的"让窗口背景透明并被合成器模糊"API。

**分级策略**（按可用性降级，全部不崩溃）：

| 级别 | 条件 | 做法 |
|---|---|---|
| **L1** 真模糊 | KDE Plasma（KWin）支持 `_KDE_NET_WM_BLUR_BEHIND_REGION` | 设窗口透明 + 设 blur 区域属性 → 真正的背景模糊（**远期，首期不做**） |
| **L2** 半透明 | 任意支持复合的合成器 | `Window.TransparencyLevelHint = Transparent` + 半透明纯色/渐变层（**本期目标**） |
| **L3** 纯色 | 无复合（X11 无 composite、软件渲染） | 不透明深色背景 |

**探测**：
```csharp
var hints = TopLevel.GetTopLevel(window)?.TransparencyLevelHint;
// Transparent / Blur / AcrylicBlur / None
```

**关键决策**：**只实现 L2（半透明）+ L3（纯色兜底）**，不做 L1 的 KDE 特化。

**理由**：
- Wayland 下没有标准的 blur 协议；`ext-background-effect-v1` 仍在讨论中
- 依赖 KDE 私有属性会把应用绑定到单一桌面环境
- **半透明渐变在视觉上已能传达 Fluent 质感**，且处处可用
- 一套代码路径、行为可预期，比"每个桌面环境一套分支"更易维护

L1 作为**远期优化**保留，不在首期实现。

**需要确认**：这个取舍可以吗？→ `06-OPEN-QUESTIONS.md` Q11。

**`ThemeShadow`** → `DropShadowEffect`（Avalonia 内建），跨平台一致，无风险。

---

## 六、环境 shell

### 6.1 目标

打开一个 **env / PATH / cwd 已按游戏启动配置准备好** 的终端，用于调试。

### 6.2 关键设计：复用 `LaunchPlan`

**绝不允许另写一套环境构建逻辑。** 必须：

```csharp
var plan = launchPlanFactory.Build(profile);   // 与真实启动同一份
var shell = new ShellSession
{
    Program = environment.GetEnvironmentVariable("SHELL") ?? "/bin/bash",
    Env = plan.Env,                            // ← 直接复用
    Cwd = plan.GameDirectory,
};
```

否则"调试环境和实际启动环境不一致"，shell 就失去意义。

### 6.3 能力

| 能力 | 说明 |
|---|---|
| 一键打开 | 使用 `LaunchPlan.Env` |
| 前缀就绪 | `WINEPREFIX` / `WINELOADER` / `LD_LIBRARY_PATH` / `GST_PLUGIN_PATH` 全在 |
| 可直接跑 wine | `wine regedit`、`winecfg`、`wineserver -k` |
| 命令预设 | 一排按钮：`winecfg` / `regedit` / `wineserver -k` / 列 prefix / `wine <exe>` |
| 导出脚本 | 生成 `env.sh`（`export K=V` 全量），可复制到外部终端 |
| 对比报告 | 与 `launch-report.json` 并排显示 |
| 日志跟随 | 同一窗口另一个标签 `tail -f game.log` |

### 6.4 实现方案：选用现成的 Avalonia 终端控件

切换 UI 框架后，这一项**从"全项目最大风险"变成"选包"**。

候选（均需 M0 核验维护状态与 Linux 兼容性）：

| 包 | 版本 | 说明 |
|---|---|---|
| `Iciclecreek.Avalonia.Terminal` | 4.1.0 | 基于 xterm.js 语义的 Avalonia 移植 |
| `RoyalApps.RoyalTerminal.Avalonia` | 0.6.3 | 有配套 `.Pty.Windows`，说明有跨平台 PTY 抽象 |
| `AvaloniaTerminal` | 1.0.0 | 最轻量 |

**选择标准**（按优先级）：
1. **PTY 走原生 `openpty`**（Linux 必须），不是模拟
2. 支持 `TERM` / `COLORTERM` 与 256 色
3. 支持 `SIGWINCH`（窗口 resize 时子进程能感知）
4. 维护活跃（近 6 个月有提交）

**若全部不达标**的退路（按优先级）：
1. 自建 PTY（`openpty` P/Invoke）+ 现成 VT 解析器（不自己写转义序列）
2. 只读日志查看器 + "导出 `env.sh` 到外部终端"按钮

> 与 GirCore 路线相比少写约 300 行 GObject 生命周期绑定，且不必担心
> `libvte` 的 ABI 变化。

**无论选哪个控件，`LaunchPlan.Env` 的复用要求不变**（见 §6.2）。

### 6.5 生命周期

- 关闭标签 → `SIGHUP` 整个进程组
- 启动器退出 → 回收所有仍在运行的 shell
- 每个 shell 记录 pid，`Cleanup` 相位检查

---

## 七、其他 Starward 功能

见 `05-ROADMAP.md` 的迁移矩阵。要点：

| 功能 | 策略 |
|---|---|
| 抽卡 + UIGF | `LunarWard.Core` 直接提供客户端，只重写视图 |
| 便笺 DailyNote | 同上 |
| 公告 / 版本亮点 | 同上 |
| 安装/更新/修复 | `LunarWard.Installer`（移植 `Starward.RPC`） |
| 账号切换 | 重写（Wine 下切 `user.reg` 的 `[Software\miHoYo\...]` 段） |
| 游戏时长 | ✅ 已实现：`LunarWard.Launch/PlayTimeService.cs`，复用相位引擎的起止时间，存 Starward 同款 SQLite 表 `PlayTimeStats`（`<DataHome>/LunarWardDatabase.db`，`Database/PlayTimeStore.cs`） |
| 截图 | 重写路径逻辑（监视截图目录） |
| 画质设置 | **P1 只读**（registry 里是二进制 blob，Wine 下在 `user.reg` 的 hex 中） |
| URL Protocol | `.desktop` 的 `MimeType=x-scheme-handler/lunarward;` |
| Discord RPC | IPC socket |
| Overlay / 手柄 | **不做** |
