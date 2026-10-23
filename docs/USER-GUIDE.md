# 用户手册

LunarWard —— Linux 下的《原神》/《绝区零》启动器。

本手册只讲**怎么用**。设计理由见 `docs/00-DECISIONS.md`，实现细节见 `docs/02-ARCHITECTURE.md`
与 `docs/03-LAUNCH-ENGINE.md`。出问题先看 [TROUBLESHOOTING.md](TROUBLESHOOTING.md)。

---

## 一、安装与依赖

### 1.1 硬依赖

| 依赖 | 说明 | 检查命令 |
|---|---|---|
| **.NET 10 运行时** | **必须**。发行包不是 self-contained | `dotnet --list-runtimes`（找 `Microsoft.NETCore.App 10.x`） |
| Linux x64 + 支持复合的桌面 | Wayland / X11 均可 | `echo $XDG_SESSION_TYPE` |

安装 .NET 10 运行时：

```bash
# Arch / CachyOS
sudo pacman -S dotnet-runtime

# Debian / Ubuntu（用官方源或 Microsoft 源，发行版自带的往往不是 10）
sudo apt install dotnet-runtime-10.0
```

### 1.2 可选依赖

**缺任何一个都不会让启动器崩溃**，只会让对应功能降级。

| 依赖 | 用途 | 缺失时 | Arch | Debian |
|---|---|---|---|---|
| `libvlc` | 动态背景视频解码 | 回退静态图 | `pacman -S vlc` | `apt install libvlc5` |
| `hdiffpatch` | 差量补丁更新 | 记录 `Patch file failed` 警告，该文件走全量下载 | `pacman -S hdiffpatch` | `apt install hdiffpatch` |
| `7zip` | 解包 `.7z` 安装包 | 抛 `InstallerCapabilityException`（纯 `.zip` 仍可解） | `pacman -S 7zip` | `apt install 7zip` |

> **Arch 上的坑**：`libvlc` 与 VLC 的**插件包是分开的**。只装 `libvlc` 时
> `libvlc_new()` 会返回 `NULL`（缺 codec 插件），视频背景仍然不可用。
> 要真能播视频，装**完整的 `vlc`**。装不上时启动器会静默回退静态图，见 §7。

---

## 二、首次配置

### 2.1 设置游戏目录

| 方式 | 操作 |
|---|---|
| **自动扫描** | 设置 → 「游戏目录」→ 点 **扫描** |
| **手填** | 设置 → 「游戏目录」→ 输入框填绝对路径 |

扫描范围（**只读**，不修改任何外部文件）：

```
~/games/**            ~/Games/**           ~/Genshin/**        ~/ZenlessZoneZero/**
~/.local/share/sleepy-launcher/**            （只读读取其 config.json 里的路径）
~/.local/share/anime-game-launcher/**
~/.local/share/lunarward/games/**
```

深度上限 3 层，跳过 `prefix` / `runners` / `dxvks` / `components` / `steamapps` /
`compatdata` / `node_modules` / `.git`。

### 2.2 三种版本的可执行文件名

启动器靠**目录里存在哪个 exe** 来识别版本，认错时手填目录即可。

| 游戏 | 版本 | 可执行文件 | 数据目录 | GameBiz |
|---|---|---|---|---|
| 原神 | 国服 | `YuanShen.exe` | `YuanShen_Data` | `hk4e_cn` |
| 原神 | 国际服 | `GenshinImpact.exe` | `GenshinImpact_Data` | `hk4e_global` |
| 绝区零 | 国服 | `ZenlessZoneZero.exe` | `ZenlessZoneZero_Data` | `nap_cn` |
| 绝区零 | 国际服 | `ZenlessZoneZero.exe` | `ZenlessZoneZero_Data` | `nap_global` |

> 绝区零两个版本**可执行文件同名**，只靠 exe 名分不出来。识别顺序：
> ① exe 名 → ② 目录里 `config.ini` 的 `game_biz` → ③ 数据目录名。
> 若两个版本都装在同一个目录下（不推荐），请分开放。

### 2.3 识别顺序与手工覆盖

识别优先级：**exe 名** > **`config.ini` 的 `game_biz`** > **数据目录名**。
三个都不中 → 扫描不列出该目录，需手动指定。

---

## 三、运行时选择

设置 → 「wine 运行时」→ 点 **重新扫描** 拉出候选列表，选中即生效。
生效结果在「诊断」里能看到完整解析链（`docs/02-ARCHITECTURE.md` §八）。

