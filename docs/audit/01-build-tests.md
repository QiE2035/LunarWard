# 构建与测试审计

只读审计，未修改任何代码。环境：Linux，SDK `10.0.302`，`DISPLAY=:0`/`WAYLAND_DISPLAY=wayland-1`（有图形环境，故 UI 相关测试非无头验证），仓库 `/home/qie2035/projects/lunarward`，HEAD 工作区仅 `docs/audit/` 未跟踪。

## 1. 结论摘要

1. **"0 警告"是压制出来的，构建门禁实际不存在**：默认构建 0 警告 0 错误，`-warnaserror` 也 exit 0；用 `-p:NoWarn=` 还原后是 **779 条警告**（去重 745 条代码告警 + 33 个 AVLN3001）。`Directory.Build.props:15` 一次压掉 8 个代码族，`TreatWarningsAsErrors=false`（:9），CI 也从不跑 `-warnaserror`。
2. **发布链路（启动相位 / 进程回收 / shell 环境）没有任何 CI 门禁**：`.github/workflows/ci.yml:31-35` 只跑 UnitTests 与 Installer.UnitTests，27 个 IntegrationTests 用例全不执行；而这 27 个里有 **26 个**依赖外部环境（`cc` / 真实 wine / 真实 `$HOME`），缺环境时靠 `return;` 静默通过（全仓库 `Skip=` 计数为 0）。
3. **安装器（7261 行）核心路径零覆盖**：`ArchiveExtractor`(445 行)/`SophonManifest`(551)/`DiffPatcher`(169)/`GamePackageService`(1046)/`GameInstallService`(929)/`GameInstallHelper`(1078) 在 `tests/` 中零命中；所谓"安装器解压"测试实际测的是 `src/LunarWard.Wine/ComponentInstaller.cs` 的 wine runner 下载。
4. **3 处测试违背可复现性**：单测真连公网（`ComponentInstallerTests.cs:314` → `ComponentInstaller.cs:43`）；**单元测试项目**里跑真实 `wineboot`（`WineRegistryTests.cs:113-166`）；两个测试硬编码真实 `$HOME` 下的 `sleepy-launcher` runner 路径（`AcceptanceTests.cs:22-38`、`RealWineTests.cs:50-58`）。
5. **断言质量两极分化**：SQLite store / UIGF4 / 账号库的断言很精确（blob 往返 + `0600` 权限 + 闭区间删除 + 异常 Kind/Field）；但 Excel 导出只断言"文件 >3000 字节"，另有 2 处 `ThrowsAny<Exception>`。
6. 基础设施不差：589 个用例全绿（6s/15s/37s），`scripts/smoke-screenshots.sh`、`scripts/check-parity.py` 都已存在，缺的是**接进 CI** 与**去掉静默跳过**。

## 2. 数据

### 2.1 构建结果

| 命令 | 结果 | 耗时 |
|---|---|---|
| `dotnet build LunarWard.slnx -v m` | 0 警告 0 错误，成功 | 17.37s |
| `dotnet build LunarWard.slnx -v m --no-incremental` | 0 警告 0 错误，成功 | 17.00s |
| `dotnet build LunarWard.slnx -warnaserror --no-incremental` | **成功，exit 0**（任务预期"会失败"，实测通过） | ~17s |
| `dotnet build LunarWard.slnx -v m --no-incremental -p:NoWarn=` | **779 警告 0 错误** | ~18s |

无任何 `NU*` 告警（`grep -oE 'warning NU[0-9]+'` 为空）：没有 NU1603/NU1903，也没有 NU1900（漏洞审计本次未报环境错）。`Directory.Packages.props` 声明版本全部真实存在（Avalonia 12.1.4、Microsoft.Data.Sqlite 10.0.13、xunit 2.9.3、Test.Sdk 18.0.1）。

### 2.2 被 `NoWarn` 压掉的告警构成（`-p:NoWarn=` 实测，按 file(line,col) 去重）

