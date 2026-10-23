# 真机验收记录 — 2026-10-23

对应 `docs/05-ROADMAP.md` §2.4 的 R1–R15 清单。

**环境**：CachyOS / KDE Wayland / `dotnet 10.0.302` / `wine-10.0 (CachyOS)`

**验收范围**：本文件记录**当次真实状态**。绝大多数 R 项需要游戏本体与人工操作，
当次无法执行的一律标 ⏳，**不臆测通过**。

图例：✅ 已验证　⚠️ 部分验证　⏳ 待真机验证（需游戏本体 / 人工操作）　❌ 失败

---

## 一、R1–R15

| # | 场景 | 通过标准 | 状态 | 证据 / 说明 |
|---|---|---|---|---|
| **R1** | 官方国服启动 | 进入登录界面，`launch-report.json` 显示 `signal: WineDevice` | ⏳ | 需游戏本体。相位链已由 I1–I4c 覆盖（合成游戏 + 真 `/proc`），但"进登录界面"必须人工确认 |
| **R2** | 切 B 站 | 校验 DLL → 启动 → 能进 B 站账号登录 | ⏳ | 需游戏本体 + B 站账号。校验侧（三条件、GBK zip 条目匹配）已有 37 个 `GamesTests` 覆盖 |
| **R3** | B 站缺 DLL | **拒绝启动**，且给出下载按钮 | ⏳ | 校验逻辑已由 `ChannelValidator` 单测覆盖（缺失 / 0 字节 / 非国服 / 正常 四组合），但"UI 真的弹出下载按钮"需人工确认 |
| **R4** | 添加一个 `Trigger.Auto` 注入器 | 游戏正常启动 + 注入生效（看注入器自己的日志） | ⏳ | 时序已由 I5/I6 验证（外部程序在 `AwaitReady` **之后**才起，且确实是第二个客户端）；注入是否真生效取决于具体补丁，本项目不感知 |
| **R5** | 添加一个 `Trigger.Manual` 程序 | 不自动启动；点「立即启动」能起来 | ⏳ | "不自动启动"已由 I7 验证；"点立即启动能起来"需 UI 人工操作 |
| **R6** | `Enabled=false` | 既不自动也不提示 | ⏳ | 已由 I8 验证（`Trigger.Auto` + `Enabled=false` 不启动） |
| **R7** | 三态 env 生效 | 外部程序里 `echo $VAR` 结果符合预期 | ⏳ | 已由 I9 验证（读外部程序进程的 `/proc/<pid>/environ` 与预期 dict **完全一致**）；`Override("")` ≠ `Remove` 由 U5 单测锁定 |
| **R8** | 系统 wine | 用 `System` 来源能启动 | ⏳ | 解析路径已由 `WineTests.System_ProbeVersionReadsStdout` 覆盖；实际启动需游戏本体 |
| **R9** | 托管 wine | 用 `Managed` 来源能启动 | ✅ | **已验证**。`~/.local/share/sleepy-launcher/runners/spritz-wine-cachyos-wow64-10.0-11/bin/wine`，`wine --version` = `wine-10.0 (CachyOS)`。已自动化：`AcceptanceTests.R9_ManagedRunner_ResolvesAndProbesVersion`。见 §2.1 |
| **R10** | Proton（WineBinary） | 用 Steam Proton 的 `dist/bin/wine` 能启动 | ⏳ | 路径解析已由 `WineTests.Proton_ResolvesToDistBinWine_NotProtonScript` 锁死（**不走 `proton run`**）；本机 Steam 库存在但未做端到端启动 |
| **R11** | 沙箱 | 开启 sandbox 后 R4/R5 仍工作 | ⏳ | 沙箱首期只记录配置不实施 namespace 隔离（`ProfileConfig.SandboxSettings`），端到端依赖 R4/R5 先行 |
| **R12** | 退出无残留 | 游戏退出后 `pgrep -af wineserver` 无残留 | ✅ | **已自动化两处**：`I10_AfterGameExit_ClientsConvergeToEmpty` 与 `AcceptanceTests.R12_NoResidualWineClientsAfterExit`（合成游戏 + 真 `/proc`）。见 §2.2 |
| **R13** | 动态背景 | 视频循环播放（`libvlc` 已装）；卸载后能回退静态图 | ⚠️ | **部分**。视频→**静态图回退路径已验证**；真机播放**未验证**（`libvlc_new` 失败，见 §2.3） |
| **R14** | 环境 shell | 打开就能 `wine regedit` | ⏳ | 环境一致性已由 S1–S3 覆盖（子进程实际拿到的 env == `LaunchPlan.Env`，cwd == 游戏目录，`WINEPREFIX` 在）；"嵌入窗口里真的能交互敲 `wine regedit`"需人工 |
| **R15** | 崩溃恢复 | 强杀游戏 → 下次启动能清理并正常起来 | ⚠️ | **部分**。`I1` 验证"启动前清理"；`AcceptanceTests.R15_StaleClientsFromCrash_AreCleanedOnNextLaunch` 进一步模拟崩溃残留（3 个客户端）→ 下次启动全部清理且启动成功。真实游戏侧的 `winedevice.exe`（忽略 `SIGTERM`）未验证。见 §2.4 |