### 3.1 五种来源

| 来源 | UI 显示 | 解析为 | 适用 |
|---|---|---|---|
| **System** | `System · wine-x.y` | `which wine` + `wine --version` 探测 | 系统已有 wine |
| **托管组件** | 目录名 | `~/.local/share/lunarward/runners/<组件>/bin/wine` | 本项目管理的 runner |
| **Proton** | `Proton · <名字>` | `<库>/steamapps/common/<名字>/dist/bin/wine` | 复用 Steam Proton |
| **自定义** | 用户指定 | 用户填的路径；给目录会补 `bin/wine` | 任意来源 |
| **只读借用** | `借用 · <名字>` | `~/.local/share/sleepy-launcher/runners/<名字>/bin/wine` | 已装别的启动器 |

**「只读借用」的语义**：把其它启动器**已经下载好**的 runner 注册成 `Custom` 条目使用。
LunarWard **不会**写入、升级或删除那个目录，也不会读它的配置。
对方删了目录，这里的条目随即失效（下次启动报 `RuntimeNotFound`）。

### 3.2 Proton：默认走 `dist/bin/wine`，不走 `proton run`

| 模式 | 做法 | 状态 |
|---|---|---|
| **`WineBinary`（默认）** | 直接执行 `<proton>/dist/bin/wine`，自建 prefix | ✅ 推荐 |
| `ProtonScript` | `proton run <exe>` + `STEAM_COMPAT_DATA_PATH` | ⚠️ 实验性，未验证 |

**为什么默认不是 `proton run`**（`docs/00-DECISIONS.md` D9）：`proton run` 会插一层
python → wine 的进程链，**可能破坏"游戏必须是该 prefix 第一个 wine 客户端"这条不变式**，
后果是无窗口、无日志、约 5 秒静默消失。`WineBinary` 直连 `dist/bin/wine`，进程链最短。

### 3.3 四级作用域

解析顺序：`本 profile 独立` > `共用运行时（具名）` > `启动器默认` > `系统兜底`。
UI 会显示"这个运行时是怎么来的"，例如：

```
生效：shared-cachyos › spritz-wine-cachyos-wow64-10.0-11
      （profile 未覆盖，跟随默认运行时）
```

### 3.4 DXVK

| 来源 | 含义 |
|---|---|
| `Managed` | `~/.local/share/lunarward/dxvks/<组件>/` |
| `System` | 用 wine 自带的 |
| `Builtin` | 什么都不装 |
| `Custom` | 用户路径 |

安装方式：备份原 DLL 为 `.dll.old` → 拷贝 `x64/*.dll` 到
`drive_c/windows/system32/` → 写 `HKCU\Software\Wine\DllOverrides\<name> = native`。
**默认用注册表**而不是 `WINEDLLOVERRIDES`（与 an-anime-team 一致）；
两者都设时**环境变量优先级更高**，会覆盖注册表。

---

## 四、区服切换（仅原神国服）

### 4.1 两个可选渠道

| 渠道 | `channel` | `cps` | `sub_channel` |
|---|---|---|---|
| **官方国服** | 1 | `mihoyo` | 0 |
| **Bilibili** | **14** | **`bilibili`** | **0** |

> Epic（`pcseaepic`）与 Google Play（`pcgoogle`）是历史遗留值：
> UI **不显示**，但读改写时会**原样保留**，不会抹掉别人写过的值。

**绝区零没有 B 站渠道**（其 `env_emulation` 只有 PC/Epic/Android），UI 按游戏隐藏该选项。

### 4.2 B 站三条件

启动前逐条校验，**任一不满足就硬拒绝**（`LaunchError.ChannelPrerequisiteMissing`，退出码 1）：

| # | 条件 | 失败时 |
|---|---|---|
| 1 | `edition == China`（即 `YuanShen.exe`） | **硬拒绝**："B 站渠道仅国服可用" |
| 2 | `YuanShen_Data/Plugins/PCGameSDK.dll` 存在且 `Length > 0` | **硬拒绝** + 给「下载插件」按钮 |
| 3 | `config.ini` 已写成 B 站值 | 不算失败，`Prepare` 相位重写 |

### 4.3 为什么缺 DLL 要硬拒绝，而不是静默回落官服

**这是本项目最危险的失败模式**：用户以为切到了 B 站，实际进的官服，全程无任何提示。