| 代码 | 唯一条数 | 压制位置 | 代表位置 |
|---|---|---|---|
| CS8618 | 710 | `Directory.Build.props:15` | `src/LunarWard.Core/HoYoPlay/GameConfig.cs`(21 处)、`GameContent.cs`(20)、`Metadata/Github/GithubUploader.cs`(16)、`SelfQuery/GenshinQueryItem.cs`(13) |
| CA1416 | 26 | 同上 | `src/LunarWard.Platform/ProcessLauncher.cs:110`、`src/LunarWard.Installer/DiffPatcher.cs:160`、`src/LunarWard.Wine/ComponentInstaller.cs:402/450/451`、`src/LunarWard.App/ViewModels/EnvironmentShellViewModel.cs:102` |
| CS8602 | 5 | 同上 | `src/LunarWard.Installer/GamePackageService.cs:381`、`src/LunarWard.App/Views/Starward/Pages/GameLauncherPage.axaml.cs:242`、`tests/LunarWard.UnitTests/GenshinBeyondGachaTests.cs:170` |
| CS8625 | 3 | 同上 | `src/LunarWard.App/ViewModels/Starward/ShiyuDefensePageViewModel.cs:862/863/864` |
| CS8600 | 1 | 同上 | `src/LunarWard.Installer/GameInstallFile.cs:149` |
| AVLN3001 | 33 | `src/LunarWard.App/LunarWard.App.csproj:14` | `src/LunarWard.App/Views/**/*.axaml`（页面无 public 无参 ctor，`avares://` 不可 runtime load） |

说明：命令行传入的全局属性会覆盖项目内赋值，所以 `-p:NoWarn=` 把 `Directory.Build.props:15` 与 `App.csproj:14` 的压制一起失效，745+33 条全部显形；正常构建下它们确实被压成 0。

### 2.3 测试项目与用例数

| 项目 | 源文件 | `[Fact]`/`[Theory]` | 展开后用例 | 耗时 | CI 执行 |
|---|---|---|---|---|---|
| `tests/LunarWard.UnitTests` | 50 | 378 / 33 | **531** | 6s | ✅ `ci.yml:31-32` |
| `tests/LunarWard.Installer.UnitTests` | 3 | 20 / 3 | **31** | 15s | ✅ `ci.yml:34-35` |
| `tests/LunarWard.IntegrationTests` | 6 | 27 / 0 | **27** | 37s | ❌ **不在 CI** |
| 合计 | 59 | 430 / 36（`InlineData` 164） | **589** | 58s | — |

全部通过：`失败 0，通过 531/31/27，已跳过 0`。仓库内 `Skip=` / `[Fact(Skip` / `[Ignore]` 共 **0** 条——"跳过"全部由方法体内的 `return;` 实现。

### 2.4 依赖环境 / 可静默空转的用例