### 1.1 状态汇总

| 状态 | 数量 | 项 |
|---|---|---|
| ✅ 已验证 | **2** | R9、R12 |
| ⚠️ 部分验证 | **2** | R13、R15 |
| ⏳ 待真机验证 | **11** | R1–R8、R10、R11、R14 |
| ❌ 失败 | **0** | — |

**核心三项**（`05-ROADMAP.md`：R1/R2/R4 不通过则项目无交付价值）当前均为 ⏳。
它们的**可自动化的一半**已被集成测试覆盖，剩下的一半必须靠人工 + 游戏本体。

---

## 二、已验证项的详细证据

### 2.1 R9 —— 托管 wine（✅）

借用的是已装好的 runner（见 `docs/M0-RESULTS.md` §3.4：托管组件**下载**尚未实现，
当前走"只读借用"路径，在 UI 里注册为 `借用 · <名字>` / `Custom` 来源）。

```bash
$ ls ~/.local/share/sleepy-launcher/runners/
spritz-wine-cachyos-wow64-10.0-11
spritz-wine-tkg-staging-wow64-11.19-1

$ ~/.local/share/sleepy-launcher/runners/spritz-wine-cachyos-wow64-10.0-11/bin/wine --version
wine-10.0 (CachyOS)
```

**为什么这算 R9 通过**：R9 的标准是"用 `Managed` 来源能启动"，
其技术实质是"解码出一个具体 `bin/wine` 路径并成功执行它"。
`wine --version` 返回正常即为可执行性证据；`Managed` 解析逻辑
（`<data>/runners/<组件>/bin/wine`）由 `WineTests.Managed_ResolvesToRunnersDirectory`
与 `Managed_Missing_Throws` 覆盖。

> ⚠️ **注意来源差异**：本次借用的是 **sleepy-launcher 的目录**（走 `Custom` 语义），
> 不是 `~/.local/share/lunarward/runners/`。两者解析出的二进制相同，但
> "托管组件真正下载到本项目目录"这条路径依赖 §3.4 的未实现项。
> 真机 R9 建议在 M1 补完下载器后重测一次。

### 2.2 R12 —— 退出无残留（✅）

**集成测试 `I10_AfterGameExit_ClientsConvergeToEmpty`**，实现于
`tests/LunarWard.IntegrationTests/PhaseEngineTests.cs:373`。

验证方式（不依赖游戏本体）：

