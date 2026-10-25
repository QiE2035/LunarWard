# LunarWard

[![CI](https://github.com/QiE2035/LunarWard/actions/workflows/ci.yml/badge.svg)](https://github.com/QiE2035/LunarWard/actions/workflows/ci.yml)

Linux 下的《原神》/《绝区零》启动器，定位是 **Starward 在 Linux 的重实现**。

不是 AGL（an-anime-team 的 `anime-games-launcher`）的改造，也不是从零造轮子——
而是在 **复用 `Starward.Core` 全部 API/模型层** 的前提下，重写平台相关的启动、
运行时管理与 UI 视图。

> 当前状态：**已实现（M0–M7 完成，M8 移植完成并做过真机下载烟测，M9 全部接入）**。
> 解决方案 0 warning / 0 error，584 个测试全通过（526 单元 + 31 安装器 + 27 集成），
> UI 已按 Starward 逐页复刻（见下节）。真机验收 R1–R15 的完成情况见
> [acceptance-2026-10-23.md](docs/acceptance-2026-10-23.md)。

---

## UI：Starward 的复刻，不是重新设计

界面按 Starward（WinUI 3）**逐页复刻**：同样的 1200×676 固定窗口、48px 标题栏、
56px 左栏导航、公告卡（Banner + 活动/公告/资讯）、右下胶囊启动按钮、全屏游戏选择层、
260px 二级导航设置页。LunarWard 独有的能力（wine 运行时、十层 env、外部程序、
诊断、环境 shell）**追加在 Starward 已有页面里**，不新造导航。

| 页面 | 截图 |
|---|---|
| 启动器 | ![launcher](artifacts/screens/sw-launcher.png) |
| 游戏设置（含 LunarWard 追加区） | ![game setting](artifacts/screens/sw-game-setting.png) |
| 设置（260px 二级导航 + 9 个子页） | ![settings](artifacts/screens/sw-setting.png) |
| 抽卡记录 | ![gacha](artifacts/screens/sw-gacha.png) |
| 游戏截图 | ![screenshot](artifacts/screens/sw-screenshot.png) |
| 工具箱（便笺 / 账号） | ![toolbox](artifacts/screens/sw-toolbox.png) |

像素与交互契约、已知偏差（Acrylic 真模糊、圆角窗口、动画）
见 [07-UI-REPLICA.md](docs/07-UI-REPLICA.md)。

---

## 一分钟上手

```bash
# 0. 全新 clone 即可直接构建与测试（不需要 submodule、不需要手动放文件）
git clone <repo> && cd lunarward

# 1. 工具链
dotnet --version          # 需 10.x
# 可选：libvlc（动态背景）、hdiffpatch（差量更新）、7zip（解包安装）

# 2. 构建与测试
dotnet build LunarWard.slnx
dotnet test  LunarWard.slnx

# 3. 运行
dotnet run --project src/LunarWard.App

# 4. 无头自检（渲染一帧 Starward 界面到 PNG 后退出，用于 CI）
dotnet run --project src/LunarWard.App -- --screenshot /tmp/lunarward.png
#    逐页出图：--page launcher|game-setting|screenshot|gacha|toolbox|self-query|genshin-beyond|settings
#    工具箱演示数据：LUNARWARD_TOOLBOX_DEMO=1（不联网、不写用户数据），
#                   LUNARWARD_TOOLBOX_GAME=starrail 可切到崩铁菜单，配合 --tab <序号> 选中子页
#    其它自检开关：--open-selector（打开游戏选择层）、--selector-hover（卡片悬停遮罩）、
#                 --selector-flyout（弹出区服列表，Flyout 是独立 Popup，不进主窗口截图）、
#                 --game-icons（展开游戏图标条）、
#                 --open-daily-note（打开每日便笺 Flyout）、
#                 --install-dialog（打开安装路径对话框）、--install-progress（显示安装进度弹层）、
#                 --install-update（按钮切到「更新游戏」；与 --install-progress 同用时用更新语义）、
#                 --install-hover（安装进度自检时假装鼠标悬停在按钮上）、
#                 --predownload <idle|downloading|hover|finished>（摆出预下载按钮/信息卡/完成后隐藏）、
#                 --window image-viewer（截图片查看器；可加 --image-viewer-gallery / --image-viewer-info）、
#                 --screenshot-folder-dialog（截「管理截图文件夹」对话框）、
#                 --screenshot-seed / --screenshot-hover（截图页样例数据 / 首块磁贴悬停放大）、
#                 --gacha-seed zzz|genshin（抽卡页样例记录，便于截 ZZZ 统计卡）、
#                 --gacha-delete-dialog（抽卡页「删除指定时间段」对话框，自动用 ZZZ 样例数据）、
#                 --gacha-materials（抽卡页右侧材料统计 Pane，自动用原神样例数据；会联网同步 wiki 图标）、
#                 --tray-test（试建系统托盘并打印 TRAY_OK / TRAY_FAIL，再模拟一次关闭，窗口应隐藏）、
#                 --scan-games（扫游戏库含 Steam 的 steamapps/common，打印 SCAN_GAME/SCAN_TOTAL 后退出）、
#                 --start-hidden（启动即藏到托盘，开机自启用）、--quit-after <ms>（自检自动退出）、
#                 --gacha-export <path>（把样例记录按 UIGF v3.0 导出到该路径）、
#                 --gacha-import <path>（读回 UIGF v3.0 / JSON 并合并，做往返自检）、
#                 --gacha-excel <path>（把样例记录按 Starward 的 Excel 模板导出到该路径）、
#                 --uigf-window（UIGF v4.2 导出/导入窗口，自动用原神样例存档；加 --uigf-import 切到导入页）、
#                 自检一律使用临时库 $TMP/lunarward-capture/capture.db，不写用户的 LunarWardDatabase.db、
#
# UI 端真机安装：`--screenshot <png> --install-real <目录> [--install-info-popup] --wait 8000`
  会走「安装对话框确认后」同一条 `BeginInstallAsync` 起真实下载，并把进度绑到「开始游戏」
  按钮上再截图；加 `--install-info-popup` 同时展开「下载中 / 下载 / 安装 / 校验」卡片
  （实测 `0.39/119.50 GB`、23.97 MB/s、按钮变「暂停」）。
真机下载自检：`lunarward install-smoke <目录> [--biz hk4e_cn] [--seconds 20]` 会启动一次
  真实安装任务（官方 CDN），跑指定秒数后暂停，打印 `state/downloaded/total/files`；同一目录再跑一次
  即可验证续传（实测 15s 下载 474 MB，6s 续传到 687 MB，未从头重下）。
逐页冒烟：scripts/smoke-screenshots.sh 会在隔离的 HOME/XDG 目录里渲染 8 个页面 + 9 个设置
  子页（共 17 张）到 artifacts/smoke/，任何一页抛未处理异常都会以非零码退出；可以只跑指定
  页面 `scripts/smoke-screenshots.sh gacha settings:files`。
#                 --predownload-dialog（预下载确认框；加 --predownload-dialog-unavailable 看不可用态）、
| `--dx12-intro` | 打开 DX12「效果详情」引导弹窗并截图 |
#                 --launcher-setting-dialog + --settings-section basic|arguments|background|packages|network
#                 --launcher-setting-dialog + --settings-section basic --uninstall-warning（卸载确认区）
#                     （截启动器「游戏设置」弹窗；network 亮网络驱动器警告）、
#                 --auth-login（右上角通行证授权登录按钮切到可见 + 失败态，并单独出 Flyout 图 *.flyout.png）、
#                 --page self-query（账号记录页）、--selfquery-select（自动选中第一行类型）、
#                 --selfquery-url（打开「输入 URL」对话框并单独出图 *.dialog.png）、
#                 --beyond-seed（千星奇域颂愿自检时写入样例记录，出统计卡）、
#                 --beyond-pane（展开材料统计 Pane）、--beyond-second-tab（切到第二个页签）、
#                 --tab <n>（工具箱第 n 个战绩菜单）、--lang en-US、
#                 --settings-section <about|general|download|files|screenshot|hotkey|gamepad|advanced|toolbox>、
#                 --window playtime（打开游戏时长统计弹窗再截图）、
#                 --profile <id>、--scroll-y <px>（截图前滚到长页下半部分）、--wait <ms>

# 5. 打包（Arch）
cd packaging && makepkg -si
```

直接启动游戏（不开窗口，给桌面快捷方式用，对应上游 `starward startgame --biz`）：

```bash
lunarward startgame --biz hk4e_cn      # 原神国服；hkrpg_cn / nap_global 等同理
lunarward "lunarward://startgame/hk4e_cn"   # 同一入口的 URL 协议形式（桌面文件的 %U 会传进来）
lunarward --version / --help            # 版本号 / 命令帮助（不启动窗口）
lunarward --start-hidden                # 开机自启：直接藏到系统托盘（需先在设置里选「最小化到系统托盘」）
lunarward playtime --biz hk4e_cn --pid 1234           # 盯住外部启动的游戏进程，退出后补记时长
lunarward "lunarward://playtime?biz=hk4e_cn&pid=1234" # 同上，URL 形式（给 hoYoPlay / 外部启动器调用）
```

`playtime` 与上游一致：用进程开始/退出时间补一条 `PlayTimeStats`，同一个 pid 只允许一个 watcher
（`$XDG_CACHE_HOME/lunarward/runtime/playtime-<pid>.lock`），重复调用直接退出。

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
│   └── LunarWard.App/             # Avalonia 12 视图层（Starward UI 复刻）
├── tests/
│   ├── LunarWard.UnitTests/           # 276
│   ├── LunarWard.Installer.UnitTests/ # 31
│   └── LunarWard.IntegrationTests/    # 27（合成游戏 + 真 /proc + 真 wine）
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

对照表可以用脚本自检：`scripts/check-parity.py --upstream <Starward 路径>` 检查
`docs/08-UI-PARITY.md` 里引用的每个源文件是否存在、行数是否漂移（漂移只告警）。
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
| [07-UI-REPLICA.md](docs/07-UI-REPLICA.md) | UI 复刻 Starward：布局/视觉 token/交互/页面映射/验收 | 改 UI 前必读 |

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

### 5. 真 wine 端到端验证

合成游戏（假 wine 脚本）验证的是**状态机**；另有一层用**真 wine 起真 PE 程序**
验证 `docs/01-RESEARCH.md` §1.6 的核心假设："能不能靠 `/proc/<pid>/maps` 找到游戏"。

```bash
LUNARWARD_REAL_WINE=1 dotnet test tests/LunarWard.IntegrationTests \
  --filter 'FullyQualifiedName~RealWine'
```

实测（wine-10.0 CachyOS）：`AwaitImage` 818 ms 靠 maps 命中；
`AwaitReady` **4 ms** 就拿到 `WineDevice` 最强信号，不是靠 `Grace` 兜底。
默认跳过（要建 prefix），设 `LUNARWARD_REAL_WINE=1` 启用。

### 6. 安装器（`Starward.RPC/GameInstall` 移植）

保留全部关键行为：`Parallel.ForEachAsync` 文件级并行、Polly 重试、
`SocketsHttpHandler`（HTTP/2+3 多连接、5 分钟连接池）、硬链接去重、MD5 跳过、断点续传。
去掉了 gRPC / protobuf / Vanara / SharpSevenZip / Snap.HPatch（后两者的 native 只有 Windows，
分别换成外部 `7z` 与 `hpatchz`），并修掉了上游"`is "NTFS"` 在 Linux 上永远为假
导致硬链接失效"的问题。

---

## 实现期间发现的三个"静默缺陷"

这三个都不报错、不抛异常、日志里也没有"错误"字样，但都是真问题。
记录在此，因为它们说明了为什么"必须真跑验证"。

| # | 缺陷 | 表现 | 根因 | 发现方式 |
|---|---|---|---|---|
| 1 | 进程组信号被拒 | shell 关标签后子进程残留 | `Signal()` 拒绝负 pid（`kill` 的"进程组"语义），且无人检查返回值 | 集成测试间歇失败 |
| 2 | 启动按钮是灰的 | 属性/绑定/命令**全对**，像素是灰 | Fluent 的 `ControlTheme` 管模板内背景，`Button.Background` 不穿透 | **截图采样像素** |
| 3 | 首次建 prefix 卡 2 分半 | 每个新用户首次启动必然超时，还留下半成品 prefix | `wineboot -u` 弹「下 wine-mono」并**阻塞等输入** | 真 wine 测试耗时异常（3m25s → 修后 21s） |

详见 `docs/M0-RESULTS.md` §3.5 与 `docs/acceptance-2026-10-23.md` §2.6–2.8。

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
| prefix 初始化 | 直接 `wineboot -u` | **必须**加 `WINEDLLOVERRIDES=mscoree,mshtml=` | 否则卡在 wine-mono 下载提示（实测 2分32秒 vs 8秒） |

---

## 许可

**MIT**（与 Starward 一致）。第三方组件与义务见
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

不打包任何补丁二进制（`Hk4eTouch` 是 AGPL-3.0、`ZZZ-touch-unlocker` 无 LICENSE），
全部由用户自备，以此规避分发风险（`docs/00-DECISIONS.md` D11）。