所以设计上选**硬拒绝**：宁可启动不了，也不让用户登错服、绑错区、
在错误的服务器上充值。看到下面的错误就是这条在起作用：

```
渠道前置条件不满足：Bilibili 渠道需要 YuanShen_Data/Plugins/PCGameSDK.dll，当前缺失。
```

### 4.4 插件下载

点「下载插件」。实现细节（CDN 的 UA/Referer 要求、zip 条目名的 GBK 编码）
见 `docs/04-FEATURES.md` §2.6，用户无需关心。缓存位置：

```
~/.cache/lunarward/bilibili/PCGameSDK5.1.0.zip
```

下载到 `.part` 再 rename，解压到 `PCGameSDK.dll.part` 再 rename——**缓存永远完整**，
中途断网不会留下半截文件。

### 4.5 切换后何时生效

**下次启动生效**。同一份安装目录共用游戏文件时，切渠道只改 `config.ini`，
**不需要重装**。若两个渠道各装了独立目录，按版本（`edition`）选路径，互不影响。

---

## 五、外部程序（后启动 / 补丁）

设置 → 「外部程序」→ **+ 从文件添加**。

### 5.1 只有一种东西，靠 Trigger 区分

后启动与补丁注入**是同一个概念**，不做类型区分（`docs/00-DECISIONS.md` D5）。

| 用户意图 | 配置 | 行为 |
|---|---|---|
| 补丁随游戏自动生效 | `Trigger.Auto` | 游戏 **Ready 之后**自动拉起 |
| 后启动一个工具 | `Trigger.Manual` | 只在 UI 点「立即启动」才拉起 |
| 临时禁用补丁但保留配置 | `Enabled = false` | 既不自动也不出现在可启动列表 |
| 工具在游戏退出后还要用 | `OnGameExit.Keep` | 不终止 |

### 5.2 硬约束：`Auto` 只在 `AwaitReady` 之后

| 相位 | 发生了什么 |
|---|---|
| `Prepare` → `AwaitReady` | **不拉起任何外部程序** |
| `PostLaunch`（`AwaitReady` 成功后） | 才拉起 `Trigger.Auto && Enabled` 的程序 |

**不提供"游戏启动前运行程序"的能力**——那正是破坏"首个 wine 客户端"不变式的行为，
会导致游戏静默消失（`docs/03-LAUNCH-ENGINE.md` §1）。`Trigger` 只有 `Auto` / `Manual` 两个值。

### 5.3 ⚠️ Auto + WindowsExe 的后果

> **`Trigger.Auto` + `Kind=WindowsExe` 会让该程序成为这个 prefix 的第二个 wine 客户端。**

UI 会显示警告，但**不阻止**（你选的文件你做主）：

```
该程序会在游戏就绪后被拉起，成为该 wine prefix 的第二个客户端。
若它需要破坏游戏的保护期初始化，可能导致游戏闪退。
```

判断依据：只要反作弊的保护期初始化还没结束，第二个客户端就可能触发
`NtRaiseHardError 0x50000018`。这就是为什么引擎把 `PostLaunch` 卡在
`AwaitReady` 之后，也是为什么 `Auto` 的可靠性取决于 §8 的 `grace` 设得对不对。

`Trigger.Manual` + `WindowsExe` **没有**这条警告（你手动点的时候游戏早就 Ready 了）。

### 5.4 环境变量三态

外部程序的 env 是**在游戏 env 之上**的三态补丁（第十层，见 §6 的合并顺序）。

| 态 | 含义 | 结果 |
|---|---|---|
| **继承** | 不写这一层 | 保留游戏启动时的值 |
| **覆盖** | 设成指定值（**空字符串合法**） | 键存在，值是 `""` |
| **删除** | 从继承结果里删掉 | 键**不存在** |

**输入框清空 → 默认按「删除」处理**（更符合直觉）。
要表达"存在但为空"必须显式操作：

| 操作 | 结果 | 典型用途 |
|---|---|---|
| 态选「覆盖」+ 输入框留空 | ❌ 会被当成「删除」 | — |
| 态选「覆盖」+ 勾 **「设为空值」** | ✅ `Override("")` | `DISPLAY=""`（winewayland 必需） |
| 态选「删除」 | ✅ `Remove` | 删掉不需要的变量 |

**为什么必须区分**（`docs/00-DECISIONS.md` D7）：对 wine 而言"变量存在但为空"
与"变量不存在"是**不同的行为**。真实案例：`winewayland` 模式需要 `DISPLAY=""`
（存在且为空），如果是删除 `DISPLAY`，wine 会退回 X11。

