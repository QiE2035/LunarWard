# LunarWard 全项目审计汇总（2026-10-25）

> 汇总 5 份只读报告（`01-build-tests` / `02-dead-code` / `03-consistency` / `04-risks` / `05-structure`），去重后 **86 条 → 34 条**（M-01..M-34，其中 27 条可修、7 条待决策）。
> 基准：HEAD `d596a7a`；SDK `10.0.302`；Avalonia **12.1.4**（`Directory.Packages.props`，03 头部写 11 是笔误）。
> 汇总时点工作区另有 4 个未提交源文件（`App.axaml.cs`/`GameAccountStore.cs`/`LunarWardDatabase.cs`/`XdgPaths.cs`，内容即 M-01 的权限与 checkpoint 修复，非本次审计产出）——**批次 A 已有人在写**，下列清单仍以 `d596a7a` 为基线。
> 本轮只产出本文件，未改任何代码、未 commit、未修改 5 份原始报告。

## 一、事实基线（构建/测试/警告/CI 的真实状态，带命令与数字）

| 项 | 命令 | 实测（本机复跑） |
|---|---|---|
| 构建 | `dotnet build LunarWard.slnx -v m --no-incremental` | **0 警告 / 0 错误**，29.2s |
| 构建(警告当错) | `dotnet build LunarWard.slnx -warnaserror --no-incremental` | **成功 exit 0** —— 该开关在本仓库不产出任何信息 |
| 真实告警债 | `dotnet build LunarWard.slnx --no-incremental -p:NoWarn=` | **779 警告 / 0 错误**，18.6s |
| 测试 | `dotnet test LunarWard.slnx` | **531 + 31 + 27 = 589 全绿**，失败 0 / 跳过 0，6s+15s+37s |
| CI 实跑 | `.github/workflows/ci.yml:31-35` | 只跑 531+31 = **562**，IntegrationTests 27 个**不执行** |
| 静默跳过 | `grep -rc 'return;' tests/` | **43 处 `if(!环境) return;`**；`Skip=`/`Assert.Skip` = **0**，报表看不出"没跑" |
| 规模 | `git ls-files` | 624 文件；src 453 `.cs` + 53 `.axaml`；tests 59 `.cs`（50/3/6） |

**告警构成（779 原始行 = 745 唯一代码告警 + 33 AVLN3001 + 1 行跨项目重复）**：CS8618 710、CA1416 26、CS8602 5、CS8625 3、CS8600 1。压制点 `Directory.Build.props:15`（8 族）+ `src/LunarWard.App/LunarWard.App.csproj:14`（AVLN3001）；`TreatWarningsAsErrors=false`（`Directory.Build.props:9`）；CI 无 `-warnaserror`、无 `dotnet format`。

### 1.3 报告间矛盾与裁决

| 矛盾点 | 各方说法 | 裁决（以实测/代码为准） |
|---|---|---|
| 「0 警告」真假 | 01 说 `-warnaserror` 通过但 779 被压；03/05 把 0 warning 当基线 | **两者都对、不冲突**：常态确实 0；`-p:NoWarn=` 是 779。真实结论 = **门禁不存在**，白名单已成债务黑洞 |
| ReleaseChecker 去留 | DC-01 说它是 `MetadataClient` 的活等价物；**PS-03 说删它、改用 Core.Metadata** | **PS-03 方向反了**。`MetadataClient` 全类 2 命中（0 外部）；`ReleaseChecker` 有 App 调用。保留 ReleaseChecker，删 `Core/Metadata/` |
| Settings 命名空间文件数 | CO-07 说 10，PS-08 说 11 | **10**（`git ls-files` 实测），PS 多算 1 |
| 测试规模 | 01 说 50/3/6 文件、531+31+27；PS-10 说 51/4/8 文件、461 用例 | **01 对**：50/3/6 文件；属性 378 `[Fact]`+33 `[Theory]`=411（531 是展开后）。PS 把 `obj/` 生成文件算进去了 |
| 死包数量 | DC-13 说 5，PS-07 说 6（多 `Configuration.Binder`） | **并集 6**：Binder 确无 `Bind`/`Get<T>` 调用 |
| DC-01 严重度 | 02 标 **P0** | **降为 P1**：520 行死代码无用户可见影响；P0 留给凭据与安装损坏 |
| Installer 行数 | 01 说 7261，实测 7193 | 口径差（是否含 axaml/obj），不影响结论 |

