using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace LunarWard.App.Views.Starward.Controls;

/// <summary>
/// Starward / WinUI <c>ContentDialog</c> 的 Avalonia 等价物：窗口内浮层对话框的内容容器。
/// </summary>
/// <remarks>
/// <para>
/// 外观（标题行、右上角关闭、底部按钮栏、亚克力底、圆角描边、投影）由
/// <c>StarwardContentDialog.axaml</c> 里的 <see cref="Avalonia.Controls.ControlTheme"/> 提供，
/// 业务 dialog 只需要继承本类、把内容放进 XAML 根节点即可：既有的 <c>Grid</c>/<c>Border</c>
/// 内容原样保留，原来的无边框 <c>Window</c> 外壳（亚克力 Border + 右上角 X）删掉。
/// </para>
/// <para>
/// 关闭统一走 <see cref="DialogService.ShowAsync"/>：它订阅 <see cref="CloseRequested"/> 调
/// <c>DialogHost.Close()</c>，并在关闭后触发 <see cref="Closed"/>；ESC 与右上角 X 都走同一条路。
/// 这样调用方在 <c>await</c> 之后读 <c>Request</c>/<c>Result</c> 的模式与原来的
/// <c>ShowDialog(owner)</c> 保持一致。
/// </para>
/// </remarks>
public class StarwardContentDialog : ContentControl
{
    /// <summary>标题；为空时标题行（48px）收起，只保留右上角关闭按钮。</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<StarwardContentDialog, string?>(nameof(Title));

    /// <summary>底部主按钮文字（强调色），空 = 不显示。</summary>
    public static readonly StyledProperty<string?> PrimaryButtonTextProperty =
        AvaloniaProperty.Register<StarwardContentDialog, string?>(nameof(PrimaryButtonText));

    /// <summary>底部次按钮文字，空 = 不显示。</summary>
    public static readonly StyledProperty<string?> SecondaryButtonTextProperty =
        AvaloniaProperty.Register<StarwardContentDialog, string?>(nameof(SecondaryButtonText));

    /// <summary>底部关闭按钮文字（「关闭」/「取消」），空 = 不显示。</summary>
    public static readonly StyledProperty<string?> CloseButtonTextProperty =
        AvaloniaProperty.Register<StarwardContentDialog, string?>(nameof(CloseButtonText));


    /// <summary>主按钮是否可用（WinUI 的 <c>IsPrimaryButtonEnabled</c>）；关闭时置 false 可表示「还不能开始」。</summary>
    public static readonly StyledProperty<bool> IsPrimaryButtonEnabledProperty =
        AvaloniaProperty.Register<StarwardContentDialog, bool>(nameof(IsPrimaryButtonEnabled), defaultValue: true);

    /// <summary>是否显示右上角 X；默认显示（相当于原来每个 dialog 自己画的关闭按钮）。</summary>
    public static readonly StyledProperty<bool> IsCloseButtonVisibleProperty =
        AvaloniaProperty.Register<StarwardContentDialog, bool>(nameof(IsCloseButtonVisible), true);

    /// <summary>内容区内边距；WinUI 里各 dialog 自己 <c>Grid Padding="24"</c>，所以默认 0。</summary>
    public static readonly StyledProperty<Thickness> ContentPaddingProperty =
        AvaloniaProperty.Register<StarwardContentDialog, Thickness>(nameof(ContentPadding));

    /// <summary>主按钮被点击。</summary>
    public event EventHandler? PrimaryButtonClicked;

    /// <summary>次按钮被点击。</summary>
    public event EventHandler? SecondaryButtonClicked;

    /// <summary>底部关闭按钮被点击。</summary>
    public event EventHandler? CloseButtonClicked;

    /// <summary>请求关闭（X / ESC / 底部关闭按钮 / 内容里的业务按钮调 <see cref="Close"/>）。</summary>
    public event EventHandler? CloseRequested;

    /// <summary>对话框已经从宿主上关闭（<see cref="DialogService"/> 在 <c>Show</c> 返回后触发）。</summary>
    public event EventHandler? Closed;

    /// <summary>
    /// 构造时挂两类处理器：
    /// 模板里的按钮（<c>PART_*</c>）通过冒泡的 <see cref="Button.ClickEvent"/> 识别，不依赖
    /// XAML 事件绑定（主题文件没有 code-behind）；
    /// ESC 用 Tunnel 阶段处理，保证输入框有焦点时也能关闭（等价 WinUI ContentDialog 的 ESC）。
    /// </summary>
    public StarwardContentDialog()
    {
        AddHandler(Button.ClickEvent, OnChildButtonClick);
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        ApplyImplicitTheme();
    }


    /// <summary>
    /// 补上隐式主题：Avalonia 只按「控件自己的类型」找默认 <c>ControlTheme</c>，不回溯基类，
    /// 而各对话框都是 <see cref="StarwardContentDialog"/> 的子类（GameLauncherSettingDialog 等），
    /// 所以这里显式把容器主题挂到 <see cref="StyledElement.Theme"/> 上。
    /// 设计器里 <see cref="Application.Current"/> 可能为空，此时保持 Fluent 默认（不影响编译）。
    /// </summary>
    private void ApplyImplicitTheme()
    {
        if (Theme is not null || Application.Current is not { } app)
        {
            return;
        }

        if (app.TryFindResource(typeof(StarwardContentDialog), out object? theme)
            && theme is ControlTheme controlTheme)
        {
            Theme = controlTheme;
        }
    }

    /// <summary>标题。</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>底部主按钮文字。</summary>
    public string? PrimaryButtonText
    {
        get => GetValue(PrimaryButtonTextProperty);
        set => SetValue(PrimaryButtonTextProperty, value);
    }

