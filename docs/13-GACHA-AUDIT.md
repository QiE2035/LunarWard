# 抽卡 / 自助查询簇移植审计（task-14）

> 审计对象：`src/LunarWard/Features/Gacha/**`、`src/LunarWard/Features/SelfQuery/**`（共 45 个文件）
> 上游基准：`/home/qie2035/projects/csharp/Starward/src/Starward/Features/{Gacha,SelfQuery}/**`
> 审计人：gacha-port（task-8 / task-14），日期 2026-10-26

## 一、方法与口径

1. **归一化 diff**：把上游文件做 `namespace Starward.→LunarWard.`、`using Starward.→using LunarWard.`、
   `Starward.Language→LunarWard.Language`、`xmlns:using:Starward→xmlns:using:LunarWard` 替换后，
   与本地文件做逐行 diff，只统计 `+`/`-` 行数。`.axaml` ↔ 上游 `.xaml` 成对比较。
2. **成员名集合比对**：对每个代码后置文件，抽出 `(private|public|internal|protected) ... Name(` 的成员名集合，
   与上游同名文件求差集，确认「上游方法/事件处理器一个都不少」。六个页面/窗口全部为空差集（见第三节）。
3. **行为级差异**只记「会改变运行结果」的点：控件语义替换、平台 API 替换、资源键替换、算法差异。
   纯格式（命名空间、using 顺序、`x:Bind`→`Binding`）不计入结论。
4. **不在 diff 里但必须继承的上游逻辑**（分页、end_id 推进、导出格式）改用单测锁定，见第四节。

结论分布：**逐字一致 17 个**（含 `GachaLogService.cs`、`ZZZGachaService.cs`、`SelfQueryService.cs`、
`GachaNoUp.cs`、全部 UIGF 模型与 `IconInfo`/`ItemList`）、**平台等价 27 个**、**本地新增 1 个**
（`GachaProgressToast.cs`，上游 `InfoBar` 的等价物）。

## 二、审计矩阵

