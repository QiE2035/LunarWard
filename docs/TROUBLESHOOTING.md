# 排障指南

结构以 `docs/03-LAUNCH-ENGINE.md` §十 的排障路径为骨架，补上可操作步骤。

**先做这一件事**：打开「诊断」看 `~/.local/share/lunarward/launch-report.json`。
90% 的问题看一眼 `exit_code` + `ready_signal` + `phases[]` 就能定位。

---

## 一、"点了启动，5 秒没窗口就退了"

这是本项目**最典型**的故障，特征是：**无窗口、无报错、约 5 秒静默消失**。
原因几乎总是同一条不变式被破坏：

> **游戏必须是该 wine prefix 的第一个 wine 客户端。**

破坏它的后果（三个参考项目交叉验证，见 `docs/01-RESEARCH.md` §1）：
`NtRaiseHardError 0x50000018` → 无窗口、无日志、约 5 秒静默消失。

按下面六步依次查，**不要跳步**。

### 第 1 步：prefix 是不是"热"的

```bash
pgrep -af 'wineserver|winedevice'
```

| 结果 | 含义 | 下一步 |
|---|---|---|
| **有输出** | 该 prefix（或别的 prefix）还有 wine 进程残留，是"热"的 | 见第 2 步清理 |
| 无输出 | prefix 是"冷"的，问题在别处 | 跳到第 3 步 |

要看是不是**你这个** prefix 的残留（系统上可能有别的 wine 程序）：

```bash
PREFIX="$HOME/.local/share/lunarward/prefixes/genshin-cn"
for pid in $(pgrep -f .); do
    grep -qa "WINEPREFIX=$PREFIX" "/proc/$pid/environ" 2>/dev/null \
        && echo "$pid $(cat /proc/$pid/comm)"
done
```

> `winedevice.exe` **会忽略 `SIGTERM`**，必须升级到 `SIGKILL`。
> 引擎的 `Prepare` 相位就是照这个顺序做的：`SIGTERM` → 等 0.5s → `SIGKILL` → `wineserver -k`。

手动清一个 prefix：

```bash
PREFIX="$HOME/.local/share/lunarward/prefixes/genshin-cn"
WINEPREFIX="$PREFIX" wineserver -k
sleep 1
pgrep -af "WINEPREFIX=$PREFIX" || echo "已清干净"
```

### 第 2 步：`launch-report.json` 的 `Prepare` 相位

```bash
jq '.phases[] | select(.name=="Prepare"), .error' \
   ~/.local/share/lunarward/launch-report.json
```

| 现象 | 诊断 |
|---|---|
| `error` 含 `PrefixNotClean` | 清理**没成功**。有进程拒绝退出（多半是 `winedevice.exe`），或清理超时（引擎给 3s） |
| `exit_code == 4` | 同上，这是 `PrefixNotClean` 的专属退出码 |
| `Prepare` 很快成功，但后面失败 | prefix 是干净的，问题在下游 → 第 3 步 |

`PrefixNotClean` 的完整消息形如：

```
wine prefix 里仍有 N 个客户端进程（pid: 12345, 12346），无法保证游戏是首个客户端。
```

拿这些 pid 直接 `kill -9`，或者重启会话（`loginctl terminate-user $USER` 太重，先试 `wineserver -k`）。

### 第 3 步：`AwaitImage` 是否命中

```bash
jq '.phases[] | select(.name=="AwaitImage")' ~/.local/share/lunarward/launch-report.json
jq '.game_pid, .ready_signal' ~/.local/share/lunarward/launch-report.json
```

| 现象 | 诊断 |
|---|---|
| `game_pid == null`，`AwaitImage` 跑满 120s | **没找到游戏镜像**。见下方三个原因 |
| `game_pid == null`，`AwaitImage` 很短就失败 | 游戏进程**直接退出了**，跳到第 5 步 |
| `game_pid` 有值 | 找到了，继续第 4 步 |

`AwaitImage` 找不到镜像的三个常见原因：

1. **游戏目录填错**：`YuanShen.exe` 不在那个目录。核对 §"版本与可执行文件名"（USER-GUIDE §2.2）。
2. **镜像名不匹配**：绝区零两个版本 exe 同名，但数据目录不同；确认选对了 `edition`。
3. **看错了 prefix**：`game_pid` 是按 `/proc/<pid>/maps` 在**本 prefix 的客户端**里找的。
   若游戏跑在别的 prefix（比如你之前用别的启动器启动过），这里永远找不到。
   核对 `launch-report.json` 的 `prefix` 字段。

