# 12 · WinUI 3 → Avalonia 移植手册（Starward 文件级搬运规则）

> 本文件是所有 teammate 移植 Features/* 时的操作手册。**目标不是重写，是搬运**：把上游
> `/home/qie2035/projects/csharp/Starward/src/Starward/` 下的文件原样拿过来，只做下面列出的
> 机械替换。凡是需要"理解性重写"的地方，先在本文件登记，再动手。

## 0. 铁律

1. **一个上游文件 = 一个移植文件**，文件名、目录名、类型名、成员名、事件名、命令名全部保持一致
   （`MainView.xaml` → `MainView.axaml`、`class GeneralSetting : PageBase` → `class GeneralSetting : PageBase`）。
2. **不要发明新层**：不新增 ViewModel/Service/Converter 中间层来"适配"。上游把逻辑写在 code-behind，
   我们就写在 code-behind；上游用 `PageBase`，我们就用 `src/LunarWard/Frameworks/PageBase.cs`。
3. **XAML 控制树尽量逐行对应**：`Grid.Row`/`Grid.ColumnSpan`/`StackPanel`/`ScrollViewer`/`ItemsControl`
   在 Avalonia 里同名同义，不要因为"更好写"而压平层级。
4. 上游有、本仓库尚无的依赖类型 → 直接把它从上游 Core 一起搬（Core 已基本对齐），**不要写替代实现**。
5. 实在无法等价的能力（WinUI 专有控件/API）才允许替换，且必须在
   `docs/09-LINUX-DIVERGENCES.md` 追加一条：位置、上游行为、本仓库行为、原因、待办。
6. 每个功能组做完立刻 commit（中文提交信息，写清对齐了哪些上游文件），不要攒到最后。

## 1. 路径与命名空间

| 上游 | 本仓库 |
| --- | --- |
| `src/Starward/Features/<Feature>/Foo.xaml(.cs)` | `src/LunarWard/Features/<Feature>/Foo.axaml(.cs)` |
| `src/Starward/Frameworks/Foo.cs` | `src/LunarWard/Frameworks/Foo.cs` |
| `src/Starward/Controls/Foo.cs` | `src/LunarWard/Controls/Foo.cs` |
| `namespace Starward.Features.Gacha;` | `namespace LunarWard.Features.Gacha;` |
| `x:Class="Starward.Features.Gacha.GachaPage"` | `x:Class="LunarWard.Features.Gacha.GachaPage"` |
| `xmlns:local="using:Starward.Features.Gacha"` | `xmlns:local="using:LunarWard.Features.Gacha"` |

`using Starward.Core;` → `using LunarWard.Core;`（其余 `Starward.*` 同理）。批量替换用
`perl -pi -e 's/\bStarward\./LunarWard./g'`，但**不要再替换 `LunarWard.Core` 里已有的 `LunarWard.`**（幂等）。

## 2. 类型映射（机械替换）

| WinUI 3 | Avalonia 12 | 备注 |
| --- | --- | --- |
| `Window` / `Page` | `Window` / `UserControl` | 页面统一继承 `PageBase`（`src/LunarWard/Frameworks/PageBase.cs`） |
| `UserControl` | `UserControl` | 同名 |
| `Grid`, `StackPanel`, `RelativePanel`(→`Grid`/`DockPanel`), `ScrollViewer`, `Border`, `Canvas`, `WrapPanel`, `ItemsControl`, `ListView`, `ListBox`, `ComboBox`, `TextBox`, `PasswordBox`, `Button`, `ToggleButton`, `CheckBox`, `RadioButton`, `Slider`, `ProgressBar`, `ProgressRing`(→`ProgressBar IsIndeterminate=true`), `Image`, `TextBlock`, `RichTextBlock`(→`TextBlock` + `Inlines`), `HyperlinkButton`(→`Button`+`Link` 样式), `Separator`(→`Border`), `Expander`, `TabView`(→`TabControl`), `NavigationView`(→ 自绘 `ListBox`+`ContentControl`，见 ViewHost) | 同名控件基本都在 | 属性名多为同名 |
| `ToolTip` | `ToolTip.Tip` | 附加属性 |
| `x:Bind Foo` | `{Binding Foo}` | 默认 `DataContext = this`（code-behind 属性需 `INotifyPropertyChanged` or `DirectProperty`） |
| `x:Bind Foo, Mode=OneWay` | `{Binding Foo}` | Avalonia 绑定默认双向/单向上都work |
| `Visibility.Collapsed/Visible` | `IsVisible="False/True"` | 见 §3 |
| `StaticResource`/`ThemeResource` | `StaticResource`/`DynamicResource` | 主题色用 `DynamicResource` |
| `x:Uid` | 无 | 本项目文案直接用 `Strings["Key"]`（`LunarWard.Language`）或直接文本 |
| `Storyboard`/`DoubleAnimation` | `Animation`/`Transitions` | 简单动效优先 `Transitions` |
| `ContentDialog` | `StarwardContentDialog` + `Services/DialogService` | 见 §4 |
| `InfoBar` | 内联 `Border` + 图标/文本（不新增控件类型） | 见 §4 |
| `TeachingTip` | `Popup`/`Flyout` | |
| `Flyout`/`MenuFlyout` | `Flyout`/`MenuFlyout` | 同名 |
| `DispatcherQueue.TryEnqueue` | `Dispatcher.UIThread.Post/InvokeAsync` | |
| `Microsoft.UI.Xaml.Media.Imaging.BitmapImage` | `Avalonia.Media.Imaging.Bitmap` | 或用 `Controls/CachedImage.cs`（上游同名，待移植） |
| `Application.Current.Resources` | `Application.Current!.Resources` | |
| `Window.AppWindow`/`ExtendsContentIntoTitleBar` | `Window.ExtendClientAreaToDecorationsHint` | `Frameworks/WindowEx.cs` 待移植 |
| `StorageFile`/`FileOpenPicker` | `IStorageProvider`（`TopLevel.StorageProvider`） | 已有用法见 `Features/Setting/*.axaml.cs` |
| `Clipboard` | `Helpers/ClipboardHelper.cs` 或 `TopLevel.GetTopLevel(this)!.Clipboard` | Avalonia 12 只有 `ClipboardExtensions` 便捷方法 |
| `WebView2` | `LunarWard.Frameworks.WebView.WebView` | API 形状已对齐，见 `docs/09 §五` |
| `H.NotifyIcon`/`TrayIcon` | `TrayIcon`（Avalonia.Controls）+ `Services/TrayService.cs` | |
| `Window.SystemBackdrop`(Mica/Acrylic) | 半透明 `Background` + `TransparencyLevelHint` | |
| `Microsoft.UI.Xaml.Controls.SymbolIcon` | `PathIcon`/`TextBlock` 字形 | |
| `Color`/`SolidColorBrush` | 同名（`Avalonia.Media`） | |
| `Thickness` | `Thickness` | 同名 |
| `HorizontalAlignment`/`VerticalAlignment` | 同名 | |
| `TextWrapping="Wrap"` | `TextWrapping="Wrap"` | |

## 3. 可见性

上游大量 `Visibility="{x:Bind Foo, Converter={StaticResource BoolToVisibilityConverter}}"`。
Avalonia 没有 `Visibility`，**统一改成** `IsVisible="{Binding Foo}"`；反向用
`IsVisible="{Binding !Foo}"`（Avalonia 内置 `!` 取反），或显式转换器
`src/LunarWard/Converters/*`（已按上游 key 注册在 `App.axaml`，例如
`{StaticResource BoolToVisibilityConverter}` 返回 bool 可直接给 `IsVisible`）。
`Converters/VisibilityToBoolConverter.cs` 用于反向场景。

## 4. 对话框与提示（避免每页自造）

- **模态对话框**：继承 `Views/Starward/Controls/StarwardContentDialog`，用
  `DialogService.ShowAsync(host, dialog)` 显示；返回值/请求放在对话框自己的 `Result`/`Request` 属性上。
- **确认框**：`DialogService.ShowConfirmAsync(...)`（见 `Services/DialogService.cs` 现有重载）。
- **轻提示**：`Services/InAppToast.cs`（`InfoBar` 的等价物），不要每页写 `Toast`。
- **文件/目录选择**：`TopLevel.GetTopLevel(this)!.StorageProvider`（上游 `FileOpenPicker` 等价）。

## 5. 移植顺序与自检

每组功能按此顺序做，做完一步构建一次：

1. 复制上游 `*.xaml` → `*.axaml`，批量改命名空间与 `x:Class`。
2. 复制 `*.xaml.cs` → `*.axaml.cs`，改 using/命名空间，先让类型编译通过（WinUI API 报错逐个替换）。
3. 按 §2 表替换 XAML 控件/属性，直到 `dotnet build src/LunarWard/LunarWard.csproj` 0 错误。
4. 处理数据/服务依赖：优先用 `LunarWard.Core` 与 `LunarWard.Launch` 已有类型；缺的从上游 Core 搬。
5. `dotnet test tests/LunarWard.UnitTests` 不许新增失败；能加纯逻辑单测就加。
6. 截图自检：`dotnet run --project src/LunarWard -- --screenshot --page <name>`（见 `scripts/`）。
7. commit：`feat(<feature>): 移植上游 Features/<X>（N 个文件，对齐 <commit>）`。

## 6. 已就绪的可复用件

| 用途 | 位置 |
| --- | --- |
| 页面基类（GameId/GameBiz/OnLoaded/OnUnloaded） | `src/LunarWard/Frameworks/PageBase.cs` |
| 转换器（13 个，App.axaml 已注册） | `src/LunarWard/Converters/` |
| 剪贴板 / 哈希 / 驱动器 / 光标 | `src/LunarWard/Helpers/` |
| WebView（WebKitGTK，WebView2 形状） | `src/LunarWard/Frameworks/WebView/` |
| 对话框 / 轻提示 / 托盘 | `src/LunarWard/Services/{DialogService,InAppToast,TrayService}.cs` |
| 本地化 | `LunarWard.Language`（`Strings["Key"]`，上游 Lang.resx 全量） |
| 数据库（Dapper，上游 DatabaseSqls） | `src/LunarWard.Launch/Database/` |

## 7. 禁止事项

- 禁止为了"先跑起来"新建 `Features/<X>/FooViewModel.cs` 之类的中间层。
- 禁止把上游 code-behind 逻辑改写进 `Services/`（上游没有的 Service 不要造）。
- 禁止改上游只读目录 `/home/qie2035/projects/csharp/Starward`。
- 禁止在共享 worktree 里 `git commit --amend` / `git add -A`（会卷入他人改动）；只 `git add` 自己的路径。
