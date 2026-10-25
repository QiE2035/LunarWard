# 一致性审计

- 审计对象：`/home/qie2035/projects/lunarward`（.NET 10 + Avalonia 11），上游 `/home/qie2035/projects/csharp/Starward`。
- 方法：全仓库静态扫描（453 个 `src/**/*.cs`，排除 `obj/`、`bin/`；注意 BOM，必须用 `utf-8-sig` 读）+ `dotnet build` / `dotnet test` 实测 + `git show` 抽查 + 上游源码行号自动比对。
- 基线（本次实测，2026 审计时点）：`dotnet build LunarWard.slnx` → **0 warning / 0 error**（4.24s）；`dotnet test LunarWard.slnx` → **531**（Unit）+ **31**（Installer）+ **27**（Integration）= **589**，失败 0。
- 本次审计**未修改任何代码，未提交任何 commit**。

## 1. 结论摘要

1. **命名空间基本健康**：453 个文件仅 18 处与目录不一致，其中 10 处是 `ViewModels/Starward/Settings/` 目录的既有约定（全部声明 `...ViewModels.Starward`），真正需要处理的只有 7 处（6 个 `ShiyuDefense/V2/` 文件 + `Services/CaptureMode.cs`）。跨项目同名类型 0 处。
2. **命名风格的主要债是移植遗留，不是新代码**：私有字段 764 个带 `_` 前缀、129 个不带（上游 Starward 自己也用 `_camel`），集中在 5 个文件；常量非 PascalCase 113 处集中在 `GameRegistry`/`GameBiz` 的区服标识；`Async` 后缀误用全仓库只有 1 处——说明新代码的规范执行得比老代码好。
3. **文档数字「大部分对、局部过期」**：`README.md:12` 的 589/531/31/27 完全正确、`docs/05-ROADMAP.md:256` 的 0 warning 也正确；但 `docs/08-UI-PARITY.md:106` 与 `docs/07-UI-REPLICA.md:196` 仍写「348（290 单测 + 31 + 27）」，且 `docs/05-ROADMAP.md:258` 的拆分算式 `I1–I13 + S1–S5 + 真 wine 2` 相加只有 20，与实际 27 不符。
4. **文档与代码最硬的矛盾在渠道**：`docs/09-LINUX-DIVERGENCES.md:14` 声明「渠道 SDK 分组…只保留官方区服」，但 Bilibili 渠道已从校验、`config.ini` 重写到 UI 选择器全链路实现（`GameBiz.cs:38/47/53`、`ChannelValidator.cs`、`SettingsViewModel.cs:43`）。同一句错误也出现在 `docs/08-UI-PARITY.md:29`。
5. **注释滞后于实现是本仓库最大的一致性风险**：6 处「尚未移植 / 未接 / 暂未移植」注释对应的功能其实已实现（Excel 导出、InAppToast、更新设备指纹、13 个战绩子页、安装/更新界面），其中 4 条是**直接给用户看的 AppStrings 文案**，会让用户以为功能不存在。
6. **i18n 结构对齐全、实现有隐患**：zh-CN 与 en-US 各 1031 个键、键集完全一一对应、无未定义键，但两个字典各有 **20 个重复键**，其中中文 6 组、英文 11 组的最终值取决于书写顺序（含 `" average"`、`" count"` 前导空格），索引器初始化不报错、静默覆盖。提交信息侧，最近 40 条全部是 conventional 规范格式，但抽查 2 条标题/正文与实质改动不符。

## 2. 问题清单

严重度：P0 阻断 / P1 应尽快修 / P2 计划内修 / P3 顺手修。本次 **P0 = 0 条**。

### CO-01｜P1｜「只保留官方区服」与代码矛盾