手动核对（游戏启动中）：

```bash
PREFIX="$HOME/.local/share/lunarward/prefixes/genshin-cn"
for pid in $(pgrep -f .); do
    grep -qa "WINEPREFIX=$PREFIX" "/proc/$pid/environ" 2>/dev/null || continue
    grep -qi 'YuanShen.exe' "/proc/$pid/maps" 2>/dev/null && echo "命中 pid=$pid"
done
```

> **为什么不能只看进程名**：runner 的 steam stub 会把 unix `comm` 固定成 `steam.exe`，
> 真正的游戏是它 fork 出的子进程。**只有镜像映射（`maps`）是可靠的"游戏已加载"信号。**

### 第 4 步：`AwaitReady` 的 `signal` 是什么

```bash
jq '.ready_signal, (.phases[] | select(.name=="AwaitReady"))' \
   ~/.local/share/lunarward/launch-report.json
```

| signal | 含义 | 可靠性 | 接下来 |
|---|---|---|---|
| `WineDevice` | 保护驱动宿主 `winedevice.exe` 已启动，`ZwLoadDriver` 成功 | **最强** | 保护期已过，问题**不在**这里 → 第 5 步 |
| `PlayerLog` | Unity 已开始初始化（`Player.log` 的 mtime 已更新） | 强 | 同上 → 第 5 步 |
| `Grace` **且很快返回** | **超时兜底**，可能过早 | **最弱** | **保护期可能更长 → 调大 grace，见 §3** |

**这是调 grace 的唯一正确判据**：`Grace` + 耗时远小于 `ready_grace_ms` 的设定值，
说明循环没等到更强信号就撞了 deadline，此时第二个客户端（`Trigger.Auto` 的程序）
会在保护期**内部**登场，游戏自杀。

### 第 5 步：`game.log` 有没有 wine 报错

```bash
tail -100 ~/.cache/lunarward/logs/game.log
```

| 关键词 | 含义 |
|---|---|
| `NtRaiseHardError` / `0x50000018` | **保护期被破坏**，回到第 1 步 |
| `HoYoProtect: c0000355` | 反作弊驱动加载失败，多半是同一原因 |
| `wine: cannot find` / `No such file` | 路径或 DLL 缺失 |
| `err:module:import_dll` | 缺 DLL（常见于 DXVK 装错 arch） |
| 完全空 | 游戏还没来得及输出就死了 → 肯定是保护期问题 |

> `game.log` 上限 **8 MiB**（防 wine 灌爆磁盘）。日志被截断时只能看到尾部。

### 第 6 步：`driverError.log`

```bash
cat "<游戏目录>/driverError.log"
```

有 `initDriver Failed` → 驱动初始化失败。常见原因：DXVK 版本与 wine 不匹配、
`WINEDLLOVERRIDES` 与注册表 override 冲突（环境变量优先级更高，会覆盖注册表）。

---

## 二、退出码表

| 码 | 含义 | 对应 `LaunchError` | 你的动作 |
|---|---|---|---|
| **0** | 成功 | — | 无需处理 |
| **1** | 参数 / 预检失败 | `Preflight`、`GameNotInstalled`、`RuntimeNotFound`、`ChannelPrerequisiteMissing` | 看 `error` 字段，按提示改配置 |
| **2** | 游戏启动期退出（**多半是保护期被破坏**） | `GameExitedDuringStartup` | 走 §一 的六步 |
| **3** | 外部程序失败（**不终止游戏**，仅记录） | `InjectorBinaryMissing`、`ExternalProgramFailed` | 游戏正常，只需修那个外部程序 |
| **4** | prefix 没清干净 | `PrefixNotClean` | 走 §一 的第 1、2 步 |

（沿用 `ZZZ-touch-unlocker` 的约定，便于和它的排障文档对照。）

### 2.1 退出码 1 的四种细分

| `error` 里的类型 | 具体原因 | 修法 |
|---|---|---|
| `Preflight` | 参数错误、目录不存在 | 看消息里的路径 |
| `GameNotInstalled` | 目录里没有对应 exe | 重设游戏目录（USER-GUIDE §2） |
| `RuntimeNotFound` | runner 解析失败（未安装 / 路径没了） | 设置 → 重新扫描 → 换一个 |
| `ChannelPrerequisiteMissing` | B 站三条件不满足 | 走 §5 |

