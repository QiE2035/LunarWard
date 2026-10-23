# 03 — 相位化启动引擎

**本项目最核心的技术。** 其它所有功能都建立在它之上。

---

## 一、不变式

> **游戏必须是该 wine prefix 的第一个 wine 客户端。**

这条不变式不是建议、不是优化项，而是**不可妥协的架构约束**。违反它会导致
游戏**无窗口、无日志、约 5 秒静默消失**（`NtRaiseHardError 0x50000018`）。

实证见 `01-RESEARCH.md` §1（三个独立项目交叉验证）。

### 推论

1. `Prepare` 相位**必须**先清空 prefix 并**确认清零**，才能进入 `Launch`
2. 任何 `Trigger.Auto` 的外部程序**只能**在 `AwaitReady` 之后拉起
3. **不提供"游戏启动前运行程序"的能力**——外部程序只有 `Auto`（伴随启动）
   与 `Manual`（手动）两种触发，且 `Auto` 只在 `AwaitReady` 之后生效。
   在游戏之前运行任何 wine 程序正是破坏不变式的行为，因此从设计上排除。
4. Proton 默认走 `WineBinary` 而不是 `proton run`（多一层进程会破坏不变式）
5. 游戏退出后若还有残留 wine 进程，下次启动前必须清理

---

## 二、相位定义

```csharp
public enum Phase
{
    Prepare,      // 写 config.ini、映射 drive、清理 prefix、写模式文件
    Launch,       // Unix 侧 spawn `wine <exe>` —— 第一个客户
    AwaitImage,   // 等游戏镜像被映射进 *本 prefix* 的进程
    AwaitReady,   // 等反作弊/Unity 初始化过关
    PostLaunch,   // 第二客户登场：外部程序
    Running,      // 监视退出
    Cleanup,      // 清理残留、回写 sessions、写报告
}
```

### 状态机

```
        ┌──────────┐
        │ Prepare  │  清理 + 写 config.ini + 映射 drive
        └────┬─────┘
             │ 确认 prefix 无客户
        ┌────▼─────┐
        │  Launch  │  spawn wine —— 第一个客户
        └────┬─────┘
             │ 镜像被映射
        ┌────▼──────┐
        │AwaitImage │  轮询 /proc/*/maps
        └────┬──────┘
             │ 命中
        ┌────▼──────┐
        │AwaitReady │  winedevice / Player.log / grace
        └────┬──────┘
             │ 过关
        ┌────▼──────┐
        │PostLaunch │  拉起 Trigger.Auto 外部程序（第二个客户）
        └────┬──────┘
             │
        ┌────▼──────┐
        │  Running  │  child.WaitForExit + 收敛检测
        └────┬──────┘
             │ 退出
        ┌────▼──────┐
        │  Cleanup  │  按 OnGameExit 处理、写报告
        └───────────┘
```

任一相位失败 → 跳到 `Cleanup` 并返回 `LaunchError`。

---

## 三、各相位的实现细节

### 3.1 `Prepare`

按顺序执行：

1. **清理 prefix**（关键）
   ```
   a. 枚举 prefix_clients（见 §4）
   b. 若有：SIGTERM → 等 0.5s → 若仍在则 SIGKILL
   c. 调 `WINEPREFIX=<prefix> wineserver -k`
   d. 再等最多 3s，确认 prefix_clients 为空
   e. 若仍非空 → 返回 PrefixNotClean（退出码语义 4）
   ```
   > `winedevice.exe` 会忽略 SIGTERM，需要升级到 SIGKILL。
   > 参考实现 `ZZZ-touch-unlocker/wine/zzz-touch-unlocker` 的 `cmd_clean()`。

2. **校验渠道前置条件**（fail fast，见 `04-FEATURES.md` §2）
3. **写 `config.ini`**（原神）
4. **映射 wine drive**（若启用）
5. **写补丁模式文件**（若外部程序需要，见 `04-FEATURES.md` §1.4）
6. **解析运行时**（`RuntimeResolver`，见 `04-FEATURES.md` §3）
7. **构建 `LaunchPlan`**（env + 命令 + 外部程序清单）

### 3.2 `Launch`

从 **Unix 侧**启动，绝不用 `CreateProcessW`：