| 用例 | 证据 | 触发条件 | 缺环境时的表现 |
|---|---|---|---|
| PhaseEngineTests 全部 14 个 | `PhaseEngineTests.cs:24,54,77,…`（14 个 `[Fact]` ↔ 14 处 guard） | `NativeHelper.Available`（需 `cc`/`gcc`/`clang` 编译 `native/fakeproc.c`） | 14 个用例全部 `return`，**报告"通过"** |
| ShellTests S1–S4 | `ShellTests.cs:28,91,124,155` | 同上 | 4 个静默通过 |
| ShellTests S5 `ExportScript_IsValidShell` | `ShellTests.cs:242-300` | 无（只用 `/bin/sh` + `sh -n`） | **始终真跑**，断言强（引号/source 结果） |
| AcceptanceTests R12/R15/Report/SamePlan/DryRun | `AcceptanceTests.cs:66,89,119,148,171` | `NativeHelper.Available` | 5 个静默通过 |
| AcceptanceTests R9 | `AcceptanceTests.cs:22-38` | 真实 `$HOME/.local/share/sleepy-launcher/runners/spritz-wine-cachyos-wow64-10.0-11/bin/wine` | `File.Exists` 失败即 `return`，静默通过 |
| RealWineTests 2 个 | `RealWineTests.cs:37-39`(`Enabled`)、`:140`、`:272` | `LUNARWARD_REAL_WINE=1` + 真 wine + `x86_64-w64-mingw32-g++` | 静默通过 |
| `WineRegistryRealWineTests.WriteThenRead_Dword_RoundTrips` | `WineRegistryTests.cs:113-166` | `LUNARWARD_REAL_WINE` + `LUNARWARD_TEST_WINE`（真 `wineboot -u`，注释称 ~8s，实际可数分钟） | 静默通过；真跑时读真实 `$HOME`(`:144`) |
| DriveHelperHardLinkTests 2 个 Theory | `DriveHelperHardLinkTests.cs:83,142` | 需要真实挂载点/vfat 语义 | 部分数据行 `return`，该行不计断言 |
| DriveHelperVolumeTests 2 个 | `DriveHelperVolumeTests.cs:114,141` | 真实挂载表 | 静默通过 |
| `ComponentInstallerTests.UpdateIndex_NoNetwork_…` | `ComponentInstallerTests.cs:307-318` | **真公网**：`ComponentInstaller.cs:43` `IndexBaseUrl = "https://raw.githubusercontent.com/an-anime-team/components/main"` | 有网时走成功分支，无网走 Errors 分支，两者都算过 |

本机同时装有 `cc`/`gcc`/`clang`/`x86_64-w64-mingw32-g++`，所以 27 个集成用例这次确实执行了（37s 可佐证）；换到无 `cc` 的机器/容器，同一命令仍是"全绿"。

### 2.5 零测试的关键模块（`tests/` 中 grep 该类型名命中 0）

| 模块 | 规模 | 说明 |
|---|---|---|
| `src/LunarWard.Installer`（除 DriveHelper、InstallerHttpClientFactory） | 6446 行未覆盖 / 7261 | `GameInstallHelper.cs` 1078、`GamePackageService.cs` 1046、`GameInstallService.cs` 929、`SophonManifest.cs` 551、`ArchiveExtractor.cs` 445、`GameInstallFile.cs` 262、`DiffPatcher.cs` 169、`GameUninstallService.cs` 187、`ParentProcessWatcher.cs` 177 |
| `src/LunarWard.Core/HoYoPlay` | 18 文件 2227 行 | Sophon chunk/patch 构建、GameConfig/GameContent 解析，全无测试 |
| `src/LunarWard.Core/SelfQuery` | 13 文件 1083 行（仅 `SelfQueryLogStore` 有测试） | 自助查询日志解析 |
| `src/LunarWard.Core/Metadata/Github` | 5 文件 | `GithubUploader` 等，无测试 |
| GameRecord 子模块 | ImaginariumTheater 600、ShiyuDefense 648、StygianOnslaught 363、ThresholdSimulation 320、SimulatedUniverse 303 | 有 Page 级"展示换算"测试的仅 ChallengePeak/TrailblazeCalendar 等少数 |
| `src/LunarWard.App/ViewModels/Starward` | 25 文件 | 仅少数页面有 PageTests |

粗口径：324 个源文件（45481 行）内的 public 类型在 `tests/` 中零出现（DTO 可能被间接覆盖，仅作定位）。

### 2.6 关键测试抽查