**P0 口径**：报告标称 **4 条**（BT-01、DC-01、RK-01、RK-02）；按「用户可见影响 × 修复成本」重排后 **P0 = 3 条**（M-01 凭据、M-03 双启动、M-02 无门禁），DC-01 降 P1。

## 二、维度总览表

| 维度 | 报告 | 条目数 | P0 | P1 | 备注 |
|---|---|---|---|---|---|
| 构建与测试 | `01-build-tests.md` | 13（BT-01..13） | 1 | 5 | 门禁假绿、集成测试不在 CI、安装器零覆盖 |
| 死代码 | `02-dead-code.md` | 20（DC-01..20） | 1 | 5 | 57 个可删单元 / 约 870 行 / 6 个死包 |
| 一致性 | `03-consistency.md` | 16（CO-01..16） | 0 | 5 | 文档/注释/文案与实现相反；i18n 重键 |
| 潜在风险 | `04-risks.md` | 23（RK-01..23） | 2 | 6 | 凭据权限、双启动、管道卡死、资源泄漏 |
| 项目结构 | `05-structure.md` | 14（PS-01..14） | 0 | 3 | 隐式依赖、Launch 过载、双份实现 |
| **合计** | 5 份 | **86** | **4** | **24** | 合并后 34 条；P2 38 / P3 20 |

## 三、Top 10 优先修复（按 用户可见影响 × 修复成本 排序）

1. **凭据在 0644 的 WAL/SHM 里明文，同机任何用户可读** — `src/LunarWard.Games/GameAccountStore.cs:173-186`、`src/LunarWard.Launch/Database/LunarWardDatabase.cs:109`、`src/LunarWard.Platform/XdgPaths.cs:111-122` — M-01（RK-01）— **批次 A**
2. **日志泵读满 8MiB 就停止消费管道，子进程写满阻塞→游戏永不出画面** — `src/LunarWard.Launch/LaunchEngine.cs:699-702`、`:730-733`、`:173` — M-04（RK-03）— **批次 A**
3. **同一安装任务被启动两次，两条流水线写同一目录→半损坏** — `src/LunarWard.Installer/GameInstallService.cs:224-226`、`GameInstallController.cs:106-109`、`GameLauncherPageViewModel.cs:1002-1005`/`:1134-1137` — M-03（RK-02）— **批次 A**
4. **退出应用不停止安装任务，留下半写目录与 `.part`** — `src/LunarWard.App/App.axaml.cs:157-165`、`AppServices.cs:103-113` — M-05（RK-04）— **批次 A**
5. **设置页文案声称"暂未提供安装/更新界面"，实际已实现** — `src/LunarWard.App/Localization/AppStrings.cs:973`/`:976`（en `:2032`/`:2035`）— M-13（CO-03）— **批次 E**
6. **43 处静默跳过 + 27 个集成用例不进 CI，绿灯不代表启动链路可用** — `.github/workflows/ci.yml:31-35`、`PhaseEngineTests.cs:24` 等 — M-02（BT-01）— **批次 B**
7. **i18n 两字典各 20 组重复键，后者静默覆盖（含 `" average"`/`" count"` 前导空格）** — `AppStrings.cs` zh `:611-655`/`:1061-1073`、en `:1238-1240`/`:2114-2133` — M-14（CO-02）— **批次 E**
8. **卸载逻辑两份，Installer 那份 187 行从未被实例化** — `src/LunarWard.Installer/GameUninstallService.cs:11` vs `App/Services/GameUninstaller.cs` — M-11（DC-02/PS-05a）— **批次 C**
9. **构建门禁被 NoWarn 整体吃掉，9 条真实可空缺陷被一起静音** — `Directory.Build.props:15`；`ShiyuDefensePageViewModel.cs:862-864`、`GameInstallFile.cs:149`、`GamePackageService.cs:381` — M-06/M-07（BT-02/03）— **批次 B+D**
10. **安装器核心路径零测试，而"解压"测试实际测的是 wine runner 下载** — `tests/` 对 `ArchiveExtractor`(445)/`SophonManifest`(551)/`DiffPatcher`(169) **0 命中** — M-08（BT-05）— **批次 D**