| 文件（`Features/`） | 上游行数 | 本地行数 | 归一化 diff 行数 | 结论 |
| --- | ---: | ---: | ---: | --- |
| `Gacha/CloudGameGachaWindow.axaml` | 47 | 42 | 79 | 平台等价（控件/资源翻译） |
| `Gacha/CloudGameGachaWindow.axaml.cs` | 126 | 135 | 31 | 平台等价（WebView2→`Frameworks.WebView`，`Visibility`→`IsVisible`，主题资源） |
| `Gacha/DeleteGachaLogDialog.axaml` | 96 | 95 | 179 | 平台等价（控件/资源翻译） |
| `Gacha/DeleteGachaLogDialog.axaml.cs` | 225 | 279 | 140 | 平台等价（`ContentDialog`→`StarwardContentDialog`，`Hide()`→`Close()`，`CalendarDatePicker.SelectedDate`/`TimePicker.SelectedTime`） |
| `Gacha/GachaBanner.cs` | 25 | 25 | 0 | 一致（逐字） |
| `Gacha/GachaLogExportFile.cs` | 57 | 57 | 0 | 一致（逐字） |
| `Gacha/GachaLogItemEx.cs` | 42 | 46 | 8 | 平台等价（`new` 隐藏 Core 多出的 `IsUp` int 字段，lead 已登记 docs/09） |
| `Gacha/GachaLogPage.axaml` | 475 | 472 | 927 | 平台等价（控件/资源翻译） |
| `Gacha/GachaLogPage.axaml.cs` | 1119 | 1266 | 497 | 平台等价（同上；横向滚轮用 `KeyModifiers.Shift`；`User32.SetForegroundWindow`→`Window.Activate()`） |
| `Gacha/GachaLogService.cs` | 299 | 299 | 0 | 一致（逐字） |
| `Gacha/GachaLogUrl.cs` | 33 | 33 | 0 | 一致（逐字） |
| `Gacha/GachaNoUp.cs` | 543 | 543 | 0 | 一致（逐字） |
| `Gacha/GachaPityProgressBackgroundBrushConverter.cs` | 61 | 65 | 20 | 平台等价（`Point`→`RelativePoint`，颜色/断点一致） |
| `Gacha/GachaProgressToast.cs` | — | 124 | 无上游同名 | 新增（上游 `InfoBar` 的 Avalonia 等价物，仅本簇内部使用） |
| `Gacha/GachaStatsCard.axaml` | 293 | 316 | 601 | 平台等价（控件/资源翻译） |
| `Gacha/GachaStatsCard.axaml.cs` | 123 | 141 | 68 | 平台等价（`DependencyProperty`→`StyledProperty`；`Pivot+Segmented`→`TabControl`+`ToggleButton`；删除 `IsTextTrimmedChanged`） |
| `Gacha/GachaTypeStats.cs` | 49 | 49 | 0 | 一致（逐字） |
| `Gacha/GenshinBeyondGachaPage.axaml` | 637 | 584 | 967 | 平台等价（控件/资源翻译） |
| `Gacha/GenshinBeyondGachaPage.axaml.cs` | 427 | 558 | 255 | 平台等价（InfoBar 进度条→`GachaProgressToast`；`ContentDialog`→`StarwardContentDialog`；文件管理器选中→`xdg-open`） |
| `Gacha/GenshinBeyondGachaService.cs` | 439 | 438 | 15 | 平台等价（文件尾转换器 WinUI→Avalonia） |
| `Gacha/GenshinGachaService.cs` | 240 | 240 | 2 | 平台等价（xlsx 模板路径用 `Path.Combine` 分段，Linux 反斜杠无效） |
| `Gacha/RarityToGIBgConverter.cs` | 34 | 35 | 9 | 平台等价（WinUI→Avalonia 转换器签名） |
| `Gacha/RarityToSRBgConverter.cs` | 34 | 35 | 9 | 平台等价（同上） |
| `Gacha/RarityToZZZIconConverter.cs` | 30 | 31 | 9 | 平台等价（同上） |
| `Gacha/StarRailGachaService.cs` | 232 | 232 | 2 | 平台等价（同上） |
| `Gacha/UIGF/GachaUidArchiveDisplay.cs` | 79 | 79 | 0 | 一致（逐字） |
| `Gacha/UIGF/UIGF3File.cs` | 53 | 53 | 0 | 一致（逐字） |
| `Gacha/UIGF/UIGF4File.cs` | 114 | 114 | 2 | 一致（仅注释 cref 改名） |
| `Gacha/UIGF/UIGF4GachaWindow.axaml` | 270 | 228 | 496 | 平台等价（控件/资源翻译） |
| `Gacha/UIGF/UIGF4GachaWindow.axaml.cs` | 183 | 256 | 119 | 平台等价 + 待办（`Pivot`→`TabControl`、`ListView`→`ListBox`、`NumberBox`→`NumericUpDown`；文件管理器定位降级为打开目录） |
| `Gacha/UIGF/UIGF4ImportException.cs` | 21 | 21 | 0 | 一致（逐字） |
| `Gacha/UIGF/UIGFGachaService.cs` | 669 | 668 | 1 | 一致（仅删除上游未使用的 `using Vanara.PInvoke`） |
| `Gacha/UIGF/UIGFGenshinGachaItem.cs` | 16 | 16 | 0 | 一致（逐字） |
| `Gacha/UpdateGachaLogMessage.cs` | 21 | 21 | 0 | 一致（逐字） |
| `Gacha/ZZZGachaService.cs` | 567 | 567 | 0 | 一致（逐字） |
| `Gacha/ZZZGachaStatsCard.axaml` | 294 | 316 | 602 | 平台等价（控件/资源翻译） |
| `Gacha/ZZZGachaStatsCard.axaml.cs` | 123 | 141 | 68 | 平台等价（同 GachaStatsCard） |
| `Gacha/ZZZGachaToolbox/IconInfo.cs` | 46 | 46 | 0 | 一致（逐字） |
| `Gacha/ZZZGachaToolbox/ItemList.cs` | 88 | 88 | 0 | 一致（逐字） |
| `Gacha/ZZZGachaToolbox/ZZZGachaInfoWindow.axaml` | 95 | 76 | 117 | 平台等价（控件/资源翻译） |
| `Gacha/ZZZGachaToolbox/ZZZGachaInfoWindow.axaml.cs` | 476 | 573 | 221 | 平台等价 + 待办（`GridView`→`ListBox`、`Hyperlink`→`HyperlinkButton`；WebView 响应事件无请求头 → `x-rpc-lang` 替换失效） |
| `SelfQuery/SelfQueryPage.axaml` | 266 | 275 | 295 | 平台等价（控件/资源翻译） |
| `SelfQuery/SelfQueryPage.axaml.cs` | 791 | 903 | 218 | 平台等价（`ProgressRing`→`ProgressBar`，`ContentDialog`→`StarwardContentDialog`） |
| `SelfQuery/SelfQueryService.cs` | 380 | 380 | 0 | 一致（逐字） |
| `SelfQuery/SelfQueryStatsNumberConverter.cs` | 56 | 67 | 33 | 平台等价（`SystemFillColor*Brush`→`StarSuccessBrush`/`StarErrorBrush`） |