### 2.2 退出码 3 的处理

游戏**照常运行**，只是某个外部程序没起来。
`launch-report.json` 的 `external_programs[]` 里能看到具体是哪个：

```bash
jq '.external_programs[] | select(.error != null)' \
   ~/.local/share/lunarward/launch-report.json
```

常见原因：文件被删/改名（`InjectorBinaryMissing`）、参数写错、该程序自己崩了。
**每个外部程序独立拉起，失败不阻塞其他程序，也不终止游戏。**

---

## 三、signal 语义表与"怎么调大 grace"

### 3.1 三个信号

| 信号 | 含义 | 可靠性 | 何时出现 |
|---|---|---|---|
| **`WineDevice`** | 保护驱动宿主 `winedevice.exe` 已启动，`ZwLoadDriver` 成功 | **最强** | 保护期真正过去时 |
| **`PlayerLog`** | Unity 已开始初始化（`Player.log` 的 mtime ≥ launch epoch） | 强 | 稍早于 WineDevice |
| **`Grace`** | 超时兜底 | **最弱，可能过早** | 到 deadline 时前两个都没出现 |

引擎的判定顺序（每 100ms 一轮）：

```
① 本 prefix 出现 winedevice.exe        → WineDevice, break   ← 最强，优先
② Player.log 的 mtime >= launchEpoch  → PlayerLog,  break
③ now >= launchEpoch + graceMs        → Grace,      break   ← 兜底
```

### 3.2 什么时候该调大 grace

**唯一判据**：`ready_signal == "Grace"` **且** `AwaitReady` 的 `duration_ms`
接近（而不是远大于）设定值，且游戏随后闪退。

```bash
jq '{signal: .ready_signal, await_ready: (.phases[] | select(.name=="AwaitReady") | .duration_ms)}' \
   ~/.local/share/lunarward/launch-report.json
```

| 输出 | 结论 |
|---|---|
| `signal: Grace`，`duration_ms ≈ 6000`，游戏闪退 | **保护期比 6 秒长** → 调大到 8000 或 10000 |
| `signal: WineDevice`，游戏仍闪退 | **保护期已过**，问题不在 grace → 走 §一 第 5、6 步 |
| `signal: PlayerLog`，游戏闪退 | 同上，`PlayerLog` 已足够强 |

### 3.3 怎么调

| 方式 | 步骤 |
|---|---|
| **UI（推荐）** | 设置 → 「就绪保护期 (ms)」（`NumericUpDown`，范围 0–600000）→ 改值 → 保存 |
| 配置文件 | `~/.config/lunarward/config.json` → `profiles.<id>.ready_grace_ms` |

```jsonc
{
  "profiles": {
    "genshin-cn": {
      "ready_grace_ms": 8000      // 默认 6000；范围 0–600000
    }
  }
}
```

对应的领域模型字段是 `ProfileConfig.ReadyGraceMs`（JSON 名 `ready_grace_ms`），
由 `LaunchPlanFactory` 转成 `LaunchPlan.ReadyGrace`（`Math.Max(0, ReadyGraceMs)` 毫秒）。

**为什么默认是 6000**：来自实测。但 **grace 长度会随游戏版本 / wine 版本漂移**
（`docs/03-LAUNCH-ENGINE.md` §3.4），所以它必须可配、必须在 UI 里。

**不要一次调太大**：grace 越大，`Trigger.Auto` 的外部程序登场越晚，用户等得越久；
而 `Grace` 兜底本来就可能已经越过保护期。合适值 = 让信号变成 `WineDevice` 的最小值 + 余量。

---

## 四、B 站静默回落官服

### 4.1 症状

**你以为切到了 B 站，实际进了官服，全程无任何提示。**
更糟的是：登录后账号绑到了错误的服务器，甚至可能在错误的区服上充值。

这是本项目**最危险的失败模式**，所以设计上选了**硬拒绝**而不是静默回落
（`docs/04-FEATURES.md` §2.5）。

### 4.2 怎么识别

