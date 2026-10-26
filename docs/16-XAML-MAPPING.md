# WinUI XAML → Avalonia XAML 映射与全量编译问题清单

> 本文是 `docs/09-LINUX-DIVERGENCES.md` 的配套速查表：`docs/09` 记录「为什么这样替换」，
> 本文只记录「遇到这条错误，改成什么」。
>
> **验收门槛：`dotnet build src/LunarWard/LunarWard.csproj --no-incremental`（或 `-t:Rebuild`）。**
> 增量构建在 `.axaml` 未改动时会整段跳过 Avalonia XAML 编译，「0 error」会骗人（dapper-db 与 misc-port 均已踩过）。
> 统计错误行时注意前缀是 `Avalonia error AVLNxxxx`，`grep ": error"` 统计会漏。

## 1. 编译绑定

| 现象（AVLN…） | 改法 |
|---|---|
| `Cannot parse a compiled binding without an explicit x:DataType` | 根节点加 `x:CompileBindings="False"`（上游用 `{x:Bind}`，Avalonia 默认开编译绑定）；确实要编译绑定就在根/模板上补 `x:DataType="local:Xxx"` |
| `Unable to find suitable setter or adder for property Converter of type CompiledBinding` | 同上，`x:CompileBindings="False"` |
| `Failed to parse binding path 'x:XxxHelper.Foo(Bar)'` | `{x:Bind}` 函数绑定在 Avalonia 不支持：改成模型只读属性（如 `GachaBanner.LocalizedName`）或 `IValueConverter` |
| `Unable to resolve property or method of name 'Node' on XamlPseudoType` | 模板/控件类型本身不存在（`DataTemplate` 内元素类型没解析），补 `x:DataType` 或换等价控件 |

## 2. 类型替换

| WinUI | Avalonia |
|---|---|
| `BitmapImage` | `Image Source="avares://LunarWard/…"` / `ImageBrush Source=…`；代码里 `new Bitmap(path)` |
| `MenuFlyoutItem` / `MenuFlyoutSeparator` | `MenuItem` / `Separator`（放在 `MenuFlyout` 内） |
| `AppBarSeparator` | `Separator` |
| `Pivot` / `PivotItem` | `Frameworks.Pivot` / `Frameworks.PivotItem`（Lead 已落：`TabControl`/`TabItem` 派生） |
| `VisualStateGroup` / `VisualState` | 删除；用 `Style` 选择器或代码切换 |
| `FontIcon Glyph` | `Frameworks.FontIcon`（已落） |
| `NavigationView` / `Frame` | `Frameworks.NavigationView` / `Frameworks.Frame`（已落） |

## 3. 属性替换

| WinUI | Avalonia |
|---|---|
| `Grid.Padding` | 外层 `Border Padding` |
| `Grid.Translation="x,y"` | `RenderTransform="translate(x,y)"` |
| `TextBlock.IsTextScaleFactorEnabled` | 删除（无等价物） |
| `Border Visibility` | `IsVisible` + `VisibilityToBoolConverter`（或直接布尔属性） |
| `Image CornerRadius` / `CachedImage CornerRadius` | `CachedImage` 已支持 `CornerRadius`（`Visual.Clip` 实现）；普通 `Image` 外包 `Border` |
| `ExtendClientAreaChromeHints` | 删除（Avalonia 12 已移除） |
| `ListBox AllowDrop/DragOver/Drop` | `DragDrop.AllowDrop="True"` 等附加属性 |
| `<Border.BoxShadow><BoxShadows>…` | `BoxShadow="0 4 16 0 #40000000"`（元素写法会触发 XAML 编译器内部异常） |
| `Checked="Handler"`（WinUI 事件） | `IsCheckedChanged="Handler"` |
| `RequestedThemeVariant` on `Grid` | 窗口级 `RequestedThemeVariant`（属性在 `StyledElement` 上） |
| `<TransformGroup>` 内给变换起 `x:Name` | 不生成字段；代码侧 `(TransformGroup)ctl.RenderTransform` 后取 `Children[i]` |

## 4. 级联错误识别

一个文件里的 XAML 编译器内部异常（`AVLN2000: Internal compiler error: …`）会让**后续所有 XAML 任务成片失败**，
表现为「别的文件突然几百条 `Unable to resolve…`」。处理顺序：先修 `Internal compiler error` 所在行，再重扫。

已修的级联源（记录在案）：
- `Features/Gacha/GachaLogPage.axaml` 的 `{Binding ToLocalization()}` → `{Binding LocalizedName}`
- `Features/GameLauncher/GameLauncherSettingDialog.axaml` 的 `<Pivot>`（无对应类型）→ `Frameworks.Pivot`

## 唯一可信的验收命令（2026-10-26，misc-port 实测）

```bash
rm -rf /tmp/lwart
dotnet build src/LunarWard/LunarWard.csproj -t:Rebuild --artifacts-path /tmp/lwart
```

`--artifacts-path` 把 `obj/`、`bin/` 全量重定向到独立目录，**不会和其他 agent 的并发构建抢 `obj/ref/*.dll`**。共享工作区里用普通 `-t:Rebuild` 会随机出现 `MSB3030 / MSB3554 / CS0006 / CS0016 / AVLN9999(file locked)`，这些是**假错误**，每次数字都不一样；`--no-incremental` 也一样会中招（只是概率低些）。

判读结果时注意：XAML 错误的行前缀是 `Avalonia error AVLNxxxx`，不是 `error CS`，所以 `grep ": error"` 会漏，正确写法：

```bash
grep -E "error (AVLN|CS)" /tmp/lwart.build.log | sed -E 's#.*/src/LunarWard/##'
```
