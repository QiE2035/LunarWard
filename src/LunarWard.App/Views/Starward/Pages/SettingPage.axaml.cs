using LunarWard.App.ViewModels.Starward;
using LunarWard.App.Views.Starward.Settings;

namespace LunarWard.App.Views.Starward.Pages;

/// <summary>
/// 设置页（Starward <c>SettingPage</c>）：260px 二级导航 + 右侧子页 Frame。
/// </summary>
/// <remarks>
/// 子页用 <c>ContentControl</c> + 字典缓存实例（Starward 的 Frame 默认也是这样：
/// 同一次设置会话里各子页的状态保留）。页面卸载时统一释放 VM。
/// </remarks>
public partial class SettingPage : StarwardPage
{
    private readonly SettingPageViewModel _viewModel;

    private readonly Dictionary<SettingSectionKind, SettingSectionView> _views = [];

    private bool _uploading;


    public SettingPage(PageContext context) : base(context)
    {
        InitializeComponent();

        _viewModel = new SettingPageViewModel(context);
        DataContext = _viewModel;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ShowSection(_viewModel.SelectedSection);
    }


    protected override void DisposeViewModel(PageContext context)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        foreach (var view in _views.Values)
        {
            view.Dispose();
        }

        _views.Clear();
        _viewModel.Dispose();
    }


    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingPageViewModel.SelectedSection))
        {
            ShowSection(_viewModel.SelectedSection);
        }
    }


    /// <summary>切换右侧子页（首次访问时创建并缓存）。</summary>
    private void ShowSection(SettingSectionViewModel section)
    {
        if (_uploading)
        {
            return;
        }

        _uploading = true;
        try
        {
            if (!_views.TryGetValue(section.Kind, out var view))
            {
                view = SettingSectionView.Create(section);
                _views[section.Kind] = view;
            }

            SectionHost.Content = view;
        }
        finally
        {
            _uploading = false;
        }
    }
}