| 检查点 | 正常（B 站生效） | 回落官服 |
|---|---|---|
| 启动器行为 | 缺 DLL 时**拒绝启动** | 若无硬拒绝就会静默进入 |
| `config.ini` 的 `channel` | `14` | `1` |
| `config.ini` 的 `cps` | `bilibili` | `mihoyo` |
| 登录界面 | B 站账号登录 | 米哈游账号登录 |

直接读配置文件：

```bash
grep -E '^(channel|cps|sub_channel)=' "<游戏目录>/config.ini"
```

判决：

```
channel=14  cps=bilibili  sub_channel=0   → ✅ B 站
channel=1   cps=mihoyo    sub_channel=0   → ❌ 官服（被回落了）
```

### 4.3 三个条件与逐条核对

| # | 条件 | 核对命令 | 失败表现 |
|---|---|---|---|
| 1 | 国服（`YuanShen.exe`） | `ls "<游戏目录>/YuanShen.exe"` | **硬拒绝**："B 站渠道仅国服可用" |
| 2 | `PCGameSDK.dll` 存在**且非空** | `ls -l "<游戏目录>/YuanShen_Data/Plugins/PCGameSDK.dll"` | **硬拒绝** + 「下载插件」按钮 |
| 3 | `config.ini` 写成 B 站值 | 见 §4.2 | 不算失败，`Prepare` 会重写 |

**条件 2 特别容易踩**：文件**存在但长度为 0** 也算失败（`Length > 0` 才通过）。
半截下载、中断的复制都会留下 0 字节文件。

```bash
f="<游戏目录>/YuanShen_Data/Plugins/PCGameSDK.dll"
[ -s "$f" ] && echo "OK: $(stat -c%s "$f") bytes" || echo "缺失或为空 — 会硬拒绝"
```

### 4.4 处理

1. 点 UI 的「下载插件」（缓存到 `~/.cache/lunarward/bilibili/`，`.part` → rename，永远完整）。
2. 删掉 0 字节的 `PCGameSDK.dll` 再重下一次。
3. 确认是**国服**（`GenshinImpact.exe` 是国际服，永远不支持 B 站）。
4. 绝区零**没有** B 站渠道，UI 会隐藏该选项——如果你是 ZZZ 用户看到这条，说明配错了游戏。

### 4.5 报错长什么样

```
渠道前置条件不满足：Bilibili 渠道需要 YuanShen_Data/Plugins/PCGameSDK.dll，当前缺失。
```

`exit_code == 1`，`launch-report.json` 的 `error` 字段同样内容。

---

## 五、可选依赖缺失

### 5.1 libvlc 缺失 → 回退静态图

**症状**：视频背景不播，显示静态图。**不会崩溃**（这是设计）。

**诊断**：

```bash
# Arch: libvlc 与 VLC 插件包是分开的！只装 libvlc 时 libvlc_new() 返回 NULL
pacman -Q libvlc vlc
ls /usr/lib/libvlc.so*
```

**这是 Arch 上最容易踩的坑**：`libvlc 3.0.24` 已装，`/usr/lib/libvlc.so.5` 也存在，
但 `libvlc_new()` 返回 `NULL`——因为**缺 codec 插件**（插件在 `vlc` 包里）。
此时 `BackgroundService.IsVlcAvailable == false`，按设计回退静态图。

**修**：

| 发行版 | 命令 |
|---|---|
| Arch / CachyOS | `sudo pacman -S vlc`（**完整 vlc，不是只装 libvlc**） |
| Debian / Ubuntu | `sudo apt install libvlc5` |
| Fedora | `sudo dnf install vlc-libs` |

验证：

```bash
vlc --version | head -1        # 有 vlc 命令说明插件包也在了
```

重开启动器，`IsVlcAvailable` 会重新探测（探测结果有缓存，但**每次进程启动重探**）。

**如果不想装**：设置 → 关闭 `video_background`。好处是**同时跳过视频下载**，省带宽。

### 5.2 hpatchz 缺失 → "Patch file failed"

**症状**：更新/校验时日志出现：

```
Patch file failed.
Source -> Target: <old> -> <new>
```

或 `game.log` / 安装日志里：

```
hpatchz 不可用：无法应用差量补丁 <diff>。请安装 hdiffpatch（Arch: pacman -S hdiffpatch；
Debian: apt install hdiffpatch），或设置环境变量 LUNARWARD_HPATCHZ 指向 hpatchz。
```