| 被测点 | 用例 | 断言强度 | 结论 |
|---|---|---|---|
| SQLite 抽卡库 | `GachaLogStoreTests.DeleteRangeRemovesOnlyMatchingUidAndInclusiveRange:86-104` | 插 4 条含第二 uid，删闭区间，断言 removed==2、剩 Id==3、另一 uid 仍 1 条 | **强**，同时锁住 uid 隔离与闭区间语义 |
| SQLite 往返 | `GachaLogStoreTests.GachaInfo_RoundTrip_And_FillItemIdByName` | UpdateItemIds 返回 1、ItemId 回填、icon 数 2 | **强** |
| 账号库权限 | `GameAccountStoreTests.SaveAndGet_RoundTripsBlob` | blob 往返 + `File.GetUnixFileMode == UserRead\|UserWrite` | **强**（0600 被锁） |
| UIGF4 导出 | `Uigf4Tests.ExportMapsGenshin400To301AndCarriesTimezoneAndLang` | uid 为字符串、timezone==8、`gacha_type=400` 且 `uigf_gacha_type=301`、时间格式 | **强** |
| UIGF4 导入 | `Uigf4Tests.ImportAppliesTimezoneOffsetAndMergesById` / `ImportRejectsUidMismatch` | Time +1h 精确到秒；`Throws<Uigf4ImportException>` 且校验 Kind/Field/FoundUid | **强** |
| 启动相位 | `PhaseEngineTests`（14 个） | 47 处 `Assert`，断言 7 相位全链、环境变量逐键、残留进程收敛 | **强但可空转**（见 BT-01） |
| Excel 导出 | `GachaExcelExportTests.ExportAsync_WritesWorkbook:92-119` | 仅 `File.Exists` + `Length > 3000`（`:105-106`） | **弱**，单元格内容/行数/表头均可回归而不被发现 |
| wine runner 下载 | `ComponentInstallerTests.InstallWine_DownloadsExtractsAndMarksExecutable` | IsInstalled、bin/wine 存在、`UserExecute` 位、`.tmp` 已清理 | **强**（但走 `HttpListener` 随机端口） |
| 游戏包解压 | 无对应测试 | — | **不存在**，见 BT-05 |

## 3. 问题清单

### BT-01 ｜ P0 ｜ 发布链路没有 CI 门禁，且用例可静默空转

- 证据：`.github/workflows/ci.yml:31-35` 只执行 UnitTests 与 Installer.UnitTests；IntegrationTests 的 27 个用例一个都不跑。其中 `PhaseEngineTests.cs` 14/14、`ShellTests.cs` 4/5、`AcceptanceTests.cs` 5/6 以 `if (!NativeHelper.Available) return;` 开头（`FakeWine.cs:11-25` 找不到 `cc/gcc/clang` 时 `Available=false`）；`RealWineTests.cs:140/272` 与 `WineRegistryTests.cs:126` 以环境变量为闸。全仓库 `Skip=` 为 0，所以 **xunit 报告里看不出"没执行"**。
- 影响：应用最核心的"相位编排 → 进程启动 → 残留回收 → shell 环境一致"只在开发者本机可能被验证；CI 绿灯不代表启动链路可用。这类回归会直接以发布故障形式出现（P0 定义命中）。
- 建议：`ci.yml` 增加 IntegrationTests 步骤（ubuntu runner 自带 gcc）；把所有 `return;` 换成 xunit 2.9 的 `Assert.Skip(...)`（或 `[Fact(Skip=...)]` + 条件 Fact），让"跳过"在报表里显式可见，并对"预期跳过"设上限（如 >0 则打印告警）。

### BT-02 ｜ P1 ｜ `-warnaserror` 假绿：构建门禁被 `NoWarn` 整体吃掉

- 证据：`dotnet build LunarWard.slnx -warnaserror --no-incremental` → `成功生成`、`0 个警告`、exit 0；同一命令加 `-p:NoWarn=` 后 `779 个警告`。压制点：`Directory.Build.props:15`（CS8618/CS8600/CS8602/CS8603/CS8604/CS8625/CA1416/NU1900）、`:9`（`TreatWarningsAsErrors=false`）、`src/LunarWard.App/LunarWard.App.csproj:14`（+AVLN3001）。CI 里没有 `-warnaserror` 步骤（`ci.yml:28-29` 仅 `dotnet build -c Release`）。
- 影响：任何"警告级"回归（新增可空性缺陷、新增平台不兼容调用、XAML 不可加载）都不会让任何流水线变红；`-warnaserror` 这个验证手段在本仓库**当前不产出任何信息**。
- 建议：分批还债而不是一次性打开——先去掉 `CS8600;CS8602;CS8625`（合计仅 9 条，见 BT-03），把这三族修完后在 CI 加 `dotnet build -c Release -warnaserror -p:NoWarn="$(既定白名单)"`；CS8618 长期白名单化并写进 `Directory.Build.props` 注释里（现已存在，但需与实际条数对齐）。