```csharp
var psi = new ProcessStartInfo
{
    FileName = resolvedWineBinary,          // …/bin/wine
    WorkingDirectory = gameDirectory,
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
};
foreach (var (k, v) in plan.Env) psi.Environment[k] = v;
// 可选：虚拟桌面、包装器链
foreach (var arg in plan.CommandArgs) psi.ArgumentList.Add(arg);

var child = Process.Start(psi);
```

**必须设置 `WorkingDirectory = 游戏目录`**：某些游戏依赖相对路径。

**stdout/stderr 重定向**到 `game.log`，双线程泵送，上限 8 MiB
（沿用 Starward `GAME_LOG_FILE_LIMIT` 的做法，防止 wine 灌爆磁盘）。

### 3.3 `AwaitImage`

```
deadline = now + waitTimeoutMs（默认 120_000）
loop:
    如果 child.HasExited → GameExitedDuringStartup
    pid = FindByImage(prefix, imageName)      // 见 §4
    如果 pid != null → 命中，记录 pid，进入 AwaitReady
    如果 now > deadline → GameExitedDuringStartup（附 comm 信息便于排障）
    sleep 200ms
```

**不能用 `child.Id`** 作为游戏 pid：它可能只是 runner 的 stub 或包装器，
真正的游戏是它 fork 出的子进程。

**也不能只看进程名**：见 `01-RESEARCH.md` §1.6。

### 3.4 `AwaitReady` —— 本引擎最关键的门禁

```
deadline = launchEpoch + graceMs（默认 6000）
loop:
    如果 gamePid 已死 → GameExitedDuringStartup（"保护期被破坏"）
    ① 若本 prefix 出现 winedevice.exe     → signal = WineDevice,   break
    ② 若 Player.log 的 mtime >= launchEpoch → signal = PlayerLog,    break
    ③ 若 now >= deadline                   → signal = Grace,        break
    sleep 100ms
```

**三个信号的语义**：

| 信号 | 含义 | 可靠性 |
|---|---|---|
| `WineDevice` | 保护驱动宿主已启动，`ZwLoadDriver` 成功 | **最强** |
| `PlayerLog` | Unity 已开始初始化 | 强 |
| `Grace` | 超时兜底 | 最弱，可能过早 |

**`Player.log` 路径**：
```
<prefix>/drive_c/users/*/AppData/LocalLow/miHoYi/<game>/Player.log
```
（注意实际是 `miHoYo`，`miHoYi` 是笔误，实现时用 glob 匹配 `*/Player.log` 更稳）

**`grace` 必须可配且暴露到 UI 高级项**：保护期长度随游戏版本/wine 版本漂移。
默认 6000ms 来自实测。

**注入太早的后果**（`01-RESEARCH.md` §1.5）：
> 注入得太早 = 在保护期里当第二个客户 = 游戏自杀。

### 3.5 `PostLaunch`

拉起所有 `Trigger.Auto && Enabled` 的外部程序。

**硬约束**：此相位只在 `AwaitReady` 成功后才进入。这是"伴随启动能安全工作"
的唯一保证。

每个程序独立拉起，失败不阻塞其他程序，也不终止游戏。

详见 `04-FEATURES.md` §1。

### 3.6 `Running`

```
child.WaitForExit();                    // 等主进程
// 收敛：等所有 wine 客户端退出（最多 30s）
loop:
    clients = prefix_clients(prefix)
    如果 clients 为空 → break
    如果超时 → 记录警告，进入 Cleanup 时按 OnGameExit 处理
    sleep 500ms
```

> 参考实现用 `ps -A` 每 3 秒轮询一次（Starward）或 sleep 3s 循环
> （anime-launcher-sdk）。我们用 `/proc` 精确收敛。

### 3.7 `Cleanup`

1. 按 `OnGameExit` 处理仍存活的 `Trigger.Auto` 程序
   - `Kill`（默认）：终止
   - `Keep`：保留（用户明确要求）
2. 更新游戏时长统计
3. 清理残留 wine 客户端（可选，下次 `Prepare` 也会清）
4. 写 `launch-report.json`
5. 返回结果

---

## 四、进程发现

### 4.1 数据结构