### 5.5 其他选项

| 选项 | 说明 |
|---|---|
| 参数 | 支持 `%keywords%`，见 §5.6 |
| 工作目录 | `null` = 继承游戏目录 |
| 前台等待 | `Wait=true` 时 Launcher 等它退出；配合 `Auto` 会一直卡到它退出 |
| 写入文件（高级） | `PreWrites`：`Prepare` 或 `AfterGameReady` 时写一个文件，路径与内容都支持关键字 |
| 立即启动 | 只在游戏 `Running` 相位可用，否则禁用并提示"需先启动游戏" |

**写入文件的约束**：默认禁止写游戏目录。路径含 `%game%` 或有 `PreWrites` 指向游戏目录时
会给明确警告（不阻止）——因为参考实现刻意不碰游戏文件。

### 5.6 关键字

| 关键字 | 替换为 | 可用相位 |
|---|---|---|
| `%build%` | runner 目录 | 全部 |
| `%prefix%` | wine prefix | 全部 |
| `%game%` | 游戏目录 | 全部 |
| `%launcher%` | LunarWard 数据目录 | 全部 |
| `%temp%` | 临时目录 | 全部 |
| `%exe%` | 游戏可执行文件名（`YuanShen.exe`） | 全部 |
| `%channel%` | 当前渠道（`Official` / `Bilibili`） | 全部 |
| `%runtime%` | 解析出的运行时名 | 全部 |
| `%pid%` | **游戏进程 pid** | **仅 `Running` 之后**（外部程序可用） |

命令模板（不是环境变量，用于自定义命令）：`%command%`、`%bash_command%`、
`%windows_command%`、`%launch_args%`、**`%post%`**（外部程序的拼接位）。

若自定义命令里**没有** `%post%`，外部程序仍由引擎独立拉起，UI 会提示
"你的自定义命令不包含 %post%，外部程序将由引擎在 Ready 后独立启动"。

### 5.7 启发式提示（只提示，不落盘）

| 文件名含 | 提示 |
|---|---|
| `inject` / `touch` / `hook` | "看起来是注入类程序，建议设为『伴随启动』" |
| `map` / `wiki` / `tool` | "看起来是辅助工具，建议设为『手动启动』" |

不改变任何配置、不下载任何东西，忽略即可。

> **本启动器不预置任何具体程序**（`docs/00-DECISIONS.md` D6），也不分发任何补丁二进制
> （D11）。`Hk4eTouch` 是 AGPL-3.0、`ZZZ-touch-unlocker` 连 LICENSE 都没有，
> 需要你自己获取并手选文件。见 [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md) §三。

---

## 六、环境 shell

设置 →「环境 shell」（或主界面 `[环境shell]`）。打开一个 **env / PATH / cwd 已按游戏启动
配置准备好** 的终端。

### 6.1 关键设计：同一份 `LaunchPlan.Env`

**绝不允许另写一套环境构建逻辑**（`docs/04-FEATURES.md` §6.2）。
shell 拿到的 `Env` 就是真实启动时那份，同一个对象、同一份合并结果。
所以"调试环境"和"实际启动环境"不可能不一致。

### 6.2 能做什么

| 能力 | 说明 |
|---|---|
| 前缀就绪 | `WINEPREFIX` / `WINELOADER` / `LD_LIBRARY_PATH` / `GST_PLUGIN_PATH` 全在 |
| 直接跑 wine | `winecfg` / `wine regedit` / `wineserver -k` 直接可用 |
| 命令预设 | 一排按钮：`winecfg`、`wine regedit`、`wineserver -k`、`wine --version`、`wine <exe>`、`ls "$WINEPREFIX/drive_c"` |
| 导出 env.sh | 生成 `#!/bin/sh` + `export K=V` + `cd <游戏目录>`，写到 `~/.cache/lunarward/env.sh`（可执行），可拿到外部终端用 |
| 环境表格 | 每行显示 **键 / 值 / 来源层**，一眼看出某个变量是哪一层给的 |
| cwd | 就是游戏目录 |

### 6.3 合并顺序（十层，后者覆盖前者）