### BT-03 ｜ P1 ｜ 被压制的可空性警告中存在真实缺陷

- 证据（`-p:NoWarn=` 输出）：
  - `src/LunarWard.App/ViewModels/Starward/ShiyuDefensePageViewModel.cs:862/863/864` CS8625（把 `null` 赋给非空引用）；
  - `src/LunarWard.Installer/GameInstallFile.cs:149:33` CS8600、`:152:22` CS8602；
  - `src/LunarWard.Installer/GamePackageService.cs:381:42` CS8602；
  - `src/LunarWard.App/Views/Starward/Pages/GameLauncherPage.axaml.cs:242:57`、`GameSettingPage.axaml.cs:42:40` CS8602；
  - 连测试代码自己也有：`tests/LunarWard.UnitTests/GenshinBeyondGachaTests.cs:170:25` CS8602。
- 影响：这 6 个文件处于安装/启动主路径，NRE 会表现为"点开始游戏没反应"或安装中断；因为被全局压制，编译器连提示都不给。
- 建议：只对 `CS8600/CS8602/CS8625` 撤掉 `NoWarn`（`CS8618` 保留），逐条修这 6 处；修完后这三族进 `-warnaserror`。

### BT-04 ｜ P1 ｜ CA1416（平台兼容）整族压制，Linux 化后无人守

- 证据：`Directory.Build.props:15` 压 `CA1416`，实测 26 条唯一告警，例如 `src/LunarWard.Platform/ProcessLauncher.cs:110`、`src/LunarWard.Games/GameAccountStore.cs:179`、`src/LunarWard.Installer/DiffPatcher.cs:160`、`src/LunarWard.Launch/DailyNoteService.cs:367`、`src/LunarWard.Wine/ComponentInstaller.cs:402/450/451`、`src/LunarWard.App/ViewModels/EnvironmentShellViewModel.cs:102`。
- 影响：这是"从 Windows 版 Starward 移植到 Linux"的项目，平台兼容分析恰好是最该开着的分析器；关掉后新增的 Windows-only API 调用（注册表、`Process.Start` 语义、路径分隔符）不会有任何提示。
- 建议：先处理 26 条（多数应是 `[SupportedOSPlatform("linux")]` 或改走 `ILinuxProcess` 抽象），然后仅对 `src/LunarWard.App` 保留豁免并在审计里记录原因；`Directory.Build.props` 不应全局压 `CA1416`。

### BT-05 ｜ P1 ｜ 安装器与游戏包路径零测试，"解压"被测对象错位

- 证据：`grep -rn 'ArchiveExtractor\|SophonManifest\|GamePackageService\|GameInstallService\|DiffPatcher' tests/` → **0** 命中。`tests/LunarWard.Installer.UnitTests` 只有 3 个文件（`DriveHelperHardLinkTests.cs` 4 Fact+3 Theory、`DriveHelperVolumeTests.cs` 7 Fact、`InstallerHttpClientFactoryTests.cs` 9 Fact），覆盖 `DriveHelper.cs`(603) + `InstallerHttpClientFactory.cs`(212) ≈ 11% 的安装器代码。真正被"解压/校验"测试覆盖的是 `src/LunarWard.Wine/ComponentInstaller.cs`（`ComponentInstallerTests.cs`，8 个 Fact）。
- 影响：`ArchiveExtractor`(445 行，zip/tar/zstd 多格式)、`SophonManifest`(551 行，chunk 校验/断点续传)、`DiffPatcher`(169 行) 是安装失败与磁盘写坏的高发区，目前零回归保护；`DiffPatcher.cs:160` 还同时带 CA1416。
- 建议：优先给三个纯逻辑单元补测（无需真实游戏目录）：`ArchiveExtractor` 用临时目录造 zip/tar.zst（`ComponentInstallerTests` 里已有 `TarWriter`/`GZipStream` 现成写法）；`SophonManifest` 用样例 JSON 测 chunk 匹配与缺块报错；`DiffPatcher` 用预置旧文件 + 空补丁测"校验失败即中止且不改动原文件"。