## 四、修复批次 A–E（可直接照做）

### 批次 A｜止血：权限 + 进程/管道边界（1–2 天，**先做**）
- **目标**：消除凭据泄露与"游戏卡死 / 安装目录半损坏"两类可见故障。
- **条目**：M-01、M-03、M-04、M-05、M-22（RK-07 单实例监听 try/catch、RK-23 日志失败留痕、RK-16 `async void` 包 try）。
- **动作**：
  1. `XdgPaths.cs:111-122` 建目录后 `SetUnixFileMode(dir, 0700)`；`LunarWardDatabase.cs` 每次 `CreateConnection` 后对 `Path+"-wal"/"-shm"` 幂等 `0600`（不存在则跳过），退出时 `PRAGMA wal_checkpoint(TRUNCATE)`；`FileSystem.cs:81-96` 的 `AtomicFile` 用 `FileStreamOptions{UnixCreateMode=0600}` + `try/finally` 删 tmp；`LaunchReport.cs:41-48` 改 0600 并对 `env` 做白名单。
  2. `LaunchEngine.cs:699-702` 把裸 `break` 改成"继续 drain 但不落盘"；`AttachLogPump` 的 catch 至少 `Log.Warn`，失败时关闭重定向而非留无读者管道。
  3. `GameInstallService.StartOrContinue` 内加 `Interlocked.CompareExchange` 单飞（或 context `RunToken`）；`GameInstallController.RunAsync` 改调 `StartOrContinue`；删 `GameLauncherPageViewModel.cs:1003`/`:1136` 两处 `Task.Run`；统一 `SafeFireAndForget(Task, op, logger)` 替换 `_ = Task.Run`。
  4. `App.axaml.cs:157-165` 在 `_shell.Shutdown()` 前调 `Services.ShutdownInstaller()`；`ShutdownInstaller` 先 `StopAllTasks` 再短超时等 ticker；`GameInstallService.Dispose` 补 `_tickerTask.Wait(1s)`。
  5. `SingleInstance.cs:57-69` 循环体包 `try/catch`（`OperationCanceledException`/`ObjectDisposedException` 正常退出，其余 `Log.Warn` + `continue`）；`BackgroundService.cs:288-301` 首次写日志失败记 stderr。
- **验收**：
  ```bash
  dotnet build LunarWard.slnx -v m
  dotnet test tests/LunarWard.UnitTests/LunarWard.UnitTests.csproj --filter "FullyQualifiedName~GameAccountStore"
  ./src/LunarWard.App/bin/Debug/net10.0/LunarWard --quit-after 800
  stat -c '%a %n' ~/.local/share/lunarward ~/.local/share/lunarward/LunarWardDatabase.db*   # 期望 700 + 600 600 600
  ls ~/.local/share/lunarward/*.tmp | wc -l                                                 # 期望 0
  ```
  新增两个用例：① 写库后断言 `-wal`/`-shm` 也是 600；② 用 `FakeWine` 造向 stdout 写 10MiB 的子进程，断言 `LaunchAsync` 5s 内返回；③ 并发两次 `StartOrContinue`，断言下载器只创建 1 次。