- 证据：`docs/09-LINUX-DIVERGENCES.md:14`「渠道 SDK 分组（腾讯/哔哩哔哩）| 注册表读取渠道分组 | 只保留官方区服 | 渠道 SDK 不提供 Linux 客户端」；`docs/08-UI-PARITY.md:29`「…Windows 专属卸载器/CMD 启动/渠道 SDK 分组等 —— 这些在 Linux 上禁用而非伪造」。
- 实际：`src/LunarWard.Core/GameBiz.cs:38` `hk4e_bilibili`、`:47` `hkrpg_bilibili`、`:53` `nap_bilibili`；`src/LunarWard.Games/ChannelValidator.cs` 已实现 B 站渠道校验、`config.ini` 重写（`channel=14` / `cps=bilibili` / `sub_channel=0`）与插件缺失判定；`src/LunarWard.App/Services/LauncherService.cs:245-273` 在启动前 `ValidateChannel`/`ApplyChannel`；`src/LunarWard.App/ViewModels/SettingsViewModel.cs:43` `Channels = ["Official", "Bilibili"]` 是用户可见的渠道选择器。
- 影响：文档指导后续开发「不做渠道」，与已付出的实现相反，容易被误删。
- 建议：把 09:14 改为「渠道分组：已支持 Bilibili（校验+config 重写+插件检测），腾讯渠道未做」，并修正 08:29 的清单；同时把 08:42「未移植：…渠道 SDK 包体分组」限定为「渠道包体分组仍未移植」。

### CO-02｜P1｜AppStrings 20 组重复键导致静默覆盖

- 证据：`src/LunarWard.App/Localization/AppStrings.cs`，zh-CN 字典 `25..1081` 行、en-US `1084..2140` 行，各 1051 条、1031 唯一。值不同（后者覆盖前者）的：zh `Gacha.UpdateSavedUrl` `:611` vs `:1063`、`Gacha.UpdateAll` `:612` vs `:1064`、`Gacha.AuthkeyTimeout` `:618`「总是出现 Authkey Timeout？」 vs `:1069`「Authkey 有效期为 24 小时…」、`Gacha.RestartAfterDeletingCache` `:619` vs `:1070`、`Gacha.LanguageDesc` `:623`「…比如 zh-cn 。」 vs `:1073`「…跟随游戏中的语言设置…」、`Gacha.UidPlaceholder` `:655`「UID」 vs `:1061`「请选择 UID」；en `StartGameButton.Verify` `:1238`「Verifying」 vs `:1240`「Verify」、`Gacha.Settings` `:1695`「Setting」 vs `:2125`「Settings」、`Gacha.Average` `:1733`「Average」 vs `:2114`「 average」（**前导空格**）、`Gacha.Stats` `:1734`「Stats」 vs `:2115`「 count」、`Gacha.LanguageDesc` `:1704` vs `:2132`、`Gacha.AuthkeyTimeout` `:1699` vs `:2128`、`Gacha.RestartAfterDeletingCache` `:1700` vs `:2129`、`Gacha.DeleteCacheFolder` `:1701` vs `:2130`、`Gacha.NoRecords` `:1731` vs `:2133`、`Gacha.UidPlaceholder` `:1736` vs `:2120`、`Gacha.UpdateRecords`/`UpdateSavedUrl`/`UpdateAll`/`OtherSettings` 各一组。
- 影响：用户看到哪一条完全取决于书写顺序；`" average"`/`" count"` 会被拼出多余空格；C# 索引器初始化不报错，因此不会被 `dotnet build` 发现。
- 建议：同键只保留一处；文案取语义完整者（`AuthkeyTimeout` 取 `:1069`、`LanguageDesc` 取 `:1073`、`UidPlaceholder` 取 `:1061`）。验收：解析两个字典断言 `len(dict) == len(unique keys) == 1031` 且 zh/en 键集相等。

### CO-03｜P1｜面向用户的 AppStrings 文案宣称「没有安装界面」，实际已实现

- 证据：`AppStrings.cs:973` `["Settings.Download.InstallPathUnavailable"]`「LunarWard 暂未提供游戏安装/更新界面…」、`:976` `["Settings.Download.HardLinkUnavailable"]`「…下载/安装界面尚未移植…」，en 侧 `:2032`/`:2035` 同义。
- 实际：`src/LunarWard.App/Views/Starward/InstallGameDialog.axaml` 存在；`Program.cs:131-135` 有 `--install-dialog`/`--install-progress`/`--install-real`/`--install-update`；`docs/08-UI-PARITY.md:42` 自己写着「安装/更新 UI 已补：`InstallGameDialog`（560 宽亚克力卡：路径可改/音频语言分段…）」。
- 影响：用户点开设置会被告知功能不存在，与启动器的「安装」按钮行为冲突。
- 建议：改写为说明真实限制（例如「默认安装路径由安装对话框内选择」），或直接删除该 infobar 与 `SettingDownloadView.axaml:22` 的视图注释（同条）。