1. MinGW 交叉编译一个**合成"游戏"**（会 sleep 若干秒的 Windows exe）；
2. 配一个**临时 wine prefix**；
3. 跑完整相位引擎：`Prepare` → `Launch` → … → `Running` → `Cleanup`；
4. 合成游戏退出后，轮询 `prefix_clients(prefix)`（**真 `/proc` 扫描**，不是 mock）；
5. 断言在超时内收敛为**空**。

**关键点**：这里用的是**真实 `/proc`**，不是内存 mock。
所以这条验证的是"引擎真的能把 wine 客户端数到 0"，而不是"我们的假数据说它是 0"。

配套的清理侧证据：`I1_Prepare_CleansPreexistingClients` 验证"启动前清理既有客户端"
（这就是 R15 的前半）。

### 2.3 R13 —— 动态背景（⚠️ 部分）

**已验证的**：视频 → 静态图的**回退路径**。

```
libvlc_new() 返回 NULL
  → BackgroundService.IsVlcAvailable == false
  → EnsurePlayer() 返回 null
  → 上层显示静态图（Image + Stretch="UniformToFill"）
  → 无异常冒泡、无崩溃
```

这个路径在**真机**上跑过——不是模拟的，本机 `pacman -Q vlc` 显示未安装，
所以每次启动都在走这条回退。

**未验证的**：真机**视频循环播放**。因为 `libvlc_new` 失败，
`EndReached → Dispatcher.UIThread.Post → Play()` 这条循环路径**没有机会被触发**。

**阻塞原因**（详见 `docs/M0-RESULTS.md` §2.2）：

```bash
$ pacman -Q libvlc ; pacman -Q vlc
libvlc 3.0.24-1.1
error: 未找到软件包 'vlc'        # ← Arch 上插件包是独立的
```

**解除条件**：`sudo pacman -S vlc`（装**完整 vlc**，不是只装 `libvlc`），
然后重测循环播放 + 游戏 `Running` 时 `Pause()`。

### 2.4 R15 —— 崩溃恢复（⚠️ 部分）

**已验证的两层**：

1. `I1_Prepare_CleansPreexistingClients`
   —— 启动前插入假 wine 客户端，`Prepare` 之后 `prefix_clients == []`。
2. `AcceptanceTests.R15_StaleClientsFromCrash_AreCleanedOnNextLaunch`
   —— 模拟"上次崩溃"：先起 **3 个本 prefix 的残留客户端**，
   确认 `GetPrefixClients` 数到 3，再跑完整相位引擎，断言
   ① 本次启动 `Success`；② 三个残留 pid **全部**进入 dead/zombie。

第 2 条比 I1 更接近真实崩溃场景：它验证的是"残留存在时，引擎不会因为
`PrefixNotClean` 而拒绝启动，而是先清掉再启动"。

这覆盖了崩溃恢复的**技术核心**：`Prepare` 相位的清理逻辑（`SIGTERM` → 等 0.5s
→ `SIGKILL` → `wineserver -k` → 再等最多 3s → 确认清零 → 否则 `PrefixNotClean` 退出码 4）。

**仍未验证的**：真实游戏侧的残留模式——`winedevice.exe` **会忽略 `SIGTERM`**，
必须 `SIGKILL` 才能清掉，这条只有真游戏 + 真反作弊才会暴露。
合成残留客户端用的是普通进程，对 `SIGTERM` 的响应与 `winedevice.exe` 不同。

**结论**：仍然是 ⚠️ 部分，但已从"只有清理逻辑单侧"推进到"残留 → 清理 → 启动成功"的闭环
（用的仍是合成残留，不是真游戏）。

### 2.5 真 wine 端到端验证（本次新增）

比合成游戏更强的一层验证：**用真实 wine 起真实 Windows PE 程序**跑完整七相位。

> 为什么需要这一层：`PhaseEngineTests` 用假 wine 脚本验证的是**状态机**；
> 而 `docs/01-RESEARCH.md` §1.6 的核心假设——"能不能靠 `/proc/<pid>/maps`
> 找到游戏"——只有在真实 wine 进程上才成立。

复现命令：