```csharp
public sealed record ProcessInfo(
    int Pid,
    string Comm,           // /proc/<pid>/comm
    string? ImageName,     // 从 /proc/<pid>/maps 提取的匹配镜像名
    ProcessState State,    // Running | Zombie | Dead
    DateTime? StartTime
);

public enum ProcessState { Running, Zombie, Dead }
```

### 4.2 `prefix_clients(prefix)`

```csharp
// 伪代码
foreach (var dir in Directory.EnumerateDirectories("/proc"))
{
    if (!int.TryParse(Path.GetFileName(dir), out var pid)) continue;
    if (pid == Environment.ProcessId) continue;             // 排除自身

    var procDir = new DirectoryInfo(dir);
    if (procDir.OwnerId != getuid()) continue;              // 只有自己的进程可读 environ

    if (ReadState(dir) == ProcessState.Zombie) continue;     // 排除僵尸

    var env = ReadEnviron(dir);                             // NUL 分隔
    if (!env.Contains($"WINEPREFIX={prefix}")) continue;

    yield return MakeInfo(pid);
}
```

**注意**：
- `environ` 读失败要静默跳过（不同权限/内核配置）
- 必须**精确匹配** `WINEPREFIX=<prefix>`，不能前缀匹配，否则会误杀其他 prefix
- 排除僵尸：僵尸仍持有 `/proc/<pid>/environ`，但已不占用 wineserver

### 4.3 `FindByImage(prefix, imageName)`

```csharp
foreach (var client in prefix_clients(prefix))
{
    if (ReadMaps(client.Pid).Contains(imageName, StringComparison.OrdinalIgnoreCase))
        return client;
}
return null;
```

`/proc/<pid>/maps` 的匹配是对**整行做子串匹配**，因为 wine 的路径映射形式多样
（`Z:\home\...` 与 `/home/...` 都可能出现）。

### 4.4 为什么不能只看 `comm`

`01-RESEARCH.md` §1.6 的实证：
> runner 的 steam stub 会把 unix 进程名固定成 `steam.exe`，
> 真正的游戏是被它 fork 出来的子进程，只有镜像映射是可靠的"游戏已加载"信号。

---

## 五、`LaunchPlan`

相位引擎的输入。**环境 shell 与外部程序都复用它**，保证环境一致。

```csharp
public sealed class LaunchPlan
{
    public required string ProfileId { get; init; }
    public required string GameKind { get; init; }        // genshin | zzz
    public required string GameDirectory { get; init; }
    public required string GameImage { get; init; }       // YuanShen.exe 等

    public required ResolvedRuntime Runtime { get; init; }
    public required string Prefix { get; init; }

    // 从 Unix 侧要执行的命令
    public required string WineBinary { get; init; }
    public required List<string> Arguments { get; init; }

    // 完整的环境变量（十层合并结果）
    public required IReadOnlyDictionary<string, string> Env { get; init; }

    // 每个 env 键来自哪一层（用于 UI 展示与排障）
    public required IReadOnlyDictionary<string, string> EnvSources { get; init; }

    // 待拉起的外部程序
    public required IReadOnlyList<ExternalProgramPlan> ExternalPrograms { get; init; }

    // 相位参数
    public TimeSpan WaitImageTimeout { get; init; } = TimeSpan.FromSeconds(120);
    public TimeSpan ReadyGrace       { get; init; } = TimeSpan.FromMilliseconds(6000);
    public TimeSpan RunningSettle    { get; init; } = TimeSpan.FromSeconds(30);
}
```

### 5.1 不可变性

`LaunchPlan` 构建后**只读**。相位引擎不改它，只读。
这样 `launch-report.json` 与"环境 shell"拿到的是**同一份**数据，
不会出现"调试环境和实际启动环境不一致"的问题。

---

## 六、环境变量十层优先级

从低到高，后者覆盖前者：