### CO-04｜P1｜「尚未移植 / 未接」注释与实现相反（5 处）

| 位置 | 注释文字 | 实际实现 |
|---|---|---|
| `src/LunarWard.Launch/GachaExport.cs:15` | 「Excel 导出（MiniExcel 模板）暂未移植。」 | `src/LunarWard.Launch/GachaExcelExport.cs:23` 已实现（`:26`/`:29` 模板路径）；`GachaPageViewModel.cs:504` 已调用，`:469` `IsExcelAvailable`；CLI `Program.cs:154 --gacha-excel` |
| `src/LunarWard.App/Views/Starward/Pages/ScreenshotPage.axaml:222` | 「Starward 用 InAppToast，ToastHost 尚未移植」 | `src/LunarWard.App/Services/InAppToast.cs:33` 已实现，`ScreenshotPageViewModel.cs:345` 已调用 |
| `src/LunarWard.App/Views/Starward/Pages/ToolboxPage.axaml:13` | 「PaneFooter：更新设备指纹（LunarWard 未接 → 保留位置、禁用）」 | `ToolboxPage.axaml:254-256` 绑定 `UpdateDeviceFingerprintCommand`，`ToolboxPageViewModel.cs:359-392` 已实现（`CanUpdateDeviceFingerprint => _server == "cn"`，`HyperionClient.GetDeviceFpAsync()`） |
| `ToolboxPage.axaml:14-15`、`:283`、`:286`；`ToolboxPageViewModel.cs:24-25` | 「战绩子页尚未移植 / 只移植了深境螺旋，其余仍是占位」 | `ToolboxPageViewModel.cs:128-142` `PortedTags` 已含 13 个子页（SpiralAbyss、ImaginariumTheater、StygianOnslaught、InterKnotMonthlyReport、DeadlyAssault、ShiyuDefense、TravelersDiary、ForgottenHall、TrailblazeCalendar、SimulatedUniverse、PureFiction、ApocalypticShadow、ChallengePeak）；`docs/08-UI-PARITY.md:55` 也写「十三个子页已 1:1 移植」 |
| `src/LunarWard.App/Views/Starward/Settings/SettingDownloadView.axaml:22` | 「安装/更新界面尚未移植 → 定位按钮/文件夹胶囊/硬链接开关/速度数字框…全部保留并禁用」 | 安装/更新 UI 已实现（见 CO-03 证据） |

- 影响：维护者按注释会重复实现或误判完成度；`ToolboxPage.axaml:286` 的占位面板虽仍作为兜底存在（`ToolboxPageViewModel.cs:122 ShowNotPortedHint`），但注释描述的「大部分子页是占位」已不成立。
- 建议：逐条改为「已移植（实现于 X）」，保留 `AnnotatedScrollBar`（`ScreenshotPage.axaml:20`）与 `rpc`（`StartupCommands.cs:18`）这两条**仍然真实**的未移植说明。

### CO-05｜P1｜测试数字过期 + 拆分算式与总数不符

| 位置 | 文档写 | 实际 |
|---|---|---|
| `docs/08-UI-PARITY.md:106` | 348 通过（290 单测 + 31 安装器 + 27 集成） | **589**（**531** + 31 + 27） |
| `docs/07-UI-REPLICA.md:196` | 「当前 348：290 单测 + 31 安装器 + 27 集成」 | 589 / 531 / 31 / 27 |
| `docs/05-ROADMAP.md:258` | 「27 个集成测试（I1–I13 + S1–S5 + 真 wine 2 个）」 | 13 + 5 + 2 = 20 ≠ 27（实际构成：`PhaseEngineTests.cs` 14、`ShellTests.cs` 5、`AcceptanceTests.cs` 6、`RealWineTests.cs` 2） |
| `README.md:12` | 589（531 + 31 + 27），0 warning / 0 error | ✔ 一致 |
| `docs/05-ROADMAP.md:256-257` | 0 warning / 0 error；531 + 31 = 562 | ✔ 一致 |

- 建议：三处数字统一改为 589/531/31/27，并把 258 行的括号改为「I1–I13（含 I4a/b/c）×14 + 验收 6 + S1–S5 + 真 wine 2」。

### CO-06｜P2｜`08-UI-PARITY.md:42` 单格自相矛盾，`check-parity.py` 阈值掩盖漂移