- **风险/回滚**：5 个独立 commit（`fix(security): 目录与 WAL 权限` / `fix(install): 单飞` / `fix(launch): 日志泵 drain` / `fix(app): 退出停安装` / `fix(app): 静默失败收口`）；单条 `git revert` 即可回滚，互不依赖。

### 批次 B｜门禁与可复现性（1–2 天）
- **目标**：让 CI 能对"启动链路 / 警告 / 依赖层次"产生真实红绿信号。
- **条目**：M-02、M-06、M-09、M-15、M-28。
- **动作**：
  1. `ci.yml` 追加 `dotnet test tests/LunarWard.IntegrationTests`（ubuntu 自带 gcc 即可，`NativeHelper.Available` 为真）；43 处 `return;` 改 `Assert.Skip($"缺少 {cond}")`（xunit 2.9 支持），CI 输出"已跳过 26"可见；再加一个"跳过数 > 上限即告警"的步骤。
  2. `global.json` 钉 `10.0.302` + `rollForward: latestPatch`；`LangVersion` 从 `preview` 收到 `latest`；加最小 `.editorconfig`；CI 加 `dotnet build -c Release -warnaserror -p:NoWarn="CS8618;CA1416;NU1900;AVLN3001"`（只门禁已清理的族）与 `dotnet format --verify-no-changes --severity warn`（先只对 Platform/Wine/Installer）。
  3. `ComponentInstaller.cs:43` 的 `IndexBaseUrl` 改可覆盖 `internal` 属性；`WineRegistryTests.cs:113-166` 的整体移到 IntegrationTests；`AcceptanceTests.cs:22-38`/`RealWineTests.cs:50-58` 的 runner 路径改环境变量；`InstallerHttpClientFactoryTests.cs:66/84/118` 的挂钟断言改断言"调用次数 + 退避序列"（注入 `TimeProvider`）；`:94` 随机端口改 `TcpListener(IPAddress.Loopback, 0)`。
  4. `Launch.csproj` 加 `Platform`、`Games.csproj` 加 `Wine` + `Microsoft.Data.Sqlite`（`GameAccountStore.cs:190` 现在靠传递引用编译）；加脚本检查"`using LunarWard.X` 必须在本项目引用闭包内"。
  5. `scripts/check-parity.py:54` 阈值 `0.5→0.15`；`:44` 的 `rglob` 兜底改为"重名 basename 直接报错"；`docs/08-UI-PARITY.md` 上游列改全路径。
- **验收**：
  ```bash
  grep -n "IntegrationTests" .github/workflows/ci.yml          # 期望至少 1 处
  LUNARWARD_REAL_WINE= dotnet test LunarWard.slnx              # 期望 589 全过、已跳过 26（显式）
  unshare -n dotnet test tests/LunarWard.UnitTests/LunarWard.UnitTests.csproj   # 断网可过
  dotnet --version                                             # 期望 10.0.302
  scripts/ci.sh && echo OK
  ```
- **风险/回滚**：`Assert.Skip` 会让"跳过"变成可见数字，若团队不接受 26 个跳过，先只做 CI 步骤（1 个 commit）再分步改守卫；每个子项 1 commit（ci.yml / global.json+editorconfig / 测试可复现 / csproj / parity）。

