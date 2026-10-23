# LunarWard

Linux 下的《原神》/《绝区零》启动器，定位是 **Starward 在 Linux 的重实现**。

不是 AGL（an-anime-team 的 `anime-games-launcher`）的改造，也不是从零造轮子——
而是在 **复用 `Starward.Core` 全部 API/模型层** 的前提下，重写平台相关的启动、
运行时管理与 UI 视图。

> 当前状态：**已实现（M0–M7 完成，M8 移植完成待真机验证）**。
> 解决方案 0 warning / 0 error，194 个测试全通过（138 单元 + 31 安装器 + 25 集成），
> 主界面可运行（见下方截图自检命令）。真机验收 R1–R15 的完成情况见
> [acceptance-2026-10-23.md](docs/acceptance-2026-10-23.md)。

---

## 一分钟上手

```bash
# 1. 工具链
dotnet --version          # 需 10.x
# 可选：libvlc（动态背景）、hdiffpatch（差量更新）、7zip（解包安装）

# 2. 构建与测试
dotnet build LunarWard.slnx
dotnet test  LunarWard.slnx

# 3. 运行
dotnet run --project src/LunarWard.App

# 4. 无头自检（渲染一帧主窗口到 PNG 后退出，用于 CI）
dotnet run --project src/LunarWard.App -- --screenshot /tmp/lunarward.png

# 5. 打包（Arch）
cd packaging && makepkg -si
```

使用说明见 [USER-GUIDE.md](docs/USER-GUIDE.md)，出问题看
[TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md)。

---

## 仓库结构

```
lunarward/
├── LunarWard.slnx                 # .slnx（XML），不用传统 .sln
├── Directory.Build.props          # 统一 net10.0 / Nullable / LangVersion
├── Directory.Packages.props       # 中央包管理
├── src/
│   ├── LunarWard.Core/            # ← Starward.Core 移植（236 个 .cs）
│   ├── LunarWard.Platform/        # /proc 进程发现、flock、XDG、原子写、进程控制
│   ├── LunarWard.Wine/            # runner/prefix/dxvk 解析与安装、十层 env
│   ├── LunarWard.Launch/          # 七相位启动引擎（核心）、LaunchPlan、启动报告
│   ├── LunarWard.Games/           # 区服（config.ini）、B站 PCGameSDK、游戏适配
│   ├── LunarWard.Installer/       # ← Starward.RPC/GameInstall 移植（去 gRPC/Vanara）
│   └── LunarWard.App/             # Avalonia 12 视图层
├── tests/
│   ├── LunarWard.UnitTests/           # 138
│   ├── LunarWard.Installer.UnitTests/ # 31
│   └── LunarWard.IntegrationTests/    # 25（合成游戏 + 真 /proc）
├── packaging/                     # .desktop / PKGBUILD
└── docs/                          # 设计文档 + 用户手册 + 排障 + 验收记录
```

依赖方向严格单向：

```
App ──→ Games ──→ Launch ──→ Wine ──→ Platform
 │        │         │
 │        └─────────┴──→ Core
 └──→ Installer ──→ Core
```

---

## 文档索引

| 文档 | 内容 | 何时读 |
|---|---|---|
| [00-DECISIONS.md](docs/00-DECISIONS.md) | 已定决策与理由、明确否决的方案 | **先读这个** |
| [01-RESEARCH.md](docs/01-RESEARCH.md) | 参考项目实证结论、事实核查记录 | 实施前，避免重复调研 |
| [02-ARCHITECTURE.md](docs/02-ARCHITECTURE.md) | 技术选型（**Avalonia**）、仓库结构、依赖核验 | 搭建骨架前 |
| [03-LAUNCH-ENGINE.md](docs/03-LAUNCH-ENGINE.md) | 相位化启动引擎（**本项目最核心的技术**） | 写启动逻辑前 |
| [04-FEATURES.md](docs/04-FEATURES.md) | 外部程序、区服切换、运行时、env、UI | 逐模块实施时 |
| [05-ROADMAP.md](docs/05-ROADMAP.md) | 里程碑、测试、验收清单 | 排期时 |
| [06-OPEN-QUESTIONS.md](docs/06-OPEN-QUESTIONS.md) | 待确认问题（Q1–Q11，多数已按推荐执行） | 开工前 |
| [M0-RESULTS.md](docs/M0-RESULTS.md) | M0 五个验证项的**实测结论**与偏差记录 | 想知道"到底验证了什么" |
| [USER-GUIDE.md](docs/USER-GUIDE.md) | 用户手册 | 给用户 |
| [TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md) | 排障手册（退出码、signal、依赖缺失） | 启动失败时 |
| [acceptance-2026-10-23.md](docs/acceptance-2026-10-23.md) | 真机验收 R1–R15 的当次状态 | 交付评审 |