**原因**：`Snap.HPatch` 这个 NuGet 包的 nupkg **只有 `runtimes/win-x64/native`**，
Linux 不可用（见 `docs/M0-RESULTS.md` 的偏差记录）。所以改用**外部 `hpatchz` CLI**。

**修**：

| 发行版 | 命令 |
|---|---|
| Arch / CachyOS | `sudo pacman -S hdiffpatch` |
| Debian / Ubuntu | `sudo apt install hdiffpatch` |

**自定义路径**（`hdiffpatch` 不在 `PATH`，或你想固定某个版本）：

```bash
export LUNARWARD_HPATCHZ=/opt/hdiffpatch/bin/hpatchz
```

启动器会依次找：`LUNARWARD_HPATCHZ` → `hpatchz` → `hpatch`。

**没有它也能更新**：该文件走全量下载，只是慢、费流量。

### 5.3 7z 缺失 → `InstallerCapabilityException`

**症状**：安装/解包时抛异常：

```
Cannot extract '<xxx>.7z': no 7z executable found in PATH
(set LUNARWARD_7Z, or install p7zip / 7zz).
Only plain .zip archives can be extracted without it.
```

**原因**：`SharpSevenZip` 的 native 只有 Windows（见 `docs/M0-RESULTS.md`），
所以改用**外部 7z CLI**。查找顺序：`7zz` → `7z` → `7za`。

**修**：

| 发行版 | 命令 |
|---|---|
| Arch / CachyOS | `sudo pacman -S 7zip`（提供 `/usr/bin/7z` 与 `7za`） |
| Debian / Ubuntu | `sudo apt install 7zip` |

**自定义路径**：

```bash
export LUNARWARD_7Z=/usr/local/bin/7zz
```

**没有它也能装的唯一情况**：安装包是**普通 `.zip`**。此时回退 BCL 的
`ZipFile.ExtractToDirectory`，功能等价（但**不支持分卷** `.7z.001` 那类）。

验证：

```bash
for c in 7zz 7z 7za; do printf '%s: ' "$c"; command -v "$c" || echo MISSING; done
```

---

## 六、运行时与 prefix 问题

### 6.1 `RuntimeNotFound`

```bash
jq '.runtime.runner, .runtime.explain' ~/.local/share/lunarward/launch-report.json
```

| 原因 | 核对 | 修 |
|---|---|---|
| 托管组件没装 | `ls ~/.local/share/lunarward/runners/<组件>/bin/wine` | 换来源，或装组件 |
| **借用**的目录被对方删了 | `ls ~/.local/share/sleepy-launcher/runners/` | 重新扫描，或改装自己的 |
| Proton 路径变了 | `ls "<库>/steamapps/common/<名字>/dist/bin/wine"` | 重新扫描 |
| 自定义路径不可执行 | `test -x <路径>` | `chmod +x` |
| 系统 wine 没有 | `which wine` | 装 wine，或换托管组件 |

**只读借用**的含义：LunarWard 注册的是 `Custom` 条目，**从不**写入那个目录。
对方更新/删除后，这里立即失效——这是预期行为，不是 bug。

### 6.1b 创建 prefix 卡住 / 超时（wine-mono 提示）

**症状**：第一次启动时长时间没反应（约 2 分半），最后报"创建 wine prefix 超时"。

**原因**：`wineboot -u` 在冷 prefix 上会弹「是否下载 wine-mono」的对话框并
**阻塞等待输入**。启动器是无窗口运行的，于是只能等到超时。

**实测**（本机，wine-10.0 CachyOS）：

| 场景 | 冷 prefix 耗时 |
|---|---|
| 默认 | **2 分 32 秒**（卡在 mono 提示） |
| 禁 mono/gecko | **8 秒** |

**本项目已默认禁掉**：`PrefixManager.InitializeAsync` 会合并
`WINEDLLOVERRIDES=mscoree,mshtml=`。原神/绝区零是原生 PE 游戏，
不需要 .NET Framework 或 MSHTML，禁掉没有副作用。

**如果你自己手动建 prefix**，也请带上这个变量，否则会卡同样的地方：

```bash
WINEPREFIX=~/.local/share/lunarward/prefixes/my-prefix \
  WINEDLLOVERRIDES="mscoree,mshtml=" \
  wine wineboot -u
```

**若仍然超时**，按顺序查：