### 批次 C｜死代码与机械清理（0.5–1 天，零行为变更）
- **目标**：删掉 57 个可删单元、6 个死包与 2 份重复实现，代码量 −约 1200 行。
- **条目**：M-10、M-11、M-17、M-18、M-19、M-26、M-27、M-31、M-32。
- **动作**：
  1. 删 `src/LunarWard.Core/Metadata/`（8 文件 520 行，DC-01）；保留 `App/Services/ReleaseChecker.cs`。
  2. 删 `src/LunarWard.Installer/GameUninstallService.cs`（187 行）与 `InstallMessages.cs:172` 的 `UninstallGameRequest`；卸载走 `App/Services/GameUninstaller.cs`。
  3. 删 `FileSystem.cs` 的 `IFileLock`/`FlockFileLock`（保留 `AtomicFile`）；删 `Libc.cs` 16 个零引用 P/Invoke；删 `GameRegistry.cs` 16 个常量；删 App 14 个无绑定成员与 `StygianOnslaughtMonsterTag.cs`。
  4. 删 6 个死包（`Markdig`、`Serilog.Extensions.Logging`、`Configuration.Binder`、`Configuration.Json`、`Avalonia.Diagnostics`、`Avalonia.Controls.ItemsRepeater` 12.0.0）；**删 Serilog.Extensions 时必须显式加回 `Serilog` 本体**，否则 `using Serilog;` 编译失败。
  5. 删 `Views/GachaWindow.axaml(.cs)`、`ViewModels/GachaViewModel.cs`、`Views/ScreenshotWindow.axaml(.cs)`；`ShellWindow` 移到 `Views/Starward/Legacy/` 加 `Obsolete`。
  6. `HardLink.cs:31` 的 `link`、`DriveHelper.cs:368` 的 `statx` 上提到 `Platform/Unix/Libc.cs`。
  7. 命名：`CaptureMode.cs` → `.Services`；6 个 `ShiyuDefense/V2/` 文件按决策结论处理；`_camel` 按 6 个文件分批；`RequestClose→CloseRequested`、`DeleteCurrentMonthAsync→DeleteCurrentMonth`。**每批一个 commit，不与功能混。**
  8. `LUNARWARD_TOOLBOX_DEMO` 26 处 → `ToolboxDemoMode.IsEnabled`；图标加载抽 `StarwardAssets.LoadIconsAsync<T>`；版本解析 6 套 → 1 个 `LunarVersion`。
  9. 删 `GameRecordClient.cs:583-609` 的 `[Obsolete]`+注释块；2 条 TODO 转 issue 后删注释。
- **验收**：
  ```bash
  dotnet build LunarWard.slnx -v m && dotnet test LunarWard.slnx     # 589 不回归
  rg -i "MetadataClient|GameUninstallService|FlockFileLock" src/ tests/   # 期望 0 命中
  rg -c "PackageVersion Include" Directory.Packages.props                  # 期望 17（23−6）
  rg -n "ItemsRepeater|Markdig" src/ -g '*.csproj' -g '*.props'            # 期望 0
  bash scripts/smoke-screenshots.sh 2>&1 | tail -5
  ```
- **风险/回滚**：9 个 commit 一一对应上面 9 步，任何一步 `git revert` 不影响其他；命名类 commit 只做重命名（`git show --stat` 应只见增删同量）。

### 批次 D｜覆盖与正确性（3–5 天）
- **目标**：把 9 条真实可空缺陷修掉，给安装/解压/清单补离线测试，堵资源泄漏。
- **条目**：M-07、M-08、M-21、M-24、M-25。
- **动作**：
  1. `Directory.Build.props:15` 移除 `CS8600;CS8602;CS8625`（保留 CS8618/CA1416），修 6 个文件 9 处：`ShiyuDefensePageViewModel.cs:862-864`、`GameInstallFile.cs:149`/`:152`、`GamePackageService.cs:381`、`GameLauncherPage.axaml.cs:242`、`GameSettingPage.axaml.cs:42`、`GenshinBeyondGachaTests.cs:170`。
  2. 新增离线用例：`ArchiveExtractor`（临时目录造 zip/tar.zst，复用 `ComponentInstallerTests` 里 `TarWriter`/`GZipStream` 写法）、`SophonManifest`（样例 JSON 测 chunk 匹配/缺块报错）、`DiffPatcher`（预置旧文件 + 空补丁，断言校验失败即中止且原文件不变）。各 3–5 个。
  3. `GachaIconCache.Memory` 换 LRU/`MemoryCache` + size limit，淘汰时 `Dispose`；`ScreenshotViewModel.cs:224`、`ImageViewWindow2ViewModel.cs:280` 覆盖前 `Interlocked.Exchange` 旧值并 Dispose；`BackgroundService.cs:150` 的 `Media` 加 `using`；16 处 `new HttpClient` 收敛到共享实例（App 侧照搬 `InstallerHttpClientFactory`）。
  4. 抽 `HomeDirectory.Resolve()` 单一实现，禁止直接 `GetFolderPath(UserProfile)`；`GameSettingPageViewModel.cs:79` 回填 `Args` 时对含空格元素加引号；统一 `IUrlLauncher`（`xdg-open` + `ArgumentList.Add`）。
  5. `PlayTimeService.cs:52-72` 锁内只取快照，排序在锁外；`GachaViewModel.cs:188-201`、`ScreenshotViewModel.cs:96-103` 改整体替换集合引用。