| # | 层 | 来源 | 示例 |
|---|---|---|---|
| 1 | 基线 | 硬编码 | `WINEARCH=win64`、`WINEPREFIX` |
| 2 | runner 组件 | 组件 `features.env` | `WINE_FULLSCREEN_FSR=1` |
| 3 | dxvk 组件 | 组件 `features.env` | `DXVK_ASYNC=1` |
| 4 | `runtime.env` | 用户配置 | — |
| 5 | wine 设置 | `wine.{sync,language,shared_libraries}` | `WINEFSYNC=1`、`LANG`、`LD_LIBRARY_PATH` |
| 6 | 增强 | `enhancements.{hud,fsr}` | `DXVK_HUD`、`MANGOHUD` |
| 7 | `profile.env` | 用户配置 | — |
| 8 | 游戏特化 | 游戏适配器 | `DXVK_ENABLE_NVAPI`（ZZZ DX12）、`WINE_ENABLE_TIMEOUT_FIX`、`DISPLAY=""`（winewayland） |
| 9 | 外部程序 env | `EnvPatchKind` 三态 | 见 §5.4 |
| 10 | （cwd） | 外部程序 `WorkingDirectory` | 不是 env，但同属继承链 |

`EnvSources` 记录**每个键最终来自哪一层**，`launch-report.json` 里能看到全部。

### 6.4 生命周期

关标签 → 对整棵进程树发 `SIGHUP`；启动器退出 → 回收所有 shell。

---

## 七、动态背景与 Acrylic 分级

### 7.1 动态背景

| 项 | 说明 |
|---|---|
| 实现 | `LibVLCSharp.Avalonia` 的 `VideoView`，**不依赖系统 GStreamer 插件** |
| 循环 | `EndReached` → `Dispatcher.UIThread.Post` 回 UI 线程再 `Play()`（VLC 的回调在线程池线程上，不回 UI 线程会死锁；**不能**依赖 `MediaPlayer` 自带的 loop 标志） |
| 静音 | 始终 `Mute = true` |
| 省资源 | 游戏 `Running` 时 `Pause()`；窗口失焦降帧/暂停 |
| 缓存 | `~/.cache/lunarward/background/`，md5 比对，未变不重下 |
| 关闭时 | `video_background = false` 时**跳过视频下载**（省带宽） |

**回退静态图**：`video_background = false`、或 VLC 初始化失败时，用
`Image` + `Stretch="UniformToFill"`，逻辑完全相同、**不崩溃**。

探测逻辑见 `BackgroundService.ProbeVlc()`；探测失败原因会存下来给 UI 显示安装命令。

### 7.2 Acrylic 分级

Avalonia 没有跨平台的 backdrop blur API，所以按可用性分级降级，**全部不崩溃**：

| 级别 | 条件 | 做法 | 本期 |
|---|---|---|---|
| **L1** 真模糊 | KDE（KWin）支持 `_KDE_NET_WM_BLUR_BEHIND_REGION` | 窗口透明 + 设 blur 区域属性 | ❌ **不做**（远期） |
| **L2** 半透明 | 任意支持复合的合成器 | `TransparencyLevelHint = Transparent` + 半透明渐变层 | ✅ 已落地 |
| **L3** 纯色 | 无复合（X11 无 composite、软件渲染） | 不透明深色背景 | ✅ 已落地 |

**只做 L2 + L3，不做 KDE 特化**（`docs/04-FEATURES.md` §5.6）。理由：
Wayland 下没有标准 blur 协议（`ext-background-effect-v1` 仍在讨论），
依赖 KDE 私有属性会把应用绑死在单一桌面环境；
半透明渐变在视觉上已能传达 Fluent 质感，且处处可用。

`ThemeShadow`（Starward 用了 62 次）→ `DropShadowEffect`，跨平台一致，无风险。

本机实测（KDE Wayland）`ActualTransparencyLevel = Transparent`，L2 生效。

---

## 八、诊断

### 8.1 启动报告

**`~/.local/share/lunarward/launch-report.json`** —— 排障的核心，
记录完整个解析链条（`docs/02-ARCHITECTURE.md` §八）。

| 字段 | 看什么 |
|---|---|
| `success` / `exit_code` / `error` | 结果与失败原因（退出码语义见 TROUBLESHOOTING §2） |
| `phases[]` | 七相位各自的 `duration_ms`，哪个相位卡住一目了然 |
| `ready_signal` | `WineDevice` / `PlayerLog` / `Grace`（语义见 TROUBLESHOOTING §3） |
| `runtime.runner.resolved` | 最终用的是哪个 wine 二进制 |
| `runtime.explain` | 运行时是怎么解析来的（"profile 未覆盖，跟随默认"） |
| `env` / `env_sources` | 每个环境变量与它的来源层 |
| `command[]` | 实际执行的完整 argv |
| `external_programs[]` | 每个外部程序的 `trigger` / `started_after` / `pid` / `exit_code` |
| `game_pid` / `game_exit_code` | 游戏进程与退出码 |