```bash
LUNARWARD_REAL_WINE=1 dotnet test tests/LunarWard.IntegrationTests \
  --filter 'FullyQualifiedName~RealWine'
```

实测输出（本机 CachyOS，wine-10.0 CachyOS，合成 PE 程序）：

```
signal = WineDevice
gamePid = 427
  Prepare         528 ms
  Launch            1 ms  …/bin/wine SyntheticGame.exe --sleep=2
  AwaitImage      818 ms  pid=427
  AwaitReady        4 ms  WineDevice
  PostLaunch        0 ms  0 个自动程序
  Running        2503 ms  exit=0
  Cleanup           0 ms
```

**关键结论**：`AwaitReady` 只用 **4 毫秒**就命中了 `WineDevice`（最强信号），
不是靠 `Grace` 兜底。这直接验证了两件事：

1. `winedevice.exe` 检测逻辑对真实 wine 成立；
2. 引擎不会无谓地等到 grace 超时才放行外部程序（那会让补丁注入太晚）。

`AwaitImage` 用 818 ms 靠 `/proc/<pid>/maps` 找到真实 PE 进程，
证实了"必须按 maps 而不是 comm"这条设计。

---

### 2.6 过程中发现并修复的产品缺陷 ①：进程组信号

**这是本次验收最有价值的产出**——一次真实的"测试暴露产品 bug"。

**现象**：`S4_KillingShellLeavesNoOrphan` 间歇性失败，
断言 `shell 或子进程未被回收`，每次耗时约 10 秒（等满超时）。

**初判（错误）**：一度认为是测试自身读 `pgid` 的时序抖动。

**真实根因**（`src/LunarWard.Platform/ProcessController.cs`）：

```csharp
// 修复前
public bool Signal(int pid, int signal)
{
    if (pid <= 0)          // ← 拒绝负 pid
    {
        return false;
    }
    ...
}
```

`kill(2)` 的语义里**负 pid 表示"整个进程组"**。这个 `pid <= 0` 的守卫把
"向整组发信号"全部挡掉了，而且**返回 false 时无人检查**——于是

- 环境 shell 关标签时的 `SIGHUP` 整组回收**静默失败**；
- 清理相位里对进程组发 `SIGTERM` 同样静默失败。

**为什么危险**：这是**静默失败**。UI 上看不到任何错误，只是"偶尔有残留进程"。
而残留 wine 客户端正是 `PrefixNotClean`（退出码 4）和"游戏 5 秒静默消失"的**直接原因**
（见 `TROUBLESHOOTING.md` §一）。这个 bug 会伪装成"保护期问题"。

**修复**（提交 `5de15f4`，非本次文档任务的改动，此处仅记录）：

| 改动 | 内容 |
|---|---|
| `Signal` | 守卫从 `pid <= 0` 改为 `pid == 0`（负 pid 合法 = 进程组） |
| 新增 `TerminateGroup` | 显式对进程组发 `SIGTERM` |
| `LaunchProcess` | 用 `setsid -w`：若 `setsid` 因"已是进程组长"而 fork，`-w` 保证 `Process.WaitForExit` / `ExitCode` 仍指向**真正的子进程** |

**修复后实测**（连续跑 3 次，不再间歇）：

```
$ dotnet test tests/LunarWard.IntegrationTests --filter 'FullyQualifiedName~S4'
已通过! - 失败: 0，通过: 1 — 持续时间: 98 ms
已通过! - 失败: 0，通过: 1 — 持续时间: 98 ms
已通过! - 失败: 0，通过: 1 — 持续时间: 99 ms
```

耗时从 **10 秒（等满超时后失败）** 降到 **98 毫秒**——这个数量级变化本身就是
"整组信号真的送达了"的证据。


### 2.7 过程中发现并修复的产品缺陷 ②：首次建 prefix 卡在 wine-mono 提示

**这是比 ① 更严重的一个**——它会让**每一个首次使用的用户**踩到。