- 证据：`docs/08-UI-PARITY.md:42` 同一单元格先写「安装/更新 UI 已补：`InstallGameDialog`…」（已实现），末尾又列「未移植：卸载游戏…、渠道 SDK 包体分组、背景音量」，读者无法判断当前状态；此单元格是 CO-03 那条错误文案的来源。
- 另：`scripts/check-parity.py:54` 仅在 `|实际−文档|/文档 > 0.5` 时告警，实测 33 个带行数声明的单元格里 **21 个已漂移**却全部静默通过。例如 `SettingFileManageView.axaml` 文档 400 / 实际 296（−26%）、`Pages/Record/ShiyuDefensePage.axaml` 760/1037（+36%）、`SettingGeneralView.axaml` 160/198（+24%）、`NavigationRail.axaml` 129/156（+21%）、`Pages/ToolboxPage.axaml` 430/331（−23%）、`SettingScreenshotView.axaml` 350/275（−21%）；仍准确的只有 `MainWindow.axaml`(45)、`Pages/SettingPage.axaml`(160) 等 12 个。
- 建议：把阈值降到 0.15 并在文档里改为「约 N 行」或直接删掉行数列；02/03 期先把 21 个数字补齐。

### CO-07｜P2｜命名空间与目录不一致（18 处，含 1 处约定）

- 证据：`src/LunarWard.Core/GameRecord/ZZZ/ShiyuDefense/V2/` 下 6 个文件声明 `namespace LunarWard.Core.GameRecord.ZZZ.ShiyuDefense`（缺 `.V2`）：`ShiyuDefenseInfoV2.cs`、`ShiyuDefenseV2Brief.cs`、`ShiyuDefenseV2FifthLayerChallengeInfo.cs`、`ShiyuDefenseV2FifthLayerDetail.cs`、`ShiyuDefenseV2FourthLayerChallengeInfo.cs`、`ShiyuDefenseV2FourthLayerDetail.cs`；`src/LunarWard.App/Services/CaptureMode.cs` 声明 `namespace LunarWard.App`（应为 `.Services`）；`src/LunarWard.Core/GlobalUsing.cs` 无命名空间（只有 `global using`，**合法**，不计问题）。
- 另有 `src/LunarWard.App/ViewModels/Starward/Settings/` 全部 10 个文件声明 `namespace LunarWard.App.ViewModels.Starward`——全仓库无任何文件使用 `...Starward.Settings`，属**内部统一的事实约定**（建议保留并在 README/ARCHITECTURE 里写明，而不是逐个改）。
- 跨项目同名类型：0（已按 class/struct/enum/interface/record 名归并比对 7 个项目）。
- 建议：V2 目录要么加 `.V2`、要么把文件上移一层；`CaptureMode.cs` 改 `.Services`（无跨项目引用风险）。

### CO-08｜P2｜私有字段 `_camel` 不一致（129 处 / 66 文件，5 个文件占 48）

| 文件 | 数量 | 例子 |
|---|---|---|
| `src/LunarWard.App/ViewModels/SettingsViewModel.cs` | 18 | `:56` `private string? selectedDownloadableWine`、`:60` `componentStatus`、`:121` `language` |
| `src/LunarWard.App/ViewModels/DailyNoteViewModel.cs` | 13 | `:68` `cookie`、`:72` `uid`、`:76` `region` |
| `src/LunarWard.App/ViewModels/GachaViewModel.cs` | 6 | `:60` `gachaUrl`、`:64` `statusText`、`:68` `errorText` |
| `src/LunarWard.App/ViewModels/Starward/GenshinBeyondGachaPageViewModel.cs` | 6 | `:334` `IBrush Rarity5`、`:336` `Rarity4`、`:338` `Rarity3` |
| `src/LunarWard.App/ViewModels/Starward/ScreenshotPageViewModel.cs` | 5 | `:33` `TimeSpan FeedbackDuration`、`:149` `totalCount`、`:154` `totalBytes` |
| `src/LunarWard.Installer/GameInstallContext.cs` | 4 | `:142`/`:144`/`:146` `lastNetworkBytes`/`lastStorageReadBytes`/`lastStorageWriteBytes` |

- 说明：上游 Starward 自身也使用 `_camel`（`/home/qie2035/projects/csharp/Starward/src/Starward/Features/Background/AppBackground.xaml.cs:455` `private bool _needToInstallVp9VideoExtension`），因此这些无前缀字段是**移植早期遗留**而非上游风格。
- 建议：按文件分批机械重命名（`Rider rename_refactoring` / IDE 重命名），每批一个 commit；不要与功能改动混在一起。