- **验收**：
  ```bash
  dotnet build LunarWard.slnx -warnaserror -p:NoWarn="CS8618;CA1416;NU1900;AVLN3001"   # 期望成功
  rg -c "warning CS860" /tmp/build.log                                                # 期望 0
  dotnet test tests/LunarWard.Installer.UnitTests -c Release                          # 31 → ≥45
  rg -c "new HttpClient" -g '*.cs' src/                                               # 期望 ≤2
  rg -n "GetFolderPath\(Environment.SpecialFolder.UserProfile\)" -g '*.cs' src/       # 期望仅 XdgPaths.cs
  dotnet test LunarWard.slnx                                                          # 589 不回归
  ```
- **风险/回滚**：可空修复每文件 1 commit；测试新增独立 commit；泄漏修复独立 commit。`ArchiveExtractor` 测试若暴露真实 bug，先加 `[Fact(Skip="见 issue #N")]` 记录再修，避免阻塞 CI。

### 批次 E｜文档与用户可见文案（1 天，零代码风险）
- **目标**：文档数字、注释、AppStrings 与实现一致，避免误导用户与后续开发。
- **条目**：M-12、M-13、M-14。
- **动作**：
  1. 数字：`docs/08-UI-PARITY.md:106`、`docs/07-UI-REPLICA.md:196` 的「348（290+31+27）」→「589（531+31+27）」；`docs/05-ROADMAP.md:258` 拆分算式改「I1–I13 14 + 验收 6 + S1–S5 + 真 wine 2」。
  2. 渠道：`docs/09-LINUX-DIVERGENCES.md:14`、`docs/08-UI-PARITY.md:29` 改为「已支持 Bilibili（校验 + config 重写 + 插件检测），腾讯渠道未做」；`08:42` 限定为「渠道**包体**分组未移植」。
  3. 6 处「尚未移植」注释改「已移植（实现于 X）」：`GachaExport.cs:15`、`ScreenshotPage.axaml:222`、`ToolboxPage.axaml:13/14-15/283/286`、`SettingDownloadView.axaml:22`；保留 `ScreenshotPage.axaml:20`(AnnotatedScrollBar) 与 `StartupCommands.cs:18`(rpc) 这两条真实说明。
  4. 用户文案：`AppStrings.cs:973`/`:976`（en `:2032`/`:2035`）改为真实限制；`CO-13` 的 `show_announcements`/`VideoBackground` 要么接线读取（推荐，两处都小），要么文案改「不生效且每次启动重置为开」。
  5. i18n：删 20 组重复键的较早一份（`AuthkeyTimeout` 取 `:1069`、`LanguageDesc` 取 `:1073`、`UidPlaceholder` 取 `:1061`）；新增 `scripts/check-i18n.py`（键集相等/无重复/en 无 CJK）并挂 CI。
  6. 补 `README.md` 开关表 5 行（`--toast-test` `--background-bar` `--drag-overlay` `--uigf-export` `--expand-rail`）；`third_party/README.md:5` 对齐 `docs/02-ARCHITECTURE.md:84-85`。
