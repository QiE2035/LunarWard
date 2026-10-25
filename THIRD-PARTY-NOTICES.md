# 第三方声明（THIRD-PARTY-NOTICES）

LunarWard 自身以 **MIT** 许可发布，全文见 [LICENSE](LICENSE)。

本文件列出所有随发行包分发或运行时链接的第三方组件及其许可。

## 核对方式

下表的 `<license>` 全部**逐条读自本机 NuGet 缓存里的真实 nuspec**，不是凭记忆填写：

```bash
tr -d '\r' < ~/.nuget/packages/<id>/<ver>/<id>.nuspec | grep -oiE '<license[^>]*>[^<]*</license>'
```

核对日期：**2026-10-23**；缓存来自 `dotnet restore`（`Directory.Packages.props`
中的版本，见该文件）。若将来升级包版本，**必须**重新核对本表。

包版本以 `Directory.Packages.props` 为准；下表标注实际解析到的版本。

---

## 一、移植来源（源码级）

| 组件 | 版本/来源 | 许可 | 我们的义务 |
|---|---|---|---|
| **Starward** | [Scighost/Starward](https://github.com/Scighost/Starward) `main` | **MIT** | 保留原始版权声明。`src/LunarWard.Core` 与 `src/LunarWard.Installer` 为其派生作品 |

> **Starward 版权声明必须保留。** 本项目 `src/LunarWard.Core`（236 个 `.cs`，
> 抽卡 / 便笺 / 公告 / HoYoPlay / SelfQuery / 元数据）与 `src/LunarWard.Installer`
> （`Starward.RPC/GameInstall` 的下载与校验逻辑）都是从 Starward 移植的，
> 属于 MIT 许可下的派生作品。上游版权归 **Scighost** 及 Starward 贡献者所有。
>
> 移植时的改动清单见 `docs/00-DECISIONS.md` D2 与 `docs/02-ARCHITECTURE.md` §三。
> 上游许可全文：<https://github.com/Scighost/Starward/blob/main/LICENSE>

---

## 二、随包分发的 NuGet 依赖

### 2.1 UI / Avalonia 全家桶

| 包 | 版本 | 许可 |
|---|---|---|
| `Avalonia` | 12.1.4 | **MIT** |
| `Avalonia.Desktop` | 12.1.4 | **MIT** |
| `Avalonia.Themes.Fluent` | 12.1.4 | **MIT** |
| `Avalonia.Controls.ItemsRepeater` | 12.0.0 | **MIT** |
| `Avalonia.Diagnostics`（仅 Debug） | 12.1.4 | **MIT** |
| `Avalonia.Skia`（传递） | 12.1.4 | **MIT** |
| `Avalonia.X11`（传递） | 12.1.4 | **MIT** |
| `Avalonia.FreeDesktop`（传递） | 12.1.4 | **MIT** |
| `Avalonia.FreeDesktop.AtSpi`（传递） | 12.1.4 | **MIT** |
| `Avalonia.Native`（传递） | 12.1.4 | **MIT** |
| `Avalonia.Win32`（传递） | 12.1.4 | **MIT** |
| `Avalonia.HarfBuzz`（传递） | 12.1.4 | **MIT** |
| `Avalonia.Remote.Protocol`（传递） | 12.1.4 | **MIT** |
| `Avalonia.BuildServices`（传递，仅构建） | 11.3.2 | **MIT** |
| `Avalonia.Angle.Windows.Natives`（传递，仅 Windows） | 2.1.27548.20260419 | **BSD-3-Clause**（ANGLE 自有 BSD 风格许可） |
| `HarfBuzzSharp`（传递） | 8.3.1.5 | **MIT** |
| `HarfBuzzSharp.NativeAssets.Linux`（传递） | 8.3.1.3 | **MIT** |
| `MicroCom.Runtime`（传递） | 0.11.6 | **MIT** |
| `Tmds.DBus.Protocol`（传递，Linux 托盘/通知） | 0.94.1 | **MIT** |

### 2.2 MVVM

| 包 | 版本 | 许可 |
|---|---|---|
| `CommunityToolkit.Mvvm` | 8.4.2 | **MIT** |

### 2.3 视频背景（LGPL 链路）

| 组件 | 版本 | 许可 |
|---|---|---|
| `LibVLCSharp` | 3.10.2 | **LGPL-2.1-or-later** |
| `LibVLCSharp.Avalonia` | 3.10.2 | **LGPL-2.1-or-later** |
| **`libvlc`（系统动态库，不随包分发）** | 本机 3.0.24 | **LGPL-2.1-or-later**（VLC core）；Arch 包 `libvlc` 元数据同时标注 `GPL-2.0-or-later` |

**LGPL 合规说明**：

- `LibVLCSharp` 是**动态链接** `libvlc.so` 的托管封装，我们不修改它，也不静态链接。
  因此 **LGPL-2.1+ 的传染性不适用于 LunarWard 自身**（LunarWard 保持 MIT）。
- `libvlc` 由**发行版**提供（Arch: `vlc` / `libvlc`；Debian: `libvlc5`；
  Fedora: `vlc-libs`），**LunarWard 的发行包不包含任何 VLC 二进制**。
  用户自行安装，属于运行时依赖而非分发物。
- 若用户启用视频背景，LunarWard 与 `libvlc` 之间是运行时动态加载关系。
  用户可自行替换系统 `libvlc` 版本，无需重新编译 LunarWard——这正是 LGPL 要求的
  "可替换性"，已满足。
- 若某个发行版要求随包分发 `libvlc`，则该发行包必须同时提供 LGPL 全文与
  `libvlc` 的完整源码获取方式，且**不得**使用 GPL 插件模块之外的静态链接形式。
  这是打包者（如 AUR 维护者）的责任，本仓库的 `packaging/` 不打包 VLC。

### 2.4 终端控件及其传递依赖

`Iciclecreek.Avalonia.Terminal` 的依赖树（读自其 nuspec `<dependencies>`）：

| 包 | 版本 | 许可 | 作用 |
|---|---|---|---|
| `Iciclecreek.Avalonia.Terminal` | 4.1.0 | **MIT** | Avalonia 终端控件本体 |
| `Porta.Pty` | 2.2.3 | **MIT** | 跨平台 PTY（Linux 走 `openpty`） |
| `XTerm.NET` | 3.0.0 | **MIT** | VT/ANSI 转义序列解析 |
| `SkiaSharp` | 3.119.4 | **MIT** | 字形光栅化 |
| `SkiaSharp.HarfBuzz` | 3.119.4 | **MIT** | 文本整形（CJK / 连字） |
| `SkiaSharp.NativeAssets.Linux` | 3.119.4 | **MIT** | 原生 `libSkiaSharp`（Linux） |
| `Wcwidth` | 4.0.1 | **MIT** | 东亚宽字符列宽计算 |
| `Unicode.net` | 2.0.0 | **MIT** | Unicode 类别数据 |
| `Microsoft.Windows.Console.ConPTY` | 1.24.260710001 | **MIT** | Windows ConPTY（Linux 上不加载） |

> `Unicode.net` 的 nuspec 写的是 `<license type="file">LICENSE</license>`，
> 没有 SPDX 表达式。已打开包内 `LICENSE` 文件确认正文为 **MIT**
> （Copyright (c) 2017 NeoSmart Technologies）。

### 2.5 数据库（抽卡记录，与 Starward 同款 SQLite）

| 包 | 版本 | 许可 |
|---|---|---|
| `Microsoft.Data.Sqlite` | 10.0.13 | **MIT** |
| `Microsoft.Data.Sqlite.Core`（传递） | 10.0.13 | **MIT** |
| `SQLitePCLRaw.bundle_e_sqlite3`（传递） | 2.1.13 | **Apache-2.0** |
| `SQLitePCLRaw.core`（传递） | 2.1.13 | **Apache-2.0** |
| `SQLitePCLRaw.lib.e_sqlite3`（传递，内含 SQLite 原生库） | 2.1.13 | **Apache-2.0**；SQLite 本体为 **Public Domain** |
| `SQLitePCLRaw.provider.e_sqlite3`（传递） | 2.1.13 | **Apache-2.0** |

> SQLite 原生库以 `e_sqlite3` 随包分发（SQLitePCLRaw 的 `lib.e_sqlite3` 提供 Linux
> `libe_sqlite3.so`），SQLite 本体为 Public Domain，不产生额外义务；SQLitePCLRaw
> 是 Apache-2.0，随包分发时需保留其 NOTICE/许可文本（NuGet 包内已带 `LICENSE`）。

### 2.6 配置 / 基础设施

| 包 | 版本 | 许可 |
|---|---|---|
| `Microsoft.Extensions.Configuration` | 10.0.11 | **MIT** |
| `Microsoft.Extensions.Configuration.Ini` | 10.0.11 | **MIT** |
| `Microsoft.Extensions.Configuration.Json` | 10.0.11 | **MIT** |
| `Microsoft.Extensions.Configuration.Binder` | 10.0.11 | **MIT** |
| `Microsoft.Extensions.Configuration.Abstractions`（传递） | 10.0.11 | **MIT** |
| `Microsoft.Extensions.Configuration.FileExtensions`（传递） | 10.0.11 | **MIT** |
| `Microsoft.Extensions.FileProviders.Abstractions`（传递） | 10.0.11 | **MIT** |
| `Microsoft.Extensions.FileProviders.Physical`（传递） | 10.0.11 | **MIT** |
| `Microsoft.Extensions.FileSystemGlobbing`（传递） | 10.0.11 | **MIT** |
| `Microsoft.Extensions.Primitives`（传递） | 10.0.11 | **MIT** |
| `Microsoft.Extensions.DependencyInjection`（传递） | 10.0.0 | **MIT** |
| `Microsoft.Extensions.DependencyInjection.Abstractions`（传递） | 10.0.0 | **MIT** |
| `Microsoft.Extensions.Logging`（传递） | 10.0.0 | **MIT** |
| `Microsoft.Extensions.Logging.Abstractions`（传递） | 10.0.0 | **MIT** |
| `Microsoft.Extensions.Options`（传递） | 10.0.0 | **MIT** |
| `Polly` | 8.8.0 | **BSD-3-Clause** |
| `Polly.Core`（传递） | 8.8.0 | **BSD-3-Clause** |

### 2.7 压缩与日志

| 包 | 版本 | 许可 |
|---|---|---|
| `ZstdSharp.Port` | 0.8.8 | **MIT** |
| `Serilog.Extensions.Logging` | 10.0.0 | **Apache-2.0** |
| `Serilog.Sinks.File` | 7.0.0 | **Apache-2.0** |
| `Serilog`（传递） | 4.2.0 | **Apache-2.0** |

### 2.8 Markdown 渲染

| 包 | 版本 | 许可 |
|---|---|---|
| `Markdig` | 1.4.2 | **BSD-2-Clause** |

### 2.9 仅测试（不随发行包分发）

以下包只被 `tests/` 引用，**不会出现在 `packages/` 产物或发布目录中**：

| 包 | 版本 | 许可 |
|---|---|---|
| `xunit` | 2.9.3 | **Apache-2.0** |
| `xunit.core` / `xunit.assert` / `xunit.extensibility.*` | 2.9.3 | **Apache-2.0** |
| `xunit.analyzers` | 1.18.0 | **Apache-2.0** |
| `xunit.abstractions` | 2.0.3 | **Apache-2.0**（nuspec 无 SPDX `<license>`，仅有 `licenseUrl` 指向 xunit 的 `license.txt`） |
| `xunit.runner.visualstudio` | 3.1.5 | **Apache-2.0** |
| `Microsoft.NET.Test.Sdk` | 18.0.1 | **MIT** |
| `Microsoft.CodeCoverage` | 18.0.1 | **MIT** |
| `Microsoft.TestPlatform.ObjectModel` / `.TestHost` | 18.0.1 | **MIT** |

---

## 三、不分发的组件

**LunarWard 不打包、不分发、不捆绑任何游戏补丁程序或注入器二进制。**
依据 `docs/00-DECISIONS.md` **D11**（不打包补丁二进制）与 **D6**（不预置具体项目）。

| 组件 | 许可 | 我们不做什么 | 用户需要做什么 |
|---|---|---|---|
| **Hk4eTouch** | **AGPL-3.0** | ❌ 不分发、不下载、不自动安装、不内置参数模板 | 自行获取，用「外部程序」面板手选文件与参数 |
| **ZZZ-touch-unlocker** | **无 LICENSE 文件** | ❌ 同上；无许可证意味着**默认保留所有权利**，任何分发都没有法律依据 | 自行获取，手选文件与参数 |
| an-anime-team 组件（wine runner / dxvk） | 各自上游许可 | ❌ 不随包分发；只提供索引与下载机制 | 运行时自行下载到 `~/.local/share/lunarward/` |
| Starward 本体（Windows 版） | MIT | ❌ 不分发可执行文件 | 仅作为**源码移植来源** |

**为什么这条是硬约束**：

- `Hk4eTouch` 是 **AGPL-3.0**。AGPL 的传染性比 GPL 更强（网络提供服务也触发）。
  一旦我们分发它的二进制或把它链接进来，整个 LunarWard 都可能被要求以 AGPL 发布，
  与 MIT 定位冲突。
- `ZZZ-touch-unlocker` 仓库**连 LICENSE 文件都没有**。在无明确许可的情况下，
  默认适用著作权法"保留所有权利"，**分发即侵权**。
- 保持"独立程序、子进程调用"的边界，既规避分发风险，也让用户自己掌握
  版本与来源（补丁的特征码随游戏更新频繁变化，本项目不感知也不需要感知）。

**机制上如何保证**：

1. 外部程序面板要求用户**手动选择文件**（`docs/04-FEATURES.md` §1.8），
   没有任何预置条目、没有推荐下载链接、没有自动更新逻辑。
2. 只做**非强制的启发式提示**（文件名含 `inject` / `touch` / `hook` 时建议
   "伴随启动"），不改变任何文件、不下载任何东西。
3. 发布包与 AUR 包都**不包含** `*.exe` / `*.dll` 补丁文件。
   打包脚本（`packaging/PKGBUILD`）只装本仓库构建出来的托管程序集。

---

## 四、许可全文位置

| 许可 | 全文 |
|---|---|
| MIT | <https://opensource.org/licenses/MIT> |
| BSD-2-Clause | <https://opensource.org/licenses/BSD-2-Clause> |
| BSD-3-Clause | <https://opensource.org/licenses/BSD-3-Clause> |
| Apache-2.0 | <https://www.apache.org/licenses/LICENSE-2.0> |
| LGPL-2.1-or-later | <https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html> |
| AGPL-3.0（仅提及，不分发） | <https://www.gnu.org/licenses/agpl-3.0.html> |

各 NuGet 包内的完整许可文本随包安装于：
`~/.nuget/packages/<id>/<ver>/`（`LICENSE` / `LICENSE.md` / `LICENSE.txt`）。
发布产物中，若某包要求随二进制附带许可文本，应把它复制到
`/usr/lib/lunarward/licenses/` 并在本文件登记。

---

## 五、变更流程

新增任何 NuGet 依赖时：

1. `dotnet add package` 后**先**读 `<license>`，再写进本文件；
2. 若许可为 **GPL / AGPL / 无许可 / SSPL / Commons Clause**，默认**拒绝**引入，
   需要需求方书面确认并单独记录理由；
3. 若许可为 **LGPL**，确认是**动态链接**且**不修改库本体**，并把合规说明补到 §2.3；
4. 更新本文件顶部的核对日期。