合计 45 个文件（上游同名 44 + 本地新增 1）。


## 三、行为锚点核对（diff 之外的「上游有、本地无」排查）

| 检查项 | 结论 |
| --- | --- |
| `GachaLogPage` 成员名集合 vs 上游 | 差集为空 |
| `GenshinBeyondGachaPage` 成员名集合 | 差集为空 |
| `SelfQueryPage` 成员名集合 | 差集为空 |
| `ZZZGachaInfoWindow` 成员名集合 | 差集为空 |
| `DeleteGachaLogDialog` / `UIGF4GachaWindow` / `CloudGameGachaWindow` | 差集均为空 |
| `GachaLogPage` 关键行为锚点（`EmojiPaimon`、`MenuFlyoutItem_CloudGame`、`IsZZZGachaStatsCardVisible`、`SetLastUidInGachaLogPage`、`Grid_GachaStats_PointerWheelChanged`、`KeyModifiers.Shift`、`UpdateGachaStatsCardLayout`、`GachaStatsCard_Loaded`、`InAppToast.MainWindow` 21 处） | 全部存在 |
| `GachaLogService` 分页 / `end_id` / 抽卡接口参数 | 文件 diff=0，逐字一致 |
| `GachaLogService.GetGachaTypeStats` 统计口径（不含 UP 的 5★、保底伪条目、新手池 20 / 始发 50 特例） | 文件 diff=0，逐字一致 |
| 三游戏 URL 参数解析（`GetGachaUrlPrefix`，在 `LunarWard.Core`） | 代码与上游逐字一致；由第四节单测覆盖 |
| UIGF v3 / SRGF v1.0 / UIGF v4.2 导出字段与顺序 | `UIGF3File`/`UIGF4File` diff=0；由单测锁定字段名、类型与顺序 |
| CloudGame 进程绑定 | `CloudGameService.GetCloudGameProcess` diff=0（按 `SessionId` 匹配同名进程）；`CloudGameButton` 只调用 `RunningGameService.AddRuninngGame(GameBiz, Process)`，未使用叠加层 |

## 四、单元测试清单（`tests/LunarWard.UnitTests/Features/Gacha/`）

合计 **121 个用例**（57 个 `[Fact]` + 15 个 `[Theory]` 的 64 条 `[InlineData]`），8 个文件，全部只落在本簇新目录，不改他人测试。