### BT-06 ｜ P1 ｜ 测试存在公网 / 真实 wine / 真实 `$HOME` 依赖

- 证据：
  - `tests/LunarWard.UnitTests/ComponentInstallerTests.cs:307-318`：`await installer.UpdateIndexAsync()` 直连 `ComponentInstaller.cs:43` 的 `https://raw.githubusercontent.com/an-anime-team/components/main`，断言是 `Assert.True(result.Success || result.Errors.Count > 0)`（:317），注释自认"有网时可能成功"。
  - `tests/LunarWard.UnitTests/WineRegistryTests.cs:122-166`：**单元测试项目**里执行真实 `wineboot -u`（`:150-165`），读取真实 `Environment.GetFolderPath(UserProfile)`（`:144`）。
  - `tests/LunarWard.IntegrationTests/AcceptanceTests.cs:22-38`、`RealWineTests.cs:50-58`：硬编码 `$HOME/.local/share/sleepy-launcher/runners/spritz-wine-cachyos-wow64-10.0-11`。
- 影响：CI 上"通过"与开发者机器"通过"含义不同；公网用例在受限网络下变成 3s 超时等待（还吃掉 CI 时间），真实 wine 用例在有 wine 的机器上可能跑几分钟甚至污染 `$HOME`。违反"测试应可离线、可重复"。
- 建议：`UpdateIndexAsync` 改为可注入 base URL（构造函数已支持 `HttpClient`，只需把 `IndexBaseUrl` 变成可覆盖的 `internal` 属性），无网用例用 `HttpClient` + 不可解析域名断言错误分支；`WineRegistryRealWineTests` 移入 IntegrationTests 并保持 env 闸；两个 runner 路径用例改为"由环境变量提供 runner 路径"。

### BT-07 ｜ P2 ｜ 真实挂钟与随机端口导致慢/不稳定

- 证据：`InstallerHttpClientFactoryTests.cs:66` `Assert.InRange(stopwatch.Elapsed.TotalSeconds, 5.0, 30.0)`（重试 1+2+3=6s），`:84` 同型 `2.0,30.0`，`:118` `0.0,5.0`；`ComponentInstallerTests.cs:94` `Random.Shared.Next(20000, 60000)` 随机端口（并行时可能撞端口）。Installer.UnitTests 15s 中有约 9s 是纯等待。
- 影响：CI 负载高时 `InRange(5.0, 30.0)` 不会挂，但 `0.0, 5.0` 会；随机端口在并行 xunit 下有 `HttpListenerException` 风险。
- 建议：把重试策略的退避时间注入（`TimeProvider`/可配置 delay），断言"调用次数+退避序列"而不是挂钟；端口改为 `TcpListener(IPAddress.Loopback, 0)` 取空闲端口。

### BT-08 ｜ P2 ｜ 弱断言三处