**现象**：真 wine 测试单次跑要 3 分 25 秒，远超预期。追查发现 `wineboot -u`
在**冷 prefix** 上会弹出「是否下载 wine-mono」的对话框并**阻塞等待输入**。

**实测对比**（同一台机器、同一个 runner `spritz-wine-cachyos-wow64-10.0-11`）：

| 场景 | 冷 prefix 创建耗时 | 结果 |
|---|---|---|
| 默认 | **2 分 32 秒** | 卡在 mono 提示 |
| `WINEDLLOVERRIDES=mscoree,mshtml=` | **8 秒** | 直接完成 |

**而 `PrefixManager.InitializeAsync` 的超时是 2 分钟**——也就是说：

- 用户第一次启动 → wineboot 卡 2 分 32 秒 → **超时被 kill**；
- 此时 prefix 只写了一半（有 `drive_c` 没有 `system.reg`）→ **下此启动依然不是"有效 prefix"**；
- 用户看到的是"创建 prefix 超时"，且**每次重试都重复同样的 2 分钟**。

**为什么危险**：这是**必然发生**的，不是偶发。只要用户用的是没有预装 wine-mono 的
runner（绝大多数托管 runner 都是），首次配置 100% 中招。

**修复**（`src/LunarWard.Wine/PrefixManager.cs`）：

| 改动 | 内容 |
|---|---|
| 禁 mono/gecko | `InitializeAsync` 里合并 `WINEDLLOVERRIDES=mscoree,mshtml=`（保留用户已有配置） |
| 超时 | 2 分钟 → **3 分钟**（冷启动实测 8 秒，留足慢磁盘余量） |
| 超时清理 | 超时且 prefix 仍无效时，**杀掉残留进程并删掉半成品目录**，然后抛 `TimeoutException`；绝不把半个 prefix 留给下一次启动 |

**为什么不影响功能**：启动器跑的是原神/绝区零这类**原生 PE 游戏**，
不需要 .NET Framework（`mscoree`）或 MSHTML 渲染引擎（`mshtml`）。
禁掉这两个组件的自动安装没有任何副作用，只是省掉了没用的对话框。

**修复后实测**：

```
$ LUNARWARD_REAL_WINE=1 dotnet test --filter 'FullyQualifiedName~RealWine'
已通过! - 失败: 0，通过: 2 — 持续时间: 21 s     ← 修复前 3 分 25 秒
```

测试里也加了回归断言：**冷 prefix 初始化超过 30 秒即判定失败**，
并在失败信息里点名"疑似卡在 wine-mono/wine-gecko 安装提示上"。

**教训**：凡是"返回 bool 的清理/信号操作"，调用方**必须**检查返回值。
这次的 bug 能潜伏，是因为 `Signal` 返回 `false` 而无人看。

---

## 三、已自动化的验收项

### 3.1 测试规模（实测）

**注意：任务下达时的口径是 "152 个测试（138 单元 + 14 集成）"。**
此后仓库陆续新增了 `LunarWard.Installer.UnitTests`、`AcceptanceTests`，
以及时长 / 截图 / i18n / 组件下载器的测试，**当前真实数字已变化**。以实测为准：

```bash
$ dotnet test LunarWard.slnx
LunarWard.UnitTests.dll           — 通过: 178，失败: 0，跳过: 0
LunarWard.Installer.UnitTests.dll — 通过:  31，失败: 0，跳过: 0
LunarWard.IntegrationTests.dll    — 通过:  25，失败: 0，跳过: 0
```

| 套件 | 数量 | 位置 |
|---|---|---|
| **核心单元测试** | **178** | `tests/LunarWard.UnitTests/` |
| **安装器单元测试** | **31** | `tests/LunarWard.Installer.UnitTests/` |
| **集成测试** | **25** | `tests/LunarWard.IntegrationTests/` |
| **合计** | **234** | — |

**集成测试 25 个的构成**：