| 检查 | 命令 | 说明 |
|---|---|---|
| 磁盘满了 | `df -h "<prefix 的父目录>"` | wineboot 要写 ~300 MB |
| 权限 | `ls -ld "<prefix>"` | 必须当前用户可写 |
| wineserver 残留 | `pgrep -af wineserver` | 有残留先 `WINEPREFIX=<prefix> wineserver -k` |
| 卡在别处 | 前台跑一次看输出 | 见上面的命令，去掉重定向 |

> **注意**：超时后本项目会**删掉半成品 prefix** 再报错。
> 所以你看到的不会是一个"半死的 prefix"，而是干净的重试机会。

### 6.2 DXVK 装错

| 症状 | 原因 | 修 |
|---|---|---|
| `err:module:import_dll` 频繁 | 32 位 DXVK 塞进 64 位 prefix（或反之） | 重装对应 arch 的 DXVK |
| `driverError.log` 有 `initDriver Failed` | DXVK 版本与 wine 不匹配 | 换 DXVK 版本 |
| override 不生效 | 注册表写了 `native`，但 `WINEDLLOVERRIDES` 覆盖了它 | **环境变量优先级更高**，二者取一并保持一致 |

核对 override 状态（本项目沿用 an-anime-team 的做法，读文本即可）：

```bash
grep -A2 'DllOverrides' "<prefix>/user.reg" | grep -E 'dxgi|d3d11|native'
```

### 6.3 环境变量没生效

看 `EnvSources`，它记录**每个键最终来自哪一层**：

```bash
jq '.env.DXVK_HUD, .env_sources.DXVK_HUD' ~/.local/share/lunarward/launch-report.json
```

十层优先级见 USER-GUIDE §6.3，后者覆盖前者。
若你设的值没出现，说明有更靠后的层覆盖了它（最常见：第 9 层外部程序的三态 env）。

**`Override("")` vs `Remove` 的坑**：输入框清空默认按 `Remove` 处理。
要"存在但为空"必须勾「设为空值」。`winewayland` 需要 `DISPLAY=""`，
删掉 `DISPLAY` 会让 wine 退回 X11——症状是 Wayland 下不起作用。

---

## 七、配置损坏恢复

### 7.1 自动行为

`config.json` 解析失败时，启动器**自动**：

1. 备份为 `~/.config/lunarward/config.json.bad-<unix-ts>`
2. **自动重建**默认配置并写回
3. 在 UI 顶部给出警告

所以配置写坏了**不会让你卡在无法启动的状态**。

```bash
ls -la ~/.config/lunarward/ | grep bad
```

### 7.2 手动恢复

```bash
# 1) 看看坏在哪
cd ~/.config/lunarward
ls config.json.bad-*

# 2) 备份当前（可能是刚重建的默认值）
cp --reflink=auto config.json config.json.now

# 3) 从备份里挑回想要的部分（profiles / runtimes）
jq '.profiles' config.json.bad-1750000000

# 4) 确认 JSON 合法再放回去
jq . config.json.bad-1750000000 > /tmp/checked.json && echo OK
```

> `jq` 能否解析是很好的健全性检查。JSONC 注释和尾逗号本项目是**允许**的
> （`ReadCommentHandling.Skip` + `AllowTrailingCommas`），但 `jq` 不接受——用 `jq` 校验前先去掉。

### 7.3 什么情况会损坏

| 原因 | 说明 |
|---|---|
| 手改时漏了引号/逗号 | 最常见 |
| 磁盘写满 | 原子写保证原文件不变，但 `.tmp` 可能残留 |
| 并发写（两个实例同时开） | 后写的赢；**不要同时开两个启动器** |
| 断电 | 原子写（`.tmp` → `fsync` → `rename`）已尽可能防护 |

### 7.4 原子写与未知字段

| 机制 | 保证 |
|---|---|
| **原子写** | 写 `config.json.tmp` → `fsync` → `rename`。**任何时刻**读到的都是完整文件 |
| **未知字段保留** | `[JsonExtensionData]` 承接。旧版本启动器**不会**抹掉新版本写的字段 |
| **`schema_version`** | 当前 `1`。若文件里的版本更高，启动器会警告"未知字段已保留"而不是拒绝启动 |

若发现 `config.json.tmp` 残留，说明上次写在 rename 前中断了，**可以安全删除**：

```bash
rm -f ~/.config/lunarward/config.json.tmp
```