- 证据：`tests/LunarWard.UnitTests/GachaExcelExportTests.cs:105-106` 仅 `File.Exists` + `Length > 3000`；`ComponentInstallerTests.cs:281` `Assert.ThrowsAnyAsync<Exception>`；`ScopedAndConfigTests.cs:333` `Assert.ThrowsAny<Exception>`；`ComponentInstallerTests.cs:317` `Assert.True(result.Success || result.Errors.Count > 0)`（BT-06 里那条近乎恒真的断言）。
- 影响：Excel 导出的表头/列序/行数回归（用户可见的导出文件损坏）不会被发现；`ThrowsAny<Exception>` 会把 NRE/`InvalidOperationException` 当成"预期失败"。
- 建议：Excel 用例用 `MiniExcel.Query` 读回，断言行数、首行表头、某单元格值；异常断言收窄到具体类型（`HttpRequestException`、`IOException` 等）。

### BT-09 ｜ P2 ｜ 无 UI/XAML 冒烟进 CI，AVLN3001 的 33 个页面只靠手工脚本

- 证据：`src/LunarWard.App/LunarWard.App.csproj:8` 开启 `AvaloniaUseCompiledBindingsByDefault`、`:14` 压掉 AVLN3001（33 个 `Views/**/*.axaml` 不可 runtime load，属设计选择）；唯一验证手段是 `scripts/smoke-screenshots.sh`（隔离 `HOME`/XDG 下逐页 `--screenshot` 渲染 18 个页面），而 `ci.yml` 未调用它。
- 影响：页面构造抛异常、XAML 资源缺失、设置子页崩溃，都要靠人记得跑脚本才发现；同时因为没有 `Avalonia.Headless` 测试项目，UI 层零自动化。
- 建议：CI 增加"smoke-screenshots 至少 3 个代表页（launcher/gacha/settings:about）"的步骤（ubuntu + libx11 已装，但需 Xvfb 或 `--screenshot` 的 headless 通路，先本地验证）；中期引入 `Avalonia.Headless.XUnit`，把 18 页构造变成测试用例。

### BT-10 ｜ P2 ｜ 无 `.editorconfig` / analyzers / `dotnet format` 门禁

- 证据：仓库根无 `.editorconfig`、无 `Directory.Build.targets`、无 `Microsoft.CodeAnalysis.NetAnalyzers` 包；`ci.yml` 无 `dotnet format --verify-no-changes`，也无 `-p:RunAnalyzers=true`。唯一"静态检查"是 `scripts/check-parity.py`（只比文件存在性）。
- 影响：代码风格与 CA 规则全靠口口相传；配合 BT-02 的 NoWarn，等于分析器整体离线。
- 建议：加最小 `.editorconfig`（缩进/`var` 风格/`dotnet_diagnostic.IDE*`）+ CI 的 `dotnet format --verify-no-changes --severity warn`；先只对 `src/LunarWard.{Platform,Wine,Installer}` 三个新项目强制，避免一次性 3 万行噪声。

### BT-11 ｜ P2 ｜ 无 `global.json`，`LangVersion=preview` 放大 SDK 漂移

- 证据：仓库无 `global.json`；`Directory.Build.props:4-5` 为 `net10.0` + `LangVersion preview`；CI 用 `dotnet-version: 10.0.x`（`ci.yml:18`）取最新补丁，本机是 `10.0.302`。
- 影响：不同机器可能落到不同 SDK 特性级别（preview 语言特性随 SDK 变化），"本地能编 CI 不能编"类问题的常见来源。
- 建议：加 `global.json`（`"version": "10.0.302", "rollForward": "latestPatch"`），并把 `LangVersion` 收到 `latest`。

### BT-12 ｜ P3 ｜ 无测试结果产物、单平台单配置

- 证据：`ci.yml:31-35` 的 `dotnet test` 无 `--logger trx`、无 `actions/upload-artifact`；只有 `ubuntu-24.04` 一个 job、只 `Release`。
- 影响：失败时只能翻控制台日志；无法看用例历史趋势。
- 建议：加 `--logger "trx;LogFileName=unit.trx"` + 上传 artifacts（失败时也上传）。

### BT-13 ｜ P3 ｜ `check-parity.py` 只验文件存在，行数漂移仅告警