### CO-09｜P2｜常量名非 PascalCase（113 处 / 14 文件）

- 证据：`src/LunarWard.Core/GameRegistry.cs` 36 处（`:8` `LauncherPath_hk4e_cn` 等路径模板）、`src/LunarWard.Core/GameBiz.cs` 19 处（`:23` `bh3` 等区服标识）、`src/LunarWard.Core/Gacha/GachaLogClient.cs` 16 处（`:13` `WEB_CACHE_PATH_YS_CN`）、`src/LunarWard.Core/GameRecord/GameRecordClient.cs` 9 处（`:46` `X_Request_With`）、`src/LunarWard.Core/Metadata/MetadataClient.cs` 5 处（`:12` `API_PREFIX_CLOUDFLARE`）、`src/LunarWard.Platform/Unix/Libc.cs` 5 处（`:12` `PATH_MAX`，与 libc 同名，可接受）。
- 建议：区服标识（`GameBiz.bh3` 等）属于**序列化数据值**，与上游一致，建议明确豁免；只改 `WEB_CACHE_*`、`API_PREFIX_*`、`X_Request_With` 这类内部常量。

### CO-10｜P2｜提交 4f62ead 标题与 diff 不符

- 证据：`4f62ead docs(readme): 自检表补 --toast-test，冒烟脚本说明更新为 17 张`；`git show --stat 4f62ead` 只有 `README.md` 3 增 3 删，diff 仅改「逐页冒烟」段落；`grep -rn "toast-test" README.md docs/` 无结果，`git log -S"toast-test" -- README.md` 为空 → 标题声称的「补 --toast-test」并未发生（该开关确实存在：`src/LunarWard.App/Program.cs:103`）。
- 建议：不必重写历史，补一条 `docs(readme): 补 --toast-test 等未文档化开关`，或在 README 开关表中补齐。

### CO-11｜P2｜提交 7ef4ffb 正文与代码规模不符

- 证据：`7ef4ffb chore(comments): 纠正三处『尚未移植』的过时注释…` 正文写「ToolboxPageViewModel：5 个战绩子页已移植」，而代码 `ToolboxPageViewModel.cs:128-142` 已是 13 个；该提交只改了 3 个 .cs，视图层注释（`ToolboxPage.axaml:13/14/283/286`、`GachaExport.cs:15`、`SettingDownloadView.axaml:22`）未一并修——即 CO-03/CO-04 的残留。
- 建议：后续修 CO-04 时用一条 commit 覆盖全部 6 处，标题写「修正 6 处过时『尚未移植』注释（Excel/Toast/设备指纹/13 子页/安装界面）」。

### CO-12｜P2｜代码有、文档没写的开关

- 证据：`src/LunarWard.App/Program.cs` 中 `--toast-test` `:103`、`--background-bar` `:129`、`--drag-overlay` `:130`、`--uigf-export` `:156`、`--expand-rail` `:163` 均未出现在 `README.md` 的开关表；文档提到的 `--uigf-window` 确实存在（`Program.cs:148`），文档与 CLI 其余部分一致（文档里的 `--squash`/`--prefix`/`--format` 等属 git/tar/dotnet/wine 参数，不计）。
- 建议：README 开关表补 5 行，或加一句「自检开关见 `Program.cs` 注释块」。

### CO-13｜P2｜「公告卡 / 动态背景」两个开关只写不读

- 证据：`src/LunarWard.App/Views/Starward/Settings/SettingGeneralView.axaml:183` 与 `AppStrings.cs:872`（en `:1931`）写「这两个开关写入 config.json，但启动器页的读取尚未接入，切换后不会立即生效」——**核实为真**：`show_announcements` 只在 `SettingGeneralViewModel.cs:128-130` 写入，全仓库无读取点；`Config.Launcher.VideoBackground` 只在 `SettingGeneralViewModel.cs:161` 写、`GameLauncherSettingDialog.axaml.cs:153/915` 读写本对话框自己的开关，而背景选择逻辑 `ShellViewModel.cs:641` 直接取 `items.FirstOrDefault(x => x.IsVideo)`，未参考该配置。
- 但注释**低估**了问题：`SettingGeneralViewModel.cs:62` 把 `Announcements` 硬编码为 `true` 且从不读回 `show_announcements`，所以重启后开关回弹为开。
- 建议：要么接上读取（推荐，两处都很小），要么把注释与文案改为「当前不生效，且每次启动都会重置为开」。