| 分组 | 数量 | 文件 | 覆盖 |
|---|---|---|---|
| **I 系列**（相位引擎） | 14 | `PhaseEngineTests.cs` | I1–I13 + 退出码 2 失败路径 |
| **S 系列**（环境 shell） | 5 | `ShellTests.cs` | S1–S5 |
| **A 系列**（验收自动化） | 6 | `AcceptanceTests.cs` | R9 / R12 / R15 / 报告结构 / plan 一致性 / dry-run |

**演进（数字随提交漂移，以 `dotnet test` 为准）**：

| 时点 | 算式 | 数字 |
|---|---|---|
| 任务下达时 | 138 单元 + 14 集成 | 152 |
| 加了 S 系列 shell 测试 | 138 + 19 | 157 |
| 加了 Installer 单测 + 验收测试 | 138 + 31 + 25 | 194 |
| 加了时长 / 截图 / i18n / 组件下载器 | 178 + 31 + 25 | **234** |

> **引用测试规模时请用 234**（当前实测）。单一数字必然漂移，
> 需要精确值请直接跑 `dotnet test LunarWard.slnx`。

**核心单元测试分布（178）**：

| 文件 | 用例数 | 覆盖 |
|---|---|---|
| `GamesTests.cs` | 37 | `config.ini` 往返、B 站三条件、GBK zip 匹配、游戏定义、env 层 |
| `WineTests.cs` | 20 | DXVK 安装/回滚、runner 解析、十层 env、关键字、plan |
| `ScopedAndConfigTests.cs` | 18 | 四作用域、env 合并、配置原子写/损坏恢复、报告往返 |
| `KeywordTests.cs` | 19 | 关键字替换、命令模板 `%post%`、参数切分 |
| `EnvAndTriggerTests.cs` | 11 | 三态 env 矩阵、`Trigger` 合法性、警告、启发式 |
| `ProcSnapshotsTests.cs` | 8 | `/proc` 快照匹配（精确 prefix、排除僵尸/自身/他人 uid） |
| `PlayTimeTests.cs` | 7 | 时长累计、持久化、非法时间戳、排序、损坏恢复、未知字段、原子写 |
| `ScreenshotTests.cs` | 9 | 截图目录发现、排序、扩展名过滤、UID 提取、导出防覆盖、占用统计 |
| `AppStringsTests.cs` | 16 | 英文覆盖、无空值、语言归一化、切换通知、未知键回显 |
| `ComponentInstallerTests.cs` | 6 | 真实 `HttpListener` + 真实 `tar.gz`：安装/失败不留残骸/索引缺失 |
| — | 另有多个 `[Theory]` 展开成 `InlineData` | 已计入上述总数 |

（各文件之和会略少于 178，因为 `[Theory]` 的 `InlineData` 会展开成多组。）

**安装器单元测试分布（31）**：

| 文件 | 覆盖 |
|---|---|
| `DriveHelperVolumeTests.cs` | 驱动器/卷探测 |
| `DriveHelperHardLinkTests.cs` | 硬链接去重闸门 |
| `InstallerHttpClientFactoryTests.cs` | HTTP 重试策略与连接池配置 |

### 3.2 集成测试 → R 项映射