---

## 八、环境 shell 问题

### 8.1 PTY 创建失败

**症状**：终端窗口空白，提示"PTY 创建失败"或长时间无输出。

| 检查 | 命令 |
|---|---|
| 有 `SHELL` 环境变量 | `echo $SHELL` |
| shell 可执行 | `test -x "$SHELL" && echo OK` |
| `openpty` 可用 | `python3 -c "import pty; pty.openpty(); print('OK')"` |

**退路（设计上就有）**：点右上角「导出 env.sh」，拿到外部终端用：

```bash
source ~/.cache/lunarward/env.sh
```

导出的脚本包含 `cd <游戏目录>` + 全量 `export K=V`，**与真实启动的环境逐字节同源**
（同一个 `LaunchPlan.Env`）。

### 8.2 环境不一致的怀疑

**不可能不一致**：环境 shell 与真实启动**共用同一个 `LaunchPlan` 对象**
（`docs/04-FEATURES.md` §6.2 的硬要求）。若怀疑不一致，直接对比：

```bash
jq '.env' ~/.local/share/lunarward/launch-report.json > /tmp/report-env.json
diff <(jq -S . /tmp/report-env.json) <(jq -S . /tmp/shell-env.json)
```

---

## 九、外部程序问题

### 9.1 补丁没生效

| 检查 | 怎么看 |
|---|---|
| 是否启用 | 外部程序面板的**启用**勾选框 |
| 触发方式对不对 | 补丁必须是 **伴随启动**（`Auto`），不是手动 |
| 是否在 `AwaitReady` 之后 | `launch-report.json` 的 `external_programs[]` → `started_after` 应为 `AwaitReady` |
| 有没有起来 | 同上的 `pid` / `exit_code` / `error` |
| 它自己的日志 | **每个注入器的日志位置不同**，看它自己的文档 |

```bash
jq '.external_programs' ~/.local/share/lunarward/launch-report.json
```

### 9.2 游戏闪退 + 配了补丁

**先怀疑保护期**（`docs/00-DECISIONS.md` D10）：

1. 把补丁的「启用」**取消勾选**，再启动一次。
2. 若游戏正常 → 确认是第二个客户端的问题：
   - 查 `ready_signal`（§三）。若是 `Grace`，**调大 grace**。
   - 该补丁若需要更晚登场，这是它自身的限制，本项目不感知补丁内部逻辑。
3. 若禁用后仍然闪退 → 问题不在补丁，走 §一。

### 9.3 `Auto` + `WindowsExe` 的警告是什么意思

UI 会显示：

```
该程序会在游戏就绪后被拉起，成为该 wine prefix 的第二个客户端。
若它需要破坏游戏的保护期初始化，可能导致游戏闪退。
```

**这只是一个提示，不阻止你**（`docs/04-FEATURES.md` §1.6）。
它是事实陈述：那个程序确实会连上同一个 `wineserver`，成为第二个客户端。
能不能接受取决于那个程序做什么。

### 9.4 参数里的关键字没替换

| 关键字 | 可用相位 |
|---|---|
| `%pid%` | **仅 `Running` 之后**。`Start` 时用 `%pid%` 会拿到空值 |
| 其他（`%build%` `%prefix%` `%game%` `%launcher%` `%temp%` `%exe%` `%channel%` `%runtime%`） | 全部相位 |

未知关键字**原样保留**（不替换成空串），所以看到 `%foo%` 出现在日志里是预期行为。

---

## 十、需要附上的信息

报 bug 时请附：

```bash
# 1) 启动报告（最重要）
cat ~/.local/share/lunarward/launch-report.json

# 2) 游戏日志尾部
tail -100 ~/.cache/lunarward/logs/game.log

# 3) 启动器日志
tail -100 ~/.cache/lunarward/logs/lunarward-$(date +%F).log

# 4) 环境
dotnet --version; echo "$XDG_SESSION_TYPE / $XDG_CURRENT_DESKTOP"
pacman -Q dotnet-runtime libvlc vlc 7zip hdiffpatch 2>/dev/null   # 或 apt list --installed

# 5) wine 版本
wine --version
```

**不要**附 `config.json` 原文（可能含路径与令牌）。需要时用 `jq` 脱敏：

```bash
jq 'del(.profiles[].path)' ~/.config/lunarward/config.json
```