- 证据：脚本 docstring 明示"退出码 0 = 全部可解析文件都存在；行数漂移只告警，不失败"（`scripts/check-parity.py:8-10`）。
- 影响：`docs/08-UI-PARITY.md` 与代码的实际漂移不会被发现，表格会逐渐失真。
- 建议：保留现状（合理），但在 CI 输出里把漂移数汇总成一行摘要（"本次漂移 N 行"），让趋势可见。

## 4. 建议的修复批次

**批次 A：让门禁产生真实信号（1 天）**
1. `ci.yml` 追加 IntegrationTests 步骤；2. 全仓 `return;` 跳过改为 `Assert.Skip`；3. 追加 `-p:NoWarn=` 的告警计数步骤（只报告不阻塞）。
验收：`dotnet build LunarWard.slnx -v m && dotnet test LunarWard.slnx -c Release` 全绿且测试报表里"已跳过"数可见；故意注释掉一个 phase 断言时 IntegrationTests 变红。

**批次 B：撤掉可空性压制（1–2 天）**
`Directory.Build.props:15` 移除 `CS8600;CS8602;CS8625` → 修 6 处（BT-03 清单）→ CI 加 `dotnet build -c Release -warnaserror`。
验收：`dotnet build LunarWard.slnx -warnaserror -p:NoWarn="CS8618;CA1416;NU1900;AVLN3001"` 通过；`grep -c 'CS860' /tmp/build.log` 为 0。

**批次 C：安装器与游戏包覆盖（3–5 天）**
`ArchiveExtractor` / `SophonManifest` / `DiffPatcher` 各 3–5 个用例，全部离线。
验收：`dotnet test tests/LunarWard.Installer.UnitTests -c Release` 用例数从 31 增至 ≥45，且无网络访问（断网可过）。

**批次 D：可复现性（1 天）**
`IndexBaseUrl` 可注入；真实 wine 用例全部移到 IntegrationTests 并 env 闸；runner 路径改环境变量。
验收：在 `unshare -n`（无网络）下 `dotnet test LunarWard.slnx` 全绿；无 `cc` 的容器里测试报表显示"跳过 26"。

**批次 E：静态检查（0.5 天）**
`.editorconfig` + `global.json` + `dotnet format --verify-no-changes`（仅新项目）+ trx 上传。
验收：`git commit` 一个多余空行时 format 步骤变红；`dotnet --version` 在 CI/本地一致。

## 5. 不建议修的点

1. **710 条 CS8618 不逐条修**：主要来自从 Starward.Core 移植的 DTO（`HoYoPlay/GameConfig.cs`、`GithubUploader.cs` 等），一次性加 `required`/构造函数会大面积改结构且无行为收益；保留白名单，只在**新增**文件上通过批次 B 的 `-warnaserror` 约束。
2. **不给纯 DTO 补"序列化再反序列化"式测试**：`GameRecord/*` 的 324 个未提及类型多数是数据容器，加测试只增加维护成本；应把预算放在 BT-05 的解压/校验逻辑上。
3. **不把 `RealWineTests` 强行塞进 CI**：真 wine + mingw 交叉编译在 runner 上极慢且易碎；保持 env 闸（改为 `Assert.Skip` 显式可见）比强行默认执行更划算。
4. **不追求行覆盖率数字**：本仓库已有 `scripts/smoke-screenshots.sh` 这种"行为级"保障；为达到某个百分比而写 DTO 测试是负投资。
5. **不强行让 Avalonia 页面走 runtime loader**：`App.csproj:14` 的 AVLN3001 豁免有明确设计理由（页面 ctor 收 `PageContext`），代价是失去 `avares://` 加载能力；用 headless 构造测试替代即可，不要为此改页面构造签名。
6. **`CA1416` 不做逐条 `SuppressMessage`**：那是把问题藏到代码里；要么改走平台抽象，要么在 BT-04 里按项目级豁免并记录，其余应修复。