| 测试文件 | 用例数 | 覆盖点 |
| --- | --- |
| `GachaUrlPrefixTests.cs` | 17 | 三游戏 × 三类 URL 来源（webstatic 国服 / `gs.hoyoverse.com` 国际服 / 已带参数的 `public-operation`）→ 接口前缀换算；`#/log` 去除；`&gacha_type`/`&page`/`&size`/`&end_id`（ZZZ 为 `&real_gacha_type`）剥离；`lang` 经 `LanguageUtil.FilterLanguage` 归一（ja→ja-jp、zh-tw 保留、en→en-us）；非法 URL 抛 `ArgumentException` 且消息取 `CoreLang.Gacha_CannotParseThe*RecordURL` |
| `GachaLogQueryTests.cs` | 8 | `GachaLogQuery.ToString()` 字段顺序与命名：原神/星铁 `gacha_type=`，绝区零 `real_gacha_type=`；`BeyondGachaLogQuery`；`size=20` 固定 |
| `GachaPaginationTests.cs` | 4 | `GetGachaLogByTypeAsync` 推进规则：满 20 且 `Last.Id > end_id` 才 `page++`/`end_id=Last.Id`；短页、空页、`Last.Id <= end_id` 三种停止条件 |
| `GachaNoUpTests.cs` | 13 | `GachaNoUp.Dictionary` 键拼法（`{GameBiz}{gacha_type}`）与三游戏区间表存在性；`ComputeIsUp` 区间内=false / 区间外=true / 未知物品=true / 未知卡池或游戏=null；原神 400 归一到 301 |
| `GachaAlignmentTests.cs` | 43 | `GachaTypeStats` 的 `Avarage_5_*` 拼写与格式（上游刻意拼作 `Avarage`）；`GachaBanner.Value`/`ToLocalization()`；`GachaLogItemEx` 的 `Progress` 三套分母（90 / 原神武器 80 / 星铁光锥 80 / ZZZ 音擎·邦布 80）、`IdText`、`IsUp`（`new bool` 与基类 `int` 各自独立）、`IsPointerIn` 的 `PropertyChanged`；`GachaLogUrl` 构造器时间戳；`GachaUidArchiveDisplay.ArchiveName` 三游戏 + `Timezone` 联动 `LastItemTimeOffest`；`UIGF4ImportException` 的 Game/Uid/Message；三个 Rarity 背景/图标转换器（1..5 与 `Transparent.png` 兜底）；`GachaPityProgressBackgroundBrushConverter`（74/63/65 三档变色、90/80 保底 offset、`Opacity=0.4`、相对端点、非 item 返回 null）；`SelfQueryStatsNumberConverter` 的 `+N`/`-N`/`-` 与 `ConvertBack` 抛错 |
| `GachaModelParityTests.cs` | 22 | 两份 `GachaNoUp`（`Features` 移植版 vs `Core` 遗留版）卡池键集合、每个物品的 `NoUpTimes` 区间逐项一致；`ComputeIsUp` 只在 Core 遗留版；ZZZ 图鉴 `ItemList`/`ItemListAvatar`/`ItemListBuddy`/`IconInfo`/`IconInfoItem` 的 `JsonPropertyName` 映射 |
| `GachaLinuxDivergenceTests.cs` | 9 | 把下文 G-1..G-8 逐条落成结构断言（G-2/G-3 明确标注「单测不可断言」并只锁定替代实现存在） |
| `GachaExportSchemaTests.cs` | 6 | UIGF v3 `info` 字段名、顺序与取值（`uid`/`gacha_type`/`item_id` 写成字符串，`time` 为 `yyyy-MM-dd HH:mm:ss`，`export_app` 默认 Starward，`uigf_version=v3.0`，`srgf_version=v1.0`）；SRGF 列表项；UIGF v4.2 顶层 `info`/`hk4e`/`hkrpg`/`nap`/`hk4e_ugc` 与归档 `uid`/`timezone`/`lang`（`lang` 为 null 时整个省略） |

运行：

```bash
dotnet test tests/LunarWard.UnitTests/LunarWard.UnitTests.csproj --filter "FullyQualifiedName~Features.Gacha"
```

实测（2026-10-26，共享 master 树全量构建转绿后）：

```
已通过! - 失败: 0，通过: 121，已跳过: 0，总计: 121，持续时间: 104 ms - LunarWard.UnitTests.dll (net10.0)
```

## 五、未覆盖风险与待办