- **验收**：
  ```bash
  grep -rn "348\|290 单测" docs/*.md README.md                # 期望无输出
  grep -rn "只保留官方区服" docs/*.md                          # 期望无输出
  grep -rn "尚未移植\|暂未移植\|LunarWard 未接" src/ --include=*.cs --include=*.axaml   # 期望仅剩 2 条真实项
  python3 scripts/check-i18n.py                                # 期望 zh 1031 / en 1031 / 重复 0
  python3 scripts/check-parity.py                              # 期望 0 missing
  dotnet build LunarWard.slnx                                  # 0 warning / 0 error
  ```
- **风险/回滚**：纯文本改动，按"数字 / 注释 / 文案 / i18n / README"5 个 commit；i18n 删键后必须跑一次 `dotnet build` + 3 个页面冒烟确认没有 `MissingKey`。

## 五、需要用户决策（**不要自动做**）

| 项 | 涉及 ID | 为什么必须等确认 | 建议选项 |
|---|---|---|---|
| Launch 拆分 / 新增 `LunarWard.Data`、`LunarWard.Gacha` | M-16（PS-01） | 新建 2 个项目 + 移动 5 个 store，动 `Games→Launch` 语义耦合，影响打包与 MiniExcel 依赖 | 先只抽 `Database/` 一个项目；Gacha 留待下轮 |
| 删除未使用公开 API 面 | M-20（DC-09/10/11/07） | `GameRecordClient` 10 抽象方法 + 2 override、`HoYoPlayClient` 3 组、`SelfQueryClient` 4 个、`Games` 14 helper 都是对外形状 | ① 删；② 标 `[Obsolete]` 保留一版；③ Games 作公共库则保留并注 `Not used by app` |
| `HoYoPlayClient` 与 Installer 私有实现合并 | M-20（DC-10） | 需先把 `GamePackageService.cs:990-1027` 上移调用，属行为重构 | 先补 Installer 测试锁行为，再合并 |
| IntegrationTests 入 CI 的环境变量策略 | M-02、M-09 | 27 个用例里 26 个依赖 `cc`/真 wine/真 `$HOME`；`LUNARWARD_REAL_WINE` 是否默认开、是否上自托管 runner 决定成本 | 默认只跑 `cc` 可满足的 20 个；真 wine 留 `env` 闸 |
| `ColorRectChart` 合一 | M-34（PS-05b） | 两个同名控件（`Border` 版 vs `Control` 版）渲染路径不同，合并需目视回归 | 先在 2 个页面上做 A/B 截图，再合 |
| SQLite `user_version` 迁移 | M-23（RK-15） | 涉及老库兼容与备份策略，做错会启动即崩 | 加 `user_version` + 事务迁移 + 升级前 `VACUUM INTO` 备份 |
| AppStrings → resx / 按页拆 partial | M-30（PS-09） | 2215 行单文件迁移影响全部页面与翻译流程 | 短期先按页 `partial` 打散，resx 另立计划 |
| 测试工程拆分 + `LunarWard.Testing` | M-29（PS-10） | UnitTests 现同时引 `App`(WinExe)，拆工程会改 CI 依赖与用例归属 | 先抽 `Testing` 共享 fixture，再按层拆 |
| `docs/audit/` 去留 | M-33（PS-14） | 直接决定本次产出与后续审计的归属 | 建议纳入版本库（纯文本、体积小），仅忽略 `*.png`/`*.html` |
| CA1416 处置策略 | M-07（BT-04） | 「改平台抽象」vs「按项目豁免」成本差一个量级 | 26 条先按项目级豁免并记录，新增代码进 `-warnaserror` |
| `xunit` v2→v3 升级 | PS-10 附表 | 2.9.3 与 runner 3.1.5 跨大版本，升级影响全部 3 个测试工程 | 单独一期，不与任何功能批次混 |
| `docs/03-BUILD-TEST.md` 是否存在 | CO §3.2 | 任务书列为基线但仓库无此文件 | 确认是笔误还是应新建 |