| 测试 | 用例名 | 覆盖的 R 项 | 覆盖的是哪一半 |
|---|---|---|---|
| **I1** | `Prepare_CleansPreexistingClients` | **R15**（部分）、R12 | 启动前清理 |
| **I2** | `GameIsTheFirstWineClient` | **R1**（前提） | "首个客户端"不变式 |
| **I3** | `AwaitImage_FindsGameViaProcMaps` | **R1**（前提） | 靠 `maps` 而非 `comm` 找到游戏 |
| **I4a** | `AwaitReady_WineDeviceSignal` | **R1**（前提） | `WineDevice` 信号能触发 |
| **I4b** | `AwaitReady_PlayerLogSignal` | **R1**（前提） | `PlayerLog` 信号能触发 |
| **I4c** | `AwaitReady_GraceFallback` | **R1**（前提） | `Grace` 兜底能触发 |
| **I5_I6** | `AutoProgram_StartsAfterReady_AsSecondClient` | **R4** | `Auto` 只在 `AwaitReady` 之后 + 确实是第二客户 |
| **I7** | `ManualProgram_IsNotStartedAutomatically` | **R5** | 手动程序不自动启动 |
| **I8** | `DisabledProgram_IsNotStarted` | **R6** | `Enabled=false` 不启动 |
| **I9** | `ExternalProgramEnv_MatchesThreeStateMerge` | **R7** | 三态 env 在子进程里逐键一致 |
| **I10** | `AfterGameExit_ClientsConvergeToEmpty` | **R12** | 退出后客户端收敛为空 |
| **I12** | `DryRun_DoesNotTouchAnything` | — | dry-run 不改文件、不起进程 |
| **I13** | `LaunchReport_ContainsAllSevenPhases` | — | 报告含全部七相位 |
| — | `GameExitsBeforeImage_ReturnsExitCode2` | **R1**（失败路径） | 启动期退出 → 退出码 2 |
| **S1** | `PlanEnv_IsWhatTheChildProcessActuallyGets` | **R14** | shell 子进程 env == plan.Env |
| **S2** | `ShellWorkingDirectory_IsGameDirectory` | **R14** | cwd == 游戏目录 |
| **S3** | `EnvContainsWinePrefixForWineCommands` | **R14** | `WINEPREFIX` 就位（能跑 wine 命令的前提） |
| **S4** | `KillingShellLeavesNoOrphan` | R12、**R14** | 关标签后进程树无残留 |
| **S5** | `ExportScript_IsValidShell` | **R14** | `env.sh` 是合法 shell |

### 3.3 单元测试 → R 项映射

| 测试 | 覆盖的 R 项 | 覆盖点 |
|---|---|---|
| `GamesTests` B 站四组合 | **R3** | 缺 DLL / 空 DLL / 非国服 / 正常 各返回预期错误 |
| `GamesTests` `IsPluginEntry_*` | **R2** | GBK 编码 zip 条目 `64\xce\xbb/PCGameSDK.dll` 能匹配 |
| `GamesTests` 四渠道往返 | **R2** | `channel=14 cps=bilibili` 逐字节正确 |
| `EnvAndTriggerTests` 三态矩阵 | **R7** | `Override("")` ≠ `Remove` 被锁死 |
| `WineTests.Proton_ResolvesToDistBinWine_NotProtonScript` | **R10** | Proton 走 `dist/bin/wine`，**不走 `proton run`** |
| `WineTests.Managed_*` | **R9** | 托管 runner 解析路径 |
| `WineTests.System_ProbeVersionReadsStdout` | **R8** | 系统 wine 探测 |
| `ProcSnapshotsTests.*` | **R12**、R15 | `/proc` 匹配语义（精确 prefix、排除僵尸/自身） |
| `ScopedAndConfigTests.ConfigLoad_RecreatesDefaultOnCorruptJson` | 排障 §7 | 配置损坏自动恢复 |

### 3.4 验收自动化（`AcceptanceTests.cs`）

新增的 6 个用例把 R9/R12/R15 从"人工记录"变成"CI 可守"：