---

## 本项目真正做了什么

### 1. 相位化启动引擎（`LunarWard.Launch`）

**不变式：游戏必须是该 wine prefix 的第一个 wine 客户端。**
违反它游戏会无窗口、无日志、约 5 秒静默消失（`NtRaiseHardError 0x50000018`）。

七相位 `Prepare → Launch → AwaitImage → AwaitReady → PostLaunch → Running → Cleanup`，
把三个参考项目各自用 shell 脚本手工维持的约束上升为架构：

- `Prepare` 清空 prefix 并**确认清零**（否则 `PrefixNotClean`，退出码 4）
- `Launch` 从 **Unix 侧** `spawn wine`，绝不用 `CreateProcessW`
- `AwaitImage` 按 `/proc/<pid>/maps` 判定（**不能看 comm**：steam stub 会固定成 `steam.exe`）
- `AwaitReady` 三个信号：`WineDevice`（最强）/ `PlayerLog`（强）/ `Grace`（兜底，可调）
- `PostLaunch` **只在 `AwaitReady` 之后**拉起外部程序，且设计上**不提供"启动前运行程序"**

### 2. 通用外部程序模型（`Trigger.Auto` / `Trigger.Manual`）

后启动与补丁注入是**同一个概念**，靠 `Trigger` 区分：`Auto` = 伴随启动（补丁），
`Manual` = 手动（后启动），`Enabled=false` = 临时禁用。三态 env
（`Inherit` / `Override` / `Remove`）里 **`Override("")` ≠ `Remove`**——
对 wine 而言"存在但为空"与"不存在"行为不同（`winewayland` 需要 `DISPLAY=""`）。

### 3. 分层运行时 + 十层 env

`PerGame | Shared | Default | Custom` 四级作用域，`System | Managed | Proton | Custom`
四种来源，Proton **默认走 `dist/bin/wine`**（`proton run` 多一层进程会破坏不变式）。
env 十层合并，每层来源记入 `EnvSources`，UI 与启动报告都能显示"这个变量是哪来的"。

### 4. 区服切换（`config.ini`）

用 `Microsoft.Extensions.Configuration.Ini` + `[ConfigurationKeyName]`（不硬拼字符串），
**保留未知键**、原子写、与游戏原生格式**逐字节一致**。
B 站三条件（国服 + `PCGameSDK.dll` 存在且非空 + `config.ini` 正确）缺一即**硬拒绝**——
因为游戏在缺插件时会**静默回落官服**，这是最坑的失败模式。

### 5. 安装器（`Starward.RPC/GameInstall` 移植）

保留全部关键行为：`Parallel.ForEachAsync` 文件级并行、Polly 重试、
`SocketsHttpHandler`（HTTP/2+3 多连接、5 分钟连接池）、硬链接去重、MD5 跳过、断点续传。
去掉了 gRPC / protobuf / Vanara / SharpSevenZip / Snap.HPatch（后两者的 native 只有 Windows，
分别换成外部 `7z` 与 `hpatchz`），并修掉了上游"`is "NTFS"` 在 Linux 上永远为假
导致硬链接失效"的问题。

---

## 与设计的偏差（如实记录）

| 项 | 计划 | 实际 | 原因 |
|---|---|---|---|
| 解包 | `SharpSevenZip` | 外部 `7z` + BCL `ZipFile` | SharpSevenZip 的 native 只有 Windows |
| 差量补丁 | `Snap.HPatch` | 外部 `hpatchz` | 同上（nupkg 只有 `runtimes/win-x64/native`） |
| gRPC | 进度服务端流 | 进程内 `IProgress<T>` + 100ms ticker | 不需要跨进程 |
| 视频背景 | `LibVLCSharp` 真机播放 | 已实现，但本机 `libvlc_new` 返回 NULL（缺 VLC 插件包）→ **回退静态图** | Arch 上 `libvlc` 与 `vlc` 是分开的包 |
| 托管组件下载 | an-anime-team 索引 + git 同步 | ✅ 已实现，但**不引 git**：直接下载 `components.json` 与分组 JSON（`ComponentInstaller`） | 少一个外部依赖 |
| 沙箱 | namespace 隔离 | 只记录配置 | 首期不做 |

---

## 许可

**MIT**（与 Starward 一致）。第三方组件与义务见
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

不打包任何补丁二进制（`Hk4eTouch` 是 AGPL-3.0、`ZZZ-touch-unlocker` 无 LICENSE），
全部由用户自备，以此规避分发风险（`docs/00-DECISIONS.md` D11）。