## 六、本轮不做

- **710 条 CS8618 不逐条修**：来自移植的 DTO，加 `required`/构造会大面积改结构且无行为收益；只保留白名单 + 新增文件受 `-warnaserror` 约束。
- **不给 324 个 DTO 补"序列化往返"测试**：预算应投在 M-08 的解压/校验逻辑。
- **真 wine + mingw 交叉编译不进默认 CI**（M-09 的一部分）：runner 上极慢且易碎，保持 `env` 闸 + 显式跳过。
- **不做 27 个大文件拆分**（`GameLauncherPageViewModel.cs` 1786、`GameSettingPageViewModel.cs` 1673、`AppStrings.cs` 2215、`HyperionClient.cs` 1068 等）：系统性重构，与 M-16 一起排期。
- **不追求行覆盖率数字**、不把 Avalonia 页面改回 runtime loader（AVLN3001 的 33 个页面属设计选择，用 headless 构造测试替代）。
- **依赖外部资源的三项**：Bilibili 插件真实下载（需网络）、`smoke-screenshots.sh` 全 18 页进 CI（需 Xvfb/真 GPU 环境）、B 服客户端真机验证。
- **不做逐条 `SuppressMessage`**（CA1416）与 **xunit v3 迁移**（见 §五）。
- **不重写 git 历史**（CO-10/CO-11 的提交信息问题只补新 commit，不改旧记录）。

## 七、审计方法（5 个子代理的只读证据）

- **01 构建与测试**：`dotnet build` 四种参数组合 + 589 用例实跑 + `tests/` 全量 grep 覆盖率，得出 779 告警构成表与 12 类可空转用例清单。
- **02 死代码**：全量符号提取（类型 337 / 方法 737 / 属性 1794 / 字段 765），对每个名字在 src+tests+axaml 做词频引用计数，再人工排除绑定/源生成/JSON 契约/P-Invoke/反射假阳性。
- **03 一致性**：453 个 `.cs`（`utf-8-sig` 读 BOM）静态扫描 + 上游 `Starward` 行号自动比对 + 2 个字典键集解析 + `git show` 抽查 2 条提交。
- **04 潜在风险**：`rg` 只读扫描 + 3 处实测复现（WAL 侧车权限与明文位置、目录权限、冷启动 1.24–1.37s），其余为代码路径推演，不能定量的单列"需实测"。
- **05 结构**：`.csproj`/`.slnx`/`Directory.Packages.props`/`git ls-files` + `wc -l` 静态分析，未编译（因此其测试文件数与 `warnaserror` 结论已按 §1.3 修正）。
- 本汇总的 `dotnet build/test` 数字为 2026-10-25 本机复跑，与 01/03 的原始记录相差 <2%（机器负载差异），结论一致。

---

## 七、批次 A（止血）执行状态 — 2026-10-25 16:20

| 条目 | 提交 | 说明 |
|---|---|---|
| M-01 WAL/侧车凭据 | `f46ac48` | 目录 0700 + 侧车 0600 + 退出 checkpoint + 启动清理残留；`DatabasePermissionTests` 3 个 |
| M-01 余项（AtomicFile / 报告 env） | 见批次 A 第二个提交 | 临时文件 0600、移动后即 0600；LaunchReport 环境变量按名字打码 |
| M-04 日志泵停读 | 同上 | 抽成 `GameLogPump`：读满上限只停写不停读，3 个单测 |
| M-03 安装双启动 | `e8a044d` | 服务内单飞守卫 + 四处调用点改用 `CurrentRun` |
| M-05 退出不停安装 | `e8a044d` | `App.Exit` 调 `AppServices.ShutdownInstaller()` |
| M-22 监听循环 | `e8a044d` | `SingleInstance` accept 异常不再终止循环 |

验证：`dotnet test LunarWard.slnx` → **537 / 31 / 27** 全过（批次 A 新增 6 个测试）。
批次 B/C/D/E 未开始。