| 编号 | 事项 | 影响 | 处置 |
| --- | --- | --- | --- |
| G-1 | `Frameworks/WebView` 的资源响应事件不带请求头（`CoreWebView2Compat.WebResourceResponseView` 只有 `Uri`/`Response`） | ZZZ 图鉴窗口里「按请求头 `x-rpc-lang` 改写语言」这一段无法等价；Cookie 头可经 `CookieManager.GetCookiesAsync` 重建，`mi18nLang` 替换仍生效 | 已在该文件 remarks 标注；需扩 `Frameworks/WebView`（非本簇作用域）后回填，见 `docs/09 §十五` |
| G-2 | `Launcher.LaunchFolderAsync(FolderLauncherOptions.ItemsToSelect)` 无等价 API（XDG 门户没有「在文件管理器里选中某文件」） | 导出/缓存目录只能打开所在文件夹，不能高亮文件名 | 降级为 `xdg-open <folder>`；`docs/09 §十五` 已记 |
| G-3 | `AppWindow.TitleBar.ExtendsContentIntoTitleBar` / `DesktopAcrylicBackdrop` 无等价 | 标题栏由窗口管理器绘制，窗口内不再自绘亚克力 | 注释保留上游写法 + `WindowEx` 适配；`docs/09 §十五` |
| G-4 | WinUI `InfoBar`（进度条 + 取消按钮）不存在 | 抽卡同步进度提示 | 以 `Features/Gacha/GachaProgressToast.cs` 等价实现（标题/消息/取消按钮，5s 自动消失） |
| G-5 | `TextBlock.IsTextTrimmedChanged` 不存在 | `GachaStatsCard` 的自适应字号（上游页面从未调用该处理器） | 删除处理器，保留 `ResetGachaTypeTextFontSize()` |
| G-6 | `Core/SelfQuery/SelfQueryService.cs` 是本地遗留（上游 Core 无此文件），与本簇 `Features/SelfQuery/SelfQueryService.cs` 同名，曾在 `AppConfig.ServiceProvider.cs` 引发 `CS0104` 二义性 | 旧 MVVM 层（`ViewModels/Starward/SelfQueryPageViewModel.cs`、`Launch/Database/SelfQueryLogStore.cs`）仍在用 Core 那份 | lead 已在 `AppConfig.ServiceProvider.cs` 用 `using SelfQueryService = LunarWard.Features.SelfQuery.SelfQueryService;` 消歧，**不删** Core 文件；登记为 endgame 删除项（`docs/10 §7`）。单测 `G6_BothSelfQueryServices_ExistAndAreDistinct` 锁定两份并存 |
| G-7 | `OverlayWindow` 未移植，`RunningGameService.OpenOverlayWindow()` 恒 false | 云游戏叠加层不可用 | CloudGame 簇未调用该 API，无需改动；待叠加层落地后复评 |
| G-8 | 抽卡数据层 SQL 由 `LunarWard.Features.Database.DatabaseService.CreateConnection()` 提供，连接串与上游一致但库文件名/目录不同 | 数据库文件位置差异（平台等价） | `docs/09 §十五` 已记 |

### G-9 本地遗留双份 `GachaNoUp`

| 项 | `src/LunarWard.Core/Gacha/GachaNoUp.cs`（本地遗留，**endgame 删除 Core 副本**） | `src/LunarWard/Features/Gacha/GachaNoUp.cs`（移植版，保留） |
| --- | --- | --- |
| 字节数 / 行数 | 20698 / 543 | 18826 / 503 |
| namespace | `LunarWard.Core.Gacha` | `LunarWard.Features.Gacha` |
| `using System.Globalization;` | 有 | 无（上游无） |
| `ComputeIsUp(string gameKind, int gachaType, int itemId, DateTime time)` | 有（LunarWard 补充：区间内=false、区间外=true、卡池无表=null、原神 400→301） | 无（上游无） |
| 属性初始化器 | `Game { get; set; } = null!`、`Name { ... } = null!`、`NoUpTimes { ... } = []` | 无初始化器（净上游写法） |
| 三游戏卡池/物品/时间段数据 | 一致 | 一致 |
| 谁在用 | `LunarWard.Launch`、旧 MVVM 层、`ComputeIsUp` 调用点 | `Features/Gacha/**` 内的 `GachaNoUp.Dictionary`（同命名空间优先，解析到移植版） |

处置（lead 裁决）：**只保留移植版**；Core 副本登记为 endgame 删除项，当前**不删**（`LunarWard.Launch` 与旧 Core 调用点仍在用）。删除时需把 `ComputeIsUp` 的调用点一并收敛。两份数据逐项相同由单测 `GachaModelParityTests` 锁定。

## 六、验收命令

```bash
# 1) 文件级对齐（本簇 missing 应为 0）
python3 scripts/check-alignment.py --json | python3 -c "import json,sys;d=json.load(sys.stdin);print([m for m in d['files']['missing'] if any(k in m for k in ('Gacha','SelfQuery','CloudGame'))])"

# 2) 构建
dotnet build src/LunarWard/LunarWard.csproj

# 3) 本簇单测
dotnet test tests/LunarWard.UnitTests/LunarWard.UnitTests.csproj --filter "FullyQualifiedName~Features.Gacha"
```