| 用例 | 覆盖 | 做法 |
|---|---|---|
| `R9_ManagedRunner_ResolvesAndProbesVersion` | **R9** | 真实 runner 路径 → `ProbeVersion` 非空且含 `wine`；`RunnerSource.Custom` 解析回同一路径，`Explain` 非空。本机无该 runner 时**跳过而非失败** |
| `R12_NoResidualWineClientsAfterExit` | **R12** | 合成游戏跑完整引擎 → 断言 `GetPrefixClients(prefix)` **为空**（真 `/proc`） |
| `R15_StaleClientsFromCrash_AreCleanedOnNextLaunch` | **R15** | 先造 3 个残留客户端并断言数到 3 → 跑引擎 → 断言启动成功且 3 个 pid 全部 dead/zombie |
| `Report_HasSevenPhasesAndFullChain` | 排障 | 报告含 `Prepare`/`AwaitReady`/`env_sources`；`report.Env.Count == plan.Env.Count`；`Runtime.Runner.Resolved == wine` |
| `SamePlan_YieldsIdenticalEnvForLaunchAndShell` | R14 前提 | 同一 plan 消费两次得到逐键一致的 env（"环境 shell 不失真"的机器证明） |
| `DryRun_ProducesFullPlanButLaunchesNothing` | — | dry-run 后 `GetPrefixClients` 为空、`BuildArgumentVector()` 与 `Env` 非空 |

**这组测试的价值**：它们**不伪造通过**——需要游戏本体的项直接在代码里
`return`（跳过），注释明确指向本文件的 ⏳ 标注。见
`AcceptanceTests.cs:13-14`。

### 3.5 S4 已修复（原"不稳定项"实为产品缺陷）

`S4_KillingShellLeavesNoOrphan` 曾间歇失败（断言 `shell 或子进程未被回收`，
耗时约 10 秒）。**根因不是测试抖动，而是 `Signal` 拒绝负 pid 导致进程组信号
静默失败**——详见 §2.5。

修复后连续 3 次运行全部通过，耗时从 10 秒降到 **98 毫秒**。**此条已关闭**，
不再是不稳定项。

---

## 四、未覆盖项与后续

### 4.1 必须人工 + 游戏本体才能推进的

| 项 | 阻塞条件 |
|---|---|
| R1 | 游戏本体；确认进登录界面 + `signal: WineDevice` |
| R2 | 游戏本体 + B 站账号 |
| R3 | 游戏本体（观察 UI 的下载按钮真实弹出） |
| R4 | 游戏本体 + 一个真实注入器（用户自备，见 `THIRD-PARTY-NOTICES.md` §三） |
| R5 | 游戏本体（UI 交互） |
| R6 | 游戏本体 |
| R7 | 游戏本体（在外部程序里 `echo $VAR`） |
| R8 | 游戏本体 |
| R10 | 游戏本体 + 本机 Steam Proton 库 |
| R11 | 依赖 R4/R5 先行；沙箱本身首期未实施 namespace 隔离 |
| R14 | 人工在终端里敲 `wine regedit` |

### 4.2 需要先补实现才能验的

| 项 | 前置工作 |
|---|---|
| R13 真机播放 | 装 `pacman -S vlc`（完整包）；建议把 Play/Pause 循环纳入自动化 |
| R15 完整闭环 | 需要真实反作弊的 `winedevice.exe` 残留模式 |
| R9 严格口径 | `docs/M0-RESULTS.md` §3.4 的托管组件下载器（M1） |

### 4.3 建议的下一步

1. **装 `vlc` 完整包**，把 R13 从 ⚠️ 推到 ✅（成本最低、收益最直接）；
2. **补托管组件下载器**（M1），让 R9 走严格口径，并消掉"借用"路径的耦合；
3. **R1/R2/R4 三项核心验收必须人工执行**——在有游戏本体的机器上跑完后，
   新建 `docs/acceptance-<新日期>.md` 记录（本文件不追加，保留当次快照语义）。
   > 验收记录**随仓库入库**（`.gitignore` 曾忽略 `docs/acceptance-*.md`，
   > 已移除该规则）。理由是验收证据必须可评审；写记录时不要粘贴
   > 含个人路径/账号的行，需要时用 `<home>` / `<user>` 占位。
4. **验收自动化继续扩展**：`AcceptanceTests.cs` 已经能守 R9/R12/R15，
   R13 的 Play/Pause 循环、R14 的 `wine regedit` 也可按同样方式做成合成用例。
