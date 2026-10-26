using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Threading;

namespace LunarWard.App.Views.Starward;

/// <summary>
/// Starward 左栏：固定 56px 图标模式 + 悬停 Tooltip。
/// </summary>
/// <remarks>
/// Starward 的 <c>NavigationView</c> 设了 <c>PaneDisplayMode="LeftCompact"</c> 且
/// <c>IsPaneToggleButtonVisible="False"</c>，所以面板**不会**展开——只有图标和 Tooltip。
/// 早期实现按"悬停展开"做过一版，与 Starward 的实际行为不符，已回退。
/// <para>
/// 选中指示条是全栏唯一的一条：这里监听 ShellViewModel 的 <c>IsXxxSelected</c>，把指示条
/// 挪到当前选中按钮的实际位置上（<c>Margin</c> 交给 XAML 里的 <c>ThicknessTransition</c> 做 180ms
/// 的位置过渡）。这样切换页面时是「滑过去」而不是「一条消失、另一条瞬间出现」。
/// </para>
/// </remarks>
public partial class NavigationRail : UserControl
{
    private Border? _indicator;
    private StackPanel? _items;
    private Button? _settingsButton;
    private INotifyPropertyChanged? _viewModel;
    private bool _positioned;

    public NavigationRail()
    {
        InitializeComponent();
        _indicator = this.FindControl<Border>("RailIndicator");
        _items = this.FindControl<StackPanel>("RailItems");
        _settingsButton = this.FindControl<Button>("SettingsButton");
        DataContextChanged += (_, _) => AttachViewModel();
        LayoutUpdated += OnLayoutUpdated;
    }


    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AttachViewModel();
        UpdateIndicator(animate: false);
    }


    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DetachViewModel();
    }


    private void AttachViewModel()
    {
        DetachViewModel();
        if (DataContext is INotifyPropertyChanged vm)
        {
            _viewModel = vm;
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }


    private void DetachViewModel()
    {
        if (_viewModel is { } vm)
        {
            vm.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }
    }


    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // ShellViewModel 的选中态都是一组 IsXxxSelected（见 ShellViewModel.SelectPage）
        if (e.PropertyName is not { Length: > 2 } name
            || !name.StartsWith("Is", StringComparison.Ordinal)
            || !name.EndsWith("Selected", StringComparison.Ordinal))
        {
            return;
        }

        // 等类名/布局这一轮变更落定再量位置（按钮位置本身不变，但选中类刚切过来）
        Dispatcher.UIThread.Post(() => UpdateIndicator(animate: _positioned), DispatcherPriority.Background);
    }


    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_positioned)
        {
            LayoutUpdated -= OnLayoutUpdated;
            return;
        }

        UpdateIndicator(animate: false);
    }


    /// <summary>把共享指示条挪到当前选中的菜单项左侧。</summary>
    private void UpdateIndicator(bool animate)
    {
        if (_indicator is null || _items is null)
        {
            return;
        }

        Button? selected = _items.Children
            .OfType<Button>()
            .FirstOrDefault(x => x.Classes.Contains("is-selected"))
            ?? (_settingsButton is { } settings && settings.Classes.Contains("is-selected") ? settings : null);

        if (selected is null)
        {
            _indicator.Opacity = 0;
            return;
        }

        if (selected.TranslatePoint(new Point(0, 0), this) is not { } origin)
        {
            return;
        }

        var target = new Thickness(
            origin.X + 2,
            origin.Y + Math.Max(0, (selected.Bounds.Height - _indicator.Height) / 2),
            0,
            0);

        // 首次定位直接落位，不做过渡：否则启动时能看到指示条从左上角滑进来。
        if (!animate)
        {
            _indicator.Transitions = null;
        }

        _indicator.Margin = target;
        _indicator.Opacity = 1;

        if (!animate)
        {
            _indicator.ClearValue(Animatable.TransitionsProperty);
        }

        _positioned = true;
    }
}