| # | 层 | 来源 | 示例 |
|---|---|---|---|
| 1 | 基线 | 硬编码 | `WINEARCH=win64`、`WINEPREFIX` |
| 2 | runner 组件 | 组件 `features.env` | `WINE_FULLSCREEN_FSR=1` |
| 3 | dxvk 组件 | 组件 `features.env` | `DXVK_ASYNC=1` |
| 4 | `runtime.env` | 用户配置 | |
| 5 | wine 设置 | `wine.{sync,language,shared_libraries}` | `WINEFSYNC=1`、`LANG`、`LD_LIBRARY_PATH` |
| 6 | 增强 | `enhancements.{hud,fsr}` | `DXVK_HUD`、`MANGOHUD` |
| 7 | `profile.env` | 用户配置 | |
| 8 | 游戏特化 | 游戏适配器 | `DXVK_ENABLE_NVAPI`（ZZZ DX12）、`WINE_ENABLE_TIMEOUT_FIX`、`DISPLAY=""`（winewayland） |
| 9 | **外部程序 env** | `EnvPatchKind` 三态 | 见 `04-FEATURES.md` §1.3 |
| 10 | （`cwd`） | 外部程序 `WorkingDirectory` | 不是 env，但同属继承链 |

**`EnvSources` 记录每个键最终来自哪一层**，用于：
- UI 显示"这个变量是哪来的"
- `launch-report.json`
- 排障时判断用户覆盖是否生效

---

## 七、关键字替换

### 7.1 标准集（沿用 an-anime-team）

| 关键字 | 替换为 |
|---|---|
| `%build%` | runner 目录 |
| `%prefix%` | wine prefix |
| `%game%` | 游戏目录 |
| `%launcher%` | LunarWard 数据目录 |
| `%temp%` | 临时目录 |

### 7.2 扩展集（本项目新增）

| 关键字 | 替换为 |
|---|---|
| `%exe%` | 游戏可执行文件名（如 `YuanShen.exe`） |
| `%pid%` | 游戏进程 pid（**仅在 Running 之后可用**，外部程序可用） |
| `%channel%` | 当前渠道（`Official` / `Bilibili`） |
| `%runtime%` | 解析出的运行时名 |

### 7.3 命令模板

| 关键字 | 含义 |
|---|---|
| `%command%` | 完整命令（= `%bash_command% %windows_command% %launch_args%`） |
| `%bash_command%` | Unix 侧部分（wine 二进制 + 包装器） |
| `%windows_command%` | Windows 侧部分（exe + 参数） |
| `%launch_args%` | 游戏启动参数 |
| **`%post%`** | **本项目新增**：外部程序的拼接位 |

**`%post%` 的意义**：现有实现靠 `!bash_command.contains(INJECTOR_NAME)` 这种
字符串探测来避免重复注入，非常脆弱。用插槽后语义明确。

若用户自定义 `command_override` 且未包含 `%post%`，则外部程序**仍由引擎独立拉起**
（不依赖命令字符串），并在 UI 提示"你的自定义命令不包含 %post%，外部程序将由引擎
在 Ready 后独立启动"。

---

## 八、包装器链

有序列包装器，外层在前：

```
[gamemoderun] [gamescope -W -H ... --] [mangohud] <wine> <exe> <args>
```

顺序显式可见，用户可增删、可插入自定义。

**与外部程序的关系**：外部程序**不经过**包装器链（它们是独立进程）。
若用户希望外部程序也带 `gamemoderun`，在它自己的 `Args` 或 `path` 里写。

---

## 九、退出码语义

| 码 | 含义 |
|---|---|
| 0 | 成功 |
| 1 | 参数/预检失败 |
| 2 | 游戏启动期退出（多半是保护期被破坏） |
| 3 | 外部程序失败（不终止游戏，仅记录） |
| 4 | prefix 没清干净 |

（沿用 `ZZZ-touch-unlocker` 的约定，便于对照排障）

---

## 十、排障路径

用户报告"点了启动，5 秒没窗口就退了"，按此顺序查：

```
1. pgrep -af 'wineserver|winedevice'      # 有输出说明 prefix 是"热"的
2. 检查 launch-report.json 的 Prepare 相位是否有 PrefixNotClean
3. 检查 AwaitImage 是否命中（有没有找到 YuanShen.exe）
4. 检查 AwaitReady 的 signal 是什么
   - Grace 且很快 → 保护期可能更长，调大 grace
   - WineDevice → 保护已过，问题在别处
5. 看 game.log 是否有 wine 报错
6. 看 driverError.log（游戏目录）是否有 initDriver Failed
```

UI 应把这套检查做成"诊断"按钮，而不是让用户读文档。