UI 的「诊断」按钮直接渲染这个文件，并附 `game.log` 的路径。
每次启动**覆盖写**（原子写），只保留最近一次。

### 8.2 日志

| 文件 | 内容 | 上限 |
|---|---|---|
| `~/.cache/lunarward/logs/lunarward-yyyyMMdd.log` | 启动器自身日志，按天滚动 | — |
| `~/.cache/lunarward/logs/game.log` | 游戏 stdout/stderr | **8 MiB**（防 wine 灌爆磁盘） |
| `<游戏目录>/Player.log` | Unity 自己的日志（wine 下在 prefix 里，见 §9） | 游戏自管 |
| `<游戏目录>/driverError.log` | 驱动初始化错误 | 游戏自管 |

---

## 九、路径表

### 9.1 LunarWard 自己的路径

遵循 XDG Base Directory（变量为空或未设置时回退默认值）。

| 路径 | 内容 |
|---|---|
| `~/.config/lunarward/config.json` | **全部配置**：launcher / runtimes / profiles |
| `~/.local/share/lunarward/runners/` | 托管 wine runner（`<组件>/bin/wine`） |
| `~/.local/share/lunarward/dxvks/` | 托管 DXVK（`<组件>/x64/*.dll`） |
| `~/.local/share/lunarward/prefixes/` | 本启动器自管的 wine prefix |
| `~/.local/share/lunarward/components/` | an-anime-team 组件索引（`components.json`、`wine/*.json`、`dxvk/*.json`） |
| `~/.local/share/lunarward/launch-report.json` | **启动报告**（见 §8.1） |
| `~/.cache/lunarward/logs/` | `lunarward-yyyyMMdd.log`、`game.log` |
| `~/.cache/lunarward/background/` | 背景媒体缓存（`.mp4` / `.webm` / `.mkv` / `.jpg` / `.png` / `.webp`） |
| `~/.cache/lunarward/bilibili/` | `PCGameSDK5.1.0.zip` |
| `~/.cache/lunarward/env.sh` | 环境 shell 导出的脚本 |
| `~/.config/lunarward/config.json.bad-<unix-ts>` | 配置损坏时的备份（见 TROUBLESHOOTING §7） |

被覆盖的方式：

```bash
XDG_CONFIG_HOME=... XDG_DATA_HOME=... XDG_CACHE_HOME=... lunarward
```

### 9.2 游戏侧路径（只读或按需写）

| 路径 | 说明 |
|---|---|
| `<游戏目录>/config.ini` | 区服配置；**只有切渠道时写**，原子替换 |
| `<游戏目录>/YuanShen_Data/Plugins/PCGameSDK.dll` | B 站插件 |
| `<prefix>/drive_c/users/<user>/AppData/LocalLow/miHoYo/<游戏>/Player.log` | Unity 日志，`AwaitReady` 的 `PlayerLog` 信号来源 |
| `<prefix>/user.reg` | DXVK override 状态、账号切换的落点 |
| `<prefix>/drive_c/windows/system32/` | DXVK DLL 安装位置（原文件留 `.dll.old`） |

---

## 十、常见操作速查

| 想做的事 | 怎么做 |
|---|---|
| 换 wine 运行时 | 设置 → wine 运行时 → 重新扫描 → 选中 |
| 让补丁随游戏启动 | 外部程序 → 添加 → 触发方式选 **伴随启动** |
| 临时禁用补丁但保留配置 | 外部程序 → 取消勾选 **启用**（不要删） |
| 改保护期长度 | 设置 → 就绪保护期 (ms)，默认 `6000`，可调大到 `8000`+ |
| 看这次启动到底发生了什么 | 诊断 → 渲染 `launch-report.json` |
| 进游戏同款环境调 wine | 环境 shell → 选预设命令 |
| 在外部终端复现环境 | 环境 shell → 导出 env.sh → `source ~/.cache/lunarward/env.sh` |
| 切 B 站 | 设置 → 区服渠道 → Bilibili（缺 DLL 会硬拒绝并给下载按钮） |
| 关掉视频背景 | 设置 → 关闭 `video_background`（同时跳过视频下载） |