    /// <summary>底部次按钮文字。</summary>
    public string? SecondaryButtonText
    {
        get => GetValue(SecondaryButtonTextProperty);
        set => SetValue(SecondaryButtonTextProperty, value);
    }

    /// <summary>底部关闭按钮文字。</summary>
    public string? CloseButtonText
    {
        get => GetValue(CloseButtonTextProperty);
        set => SetValue(CloseButtonTextProperty, value);
    }


    public bool IsPrimaryButtonEnabled
    {
        get => GetValue(IsPrimaryButtonEnabledProperty);
        set => SetValue(IsPrimaryButtonEnabledProperty, value);
    }

    /// <summary>是否显示右上角 X。</summary>
    public bool IsCloseButtonVisible
    {
        get => GetValue(IsCloseButtonVisibleProperty);
        set => SetValue(IsCloseButtonVisibleProperty, value);
    }

    /// <summary>内容区内边距。</summary>
    public Thickness ContentPadding
    {
        get => GetValue(ContentPaddingProperty);
        set => SetValue(ContentPaddingProperty, value);
    }

    /// <summary>是否已经打开过（用于自检截图判断浮层是否就位）。</summary>
    public bool IsShown { get; private set; }

    private Button? _primaryButton;

    /// <summary>右上角 X（自检/焦点归还用）。</summary>
    private Button? _closeIconButton;


    /// <summary>请求关闭当前对话框（X / ESC / 底部关闭按钮都走这里）。</summary>
    public void Close() => RequestClose();

    /// <summary>请求关闭并带上「点了哪个按钮」的语义，供 dialog 自己决定返回值。</summary>
    protected void RequestClose()
    {
        if (!IsShown)
        {
            return;
        }

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }


    /// <summary>把键盘焦点放到内容里第一个可聚焦控件（跳过浮层自己的 X 与按钮栏）。</summary>
    public bool FocusFirstControl()
    {
        if (Content is not Visual root)
        {
            return false;
        }

        Control? target = root
            .GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(x => x.Focusable
                && x.IsEffectivelyVisible
                && x.IsEffectivelyEnabled
                && x is not (Button { IsDefault: true }));

        if (target is null)
        {
            return false;
        }

        target.Focus();
        return target.IsFocused;
    }


    /// <summary>由 <see cref="DialogService"/> 在 <c>DialogHost.Show</c> 打开后调用。</summary>
    internal void NotifyOpened()
    {
        IsShown = true;
        UpdateChromeClasses();
        OnDialogOpened();
    }

    /// <summary>由 <see cref="DialogService"/> 在浮层关闭后调用（也会触发 <see cref="Closed"/>）。</summary>
    internal void NotifyClosed()
    {
        if (!IsShown)
        {
            return;
        }

        IsShown = false;
        OnDialogClosed();
        Closed?.Invoke(this, EventArgs.Empty);
    }


    /// <summary>打开后回调（dialog 重写它做首次聚焦、加载数据等）。</summary>
    protected virtual void OnDialogOpened()
    {
    }


    /// <summary>关闭后回调（原来 <c>OnClosing</c> 里写回配置的 dialog 改重写这里）。</summary>
    protected virtual void OnDialogClosed()
    {
    }


    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TitleProperty
            || change.Property == PrimaryButtonTextProperty
            || change.Property == SecondaryButtonTextProperty
            || change.Property == CloseButtonTextProperty
            || change.Property == IsPrimaryButtonEnabledProperty
            || change.Property == IsCloseButtonVisibleProperty)
        {
            UpdateChromeClasses();
        }
    }


    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _primaryButton = e.NameScope.Find<Button>("PrimaryButton");
        _closeIconButton = e.NameScope.Find<Button>("CloseIconButton");
        UpdateChromeClasses();
    }


    /// <summary>把「有没有标题 / 有没有各按钮」映射成主题里的伪类，由样式收起多余行。</summary>
    private void UpdateChromeClasses()
    {
        PseudoClasses.Set(":noheader", string.IsNullOrEmpty(Title));
        PseudoClasses.Set(":noicon", !IsCloseButtonVisible);

        bool hasPrimary = !string.IsNullOrEmpty(PrimaryButtonText);
        bool hasSecondary = !string.IsNullOrEmpty(SecondaryButtonText);
        bool hasClose = !string.IsNullOrEmpty(CloseButtonText);

        PseudoClasses.Set(":noprimary", !hasPrimary);
        PseudoClasses.Set(":nosecondary", !hasSecondary);
        PseudoClasses.Set(":noclose", !hasClose);
        PseudoClasses.Set(":nobuttons", !hasPrimary && !hasSecondary && !hasClose);

        // 主按钮可用性由对话框决定（例如预下载要等体积算完才允许点）
        if (_primaryButton is not null)
        {
            _primaryButton.IsEnabled = IsPrimaryButtonEnabled;
        }
    }


    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !IsShown)
        {
            return;
        }

        e.Handled = true;
        RequestClose();
    }


    private void OnChildButtonClick(object? sender, RoutedEventArgs e)
    {
        // 模板按钮从内容冒泡上来；用名字区分，避免和 dialog 内容里的业务按钮（如「开始安装」）冲突。
        if (e.Source is not Button { Name: { } name })
        {
            return;
        }

        switch (name)
        {
            case "PrimaryButton":
                PrimaryButtonClicked?.Invoke(this, EventArgs.Empty);
                RequestClose();
                break;
            case "SecondaryButton":
                SecondaryButtonClicked?.Invoke(this, EventArgs.Empty);
                RequestClose();
                break;
            case "CloseButton":
            case "CloseIconButton":
                CloseButtonClicked?.Invoke(this, EventArgs.Empty);
                RequestClose();
                break;
        }
    }
}