### CO-14｜P3｜事件命名不统一（35 个事件中 7 个非 `XxxRequested`/过去式）

- 证据：`src/LunarWard.App/ViewModels/Starward/DeleteGachaLogDialogViewModel.cs:60` `RequestClose`（应为 `CloseRequested`，与 `GachaPageViewModel.cs:418` `ExportJsonRequested`、`GameLauncherPageViewModel.cs:630` `InstallDialogRequested` 等 28 个 `XxxRequested` 风格不一致）；同类还有 `LauncherService.cs:68` `StateChanged`、`:71` `Completed`、`GameInstallService.cs:36`/`GameInstallController.cs:48` `TaskStateChanged`、`ParentProcessWatcher.cs:34` `ParentExited`、`Uigf4GachaWindowViewModel.cs:351/418` `CheckedChanged`、`ImageViewWindow2ViewModel.cs:147` `GallerySelectionChanged`/`:219` `ImageLoaded`——这些属「状态变化」语义，可接受，但建议在 ADR 里写明「命令请求用 `XxxRequested`，状态广播用 `XxxChanged`」。
- 建议：只改 `RequestClose` → `CloseRequested`（1 处 + 引用）。

### CO-15｜P3｜`Async` 后缀用在同步方法

- 证据：`src/LunarWard.App/ViewModels/Starward/SelfQueryPageViewModel.cs:533` `public void DeleteCurrentMonthAsync()`——返回 `void`、无 `await`；同文件 `:526` 已有 `[RelayCommand] private void DeleteCurrentMonthData()` 触发 `DeleteCurrentMonthRequested`。
- 建议：改名为 `DeleteCurrentMonth()`（它确实是同步的本地删除）。

### CO-16｜P3｜中文文案全/半角括号混用（12 条技术词）

- 证据：`AppStrings.cs` zh 侧 12 个值不含中文，均在技术名词处，且全/半角括号不一致：`:73`「FSR（WINE_FULLSCREEN_FSR）」全角、`:378`「cookie（ltoken / ltuid / cookie_token …）」全角、`:349`「cookie」/`:350`「UID」/`:631`「Excel」/`:655`「UID」/`:749`「GameMode」/`:752`「MangoHud」/`:755`「gamescope」/`:769`「prefix」纯英文（可接受）。en 侧无 CJK（已校验）。
- 建议：统一为「中文用全角括号、纯技术串保留原文」，或至少同一页内一致。

## 3. 需要人工确认的判断

1. **渠道**：`docs/08-UI-PARITY.md:42` 的「未移植：…渠道 SDK 包体分组」究竟指「渠道包体（B 服安装包）分组」还是「整个渠道体系」？代码里 Bilibili 渠道已通（CO-01 证据），若指的是前者，只需改措辞；若指的是后者，文档对、代码多做了——请确认哪个是意图。
2. **`docs/03-BUILD-TEST.md` 不存在**：任务书把它列为基线文档，但 `docs/` 下只有 `03-LAUNCH-ENGINE.md`，且全仓库无任何文件引用 `03-BUILD-TEST.md`。是「应新建该文档」还是「任务书笔误、实际应为 03-LAUNCH-ENGINE.md」？
3. **`ShiyuDefense/V2/` 命名空间**：V1 与 V2 是否共用 `LunarWard.Core.GameRecord.ZZZ.ShiyuDefense` 命名空间、靠类型名后缀 `V2` 区分？若是刻意压平，请在 `GlobalUsing`/文档里注明；若不是，应加 `.V2`。
4. **`ViewModels/Starward/Settings/` 的命名空间**：10 个文件统一声明 `...ViewModels.Starward` 而目录叫 `Settings`。改命名空间会牵动 10 个文件 + 所有引用，收益仅为「目录=命名空间」的字面一致。建议明确写成约定而不是重构。
5. **CO-13 两个开关**：是「先落盘、后续接线」（则保留注释并按 CO-13 建议补读取），还是「本轮不做、删掉开关」（则删 UI 与文案）？
6. **提交信息口径**：`chore(comments)` 类提交是否要求「正文描述的规模与代码同步」？7ef4ffb 写了 5 个子页而代码已 13 个——是提交时已过时，还是有意只记录增量？这决定要不要为提交信息加校验。

