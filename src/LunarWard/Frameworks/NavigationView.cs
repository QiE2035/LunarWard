using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace LunarWard.Frameworks;

/// <summary>左侧栏显示模式（上游 NavigationView 的同名枚举，本次只实现 LeftCompact 语义）。</summary>
public enum NavigationViewPaneDisplayMode
{
    Auto,
    Left,
    Top,
    LeftCompact,
    LeftMinimal,
}

/// <summary>导航项（等价于 <c>Microsoft.UI.Xaml.Controls.NavigationViewItem</c>）。</summary>
public class NavigationViewItem : Control
{
    public object? Tag { get; set; }

    public object? Icon { get; set; }

    public object? Content { get; set; }

    public bool IsSelected { get; set; }
}

/// <summary>导航项分隔线。</summary>
public class NavigationViewItemSeparator : Control
{
}

public class NavigationViewItemInvokedEventArgs
{
    public NavigationViewItem? InvokedItemContainer { get; init; }

    public bool IsSettingsInvoked { get; init; }
}

/// <summary>
/// <c>Microsoft.UI.Xaml.Controls.NavigationView</c> 的最小等价实现：
/// 只实现上游 MainView 用到的部分——LeftCompact 折叠栏、<see cref="MenuItems"/> 声明式菜单、
/// <see cref="PaneCustomContent"/>、<see cref="SelectedItem"/> 与 <see cref="ItemInvoked"/>。
/// 图标栏 40×40、栏宽 <see cref="CompactPaneLength"/>、内容区与栏同色（与上游一致）。
/// 未实现：BackButton / PaneToggleButton / 展开态 / 顶部模式（本项目不使用）。
/// 平台差异见 docs/09「五、Features/ViewHost」。
/// </summary>
public class NavigationView : ContentControl
{

    public static readonly StyledProperty<double> CompactPaneLengthProperty =
        AvaloniaProperty.Register<NavigationView, double>(nameof(CompactPaneLength), 48);

    public static readonly StyledProperty<NavigationViewPaneDisplayMode> PaneDisplayModeProperty =
        AvaloniaProperty.Register<NavigationView, NavigationViewPaneDisplayMode>(nameof(PaneDisplayMode), NavigationViewPaneDisplayMode.LeftCompact);

    public static readonly StyledProperty<object?> PaneCustomContentProperty =
        AvaloniaProperty.Register<NavigationView, object?>(nameof(PaneCustomContent));

    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<NavigationView, object?>(nameof(SelectedItem));

    public static readonly StyledProperty<bool> IsBackButtonVisibleProperty =
        AvaloniaProperty.Register<NavigationView, bool>(nameof(IsBackButtonVisible));

    public static readonly StyledProperty<bool> IsPaneToggleButtonVisibleProperty =
        AvaloniaProperty.Register<NavigationView, bool>(nameof(IsPaneToggleButtonVisible));


    private readonly StackPanel _pane;
    private readonly ContentPresenter _contentPresenter;
    private readonly Dictionary<NavigationViewItem, ToggleButton> _buttons = new();


    public NavigationView()
    {
        _pane = new StackPanel { Orientation = Orientation.Vertical };
        _contentPresenter = new ContentPresenter { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
        };
        var paneHost = new Panel { Children = { _pane } };
        Grid.SetColumn(paneHost, 0);
        Grid.SetColumn(_contentPresenter, 1);
        grid.Children.Add(paneHost);
        grid.Children.Add(_contentPresenter);
        Template = new FuncControlTemplate((_, _) => grid);
        MenuItems.CollectionChanged += (_, _) => RebuildPane();
    }


    public double CompactPaneLength
    {
        get => GetValue(CompactPaneLengthProperty);
        set => SetValue(CompactPaneLengthProperty, value);
    }

    public NavigationViewPaneDisplayMode PaneDisplayMode
    {
        get => GetValue(PaneDisplayModeProperty);
        set => SetValue(PaneDisplayModeProperty, value);
    }

    public object? PaneCustomContent
    {
        get => GetValue(PaneCustomContentProperty);
        set => SetValue(PaneCustomContentProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public bool IsBackButtonVisible
    {
        get => GetValue(IsBackButtonVisibleProperty);
        set => SetValue(IsBackButtonVisibleProperty, value);
    }

    public bool IsPaneToggleButtonVisible
    {
        get => GetValue(IsPaneToggleButtonVisibleProperty);
        set => SetValue(IsPaneToggleButtonVisibleProperty, value);
    }

    /// <summary>菜单项集合（元素类型为 <see cref="NavigationViewItem"/> 或 <see cref="NavigationViewItemSeparator"/>）。</summary>
    public AvaloniaList<object> MenuItems { get; } = new();

    public event EventHandler<NavigationViewItemInvokedEventArgs>? ItemInvoked;


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty)
        {
            _contentPresenter.Content = Content;
        }
        else if (change.Property == CompactPaneLengthProperty)
        {
            Width = -1;
            RebuildPane();
        }
    }


    private void RebuildPane()
    {
        _pane.Children.Clear();
        _buttons.Clear();
        _pane.Width = CompactPaneLength;
        _pane.Background = Brushes.Transparent;
        if (PaneCustomContent is Control custom)
        {
            _pane.Children.Add(custom);
        }
        foreach (object item in MenuItems)
        {
            switch (item)
            {
                case NavigationViewItemSeparator:
                    _pane.Children.Add(new Border { Height = 1, Margin = new Thickness(8, 4), Background = new SolidColorBrush(Color.FromArgb(0x20, 0x80, 0x80, 0x80)) });
                    break;
                case NavigationViewItem navItem:
                    var button = CreateButton(navItem);
                    _buttons[navItem] = button;
                    _pane.Children.Add(button);
                    break;
            }
        }
        ApplySelectionVisual();
    }


    private ToggleButton CreateButton(NavigationViewItem item)
    {
        var button = new ToggleButton
        {
            Width = 40,
            Height = 40,
            Margin = new Thickness(8, 2),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            IsChecked = item.IsSelected,
            Tag = item,
        };
        if (item.Icon is Control icon)
        {
            button.Content = icon;
        }
        if (item.Content is Control label)
        {
            ToolTip.SetTip(button, label is TextBlock tb ? tb.Text : item.Content);
        }
        button.Click += (_, _) =>
        {
            SelectedItem = item;
            ItemInvoked?.Invoke(this, new NavigationViewItemInvokedEventArgs { InvokedItemContainer = item });
        };
        return button;
    }


    private void ApplySelectionVisual()
    {
        foreach ((NavigationViewItem item, ToggleButton button) in _buttons)
        {
            button.IsChecked = ReferenceEquals(SelectedItem, item);
        }
    }

}