## 4. 建议修复批次

顺序原则：先改「读者最信的数字」，再改「会误导人的注释」，最后才是机械重命名（重命名会污染 `git blame`，放最后且单独 commit）。

### 批次 A：文档数字与措辞（预计 30 分钟，零代码风险）

1. `docs/08-UI-PARITY.md:106`、`docs/07-UI-REPLICA.md:196` → 589（531 + 31 + 27）；`docs/05-ROADMAP.md:258` 拆分算式改为「I1–I13 系列 14 + 验收 6 + S1–S5 + 真 wine 2 = 27」。
2. `docs/09-LINUX-DIVERGENCES.md:14`、`docs/08-UI-PARITY.md:29`/`:42` 的渠道与安装 UI 措辞（见 CO-01、CO-06）。
3. `scripts/check-parity.py:54` 阈值 `0.5` → `0.15`，重跑后按输出补齐 21 个行数。

验收：
```bash
grep -rn "348\|290 单测" docs/*.md README.md            # 期望无输出
grep -rn "只保留官方区服" docs/*.md                      # 期望无输出
python3 scripts/check-parity.py --upstream /home/qie2035/projects/csharp/Starward  # 期望告警清零或已修
```

### 批次 B：注释与用户文案（预计 1–2 小时）

1. 按 CO-04 表逐条改 6 处注释；CO-03 改 4 条 AppStrings（zh/en 各 2）。
2. CO-13：接上 `show_announcements` 与 `VideoBackground` 的读取，或把 `AppStrings.cs:872/1931` 改为「不生效且每次启动重置为开」。

验收：
```bash
grep -rn "尚未移植\|暂未移植\|LunarWard 未接" src/ --include=*.cs --include=*.axaml
# 期望只剩：ScreenshotPage.axaml:20 AnnotatedScrollBar、StartupCommands.cs:18 rpc、
#           ToolboxPageViewModel.cs:122 的兜底逻辑注释
dotnet build LunarWard.slnx    # 0 warning / 0 error
```

### 批次 C：i18n 去重（预计 1 小时）

1. 删除 20 组重复键的较早一份（保留语义完整者，见 CO-02 建议）。
2. 加一个仓库内脚本（如 `scripts/check-i18n.py`）纳入验收：两字典键集相等、无重复键、en 无 CJK、zh 允许技术词白名单。

验收：
```bash
python3 scripts/check-i18n.py   # 期望：zh 1031 / en 1031 / 重复 0 / 缺键 0
grep -c '\["Gacha.Average"\]' src/LunarWard.App/Localization/AppStrings.cs   # 期望 2（zh+en 各一）
dotnet test LunarWard.slnx      # 589 全绿
```

### 批次 D：命名（预计半天，必须独立 commit）

1. CO-07：`CaptureMode.cs` → `.Services`；`ShiyuDefense/V2/` 按人工确认结论处理；`Settings/` 目录保持现状并在文档写明约定。
2. CO-08：按文件分批重命名（`SettingsViewModel` 18 → `DailyNoteViewModel` 13 → `GachaViewModel` 6 → `GenshinBeyondGachaPageViewModel` 6 → `ScreenshotPageViewModel` 5 → `GameInstallContext` 4 → 其余 67），**每批一个 commit**，不改逻辑。
3. CO-09：只改内部常量（`WEB_CACHE_*`、`API_PREFIX_*`、`X_Request_With`），区服标识与 `Libc` 常量豁免。
4. CO-14/CO-15：`RequestClose` → `CloseRequested`；`DeleteCurrentMonthAsync` → `DeleteCurrentMonth`。

验收：
```bash
dotnet build LunarWard.slnx     # 0 warning / 0 error
dotnet test LunarWard.slnx      # 589 全绿（重命名不应改变任何测试数量）
git log --oneline -6            # 期望 6 个独立 chore/refactor commit，不混功能
```

### 批次 E：提交信息习惯（长期）

- 建议加一个 `commit-msg` 钩子或 CI 检查：conventional 前缀 + 正文规模与 `git show --stat` 一致（至少要求「改了 N 个文件」与 stat 相符）。CO-10、CO-11 两类问题靠此防止复发。
