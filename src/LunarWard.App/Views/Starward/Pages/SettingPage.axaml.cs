using LunarWard.App.ViewModels.Starward;
using LunarWard.App.Views.Starward.Settings;

namespace LunarWard.App.Views.Starward.Pages;

/// <summary>
/// 设置页（Starward <c>SettingPage</c>）：260px 二级导航 + 右侧子页 Frame。
/// </summary>
/// <remarks>
/// <para>
/// 子页用 <c>ContentControl</c> + 字典缓存实例（Starward 的 Frame 也是这样：同一次
/// 设置会话里切回某个子页时状态保留）。
/// </para>
/// <para>
/// 外壳把页面实例缓存在 <c>StarwardShell</c> 里，离开设置页时会调
/// <see cref="DisposeViewModel"/> 释放 VM；再次进入时同一个实例被复用，所以
/// <see cref="OnNavigatedTo"/> 必须能重建 VM 与子页，否则会拿到已释放的对象。
/// </para>
/// </remarks>
public partial class SettingPage : StarwardPage
{
    private readonly Dictionary<SettingSectionKind, SettingSectionView> _views = [];

    private SettingPageViewModel? _viewModel;


    private bool _switching;


    public SettingPage(PageContext context) : base(context)
    {
        InitializeComponent();
        Build();
    }


    public override void OnNavigatedTo()
    {
        base.OnNavigatedTo();
        if (_viewModel is null)
        {
            Build();
        }
    }


    protected override void DisposeViewModel(PageContext context) => Clear();


    /// <summary>创建 VM 并选中当前子页（构造与"从别页回来"都走这里）。</summary>
    private void Build()
    {
        _viewModel = new SettingPageViewModel(Context);
        DataContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ShowSection(_viewModel.SelectedSection);
    }


    private void Clear()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        foreach (var view in _views.Values)
        {
            view.Dispose();
        }

        _views.Clear();
        SectionHost.Content = null;
        _viewModel?.Dispose();
        _viewModel = null;
        DataContext = null;
    }


    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingPageViewModel.SelectedSection) && _viewModel is { } vm)
        {
            ShowSection(vm.SelectedSection);
        }
    }


    /// <summary>切换右侧子页（首次访问时创建并缓存）。</summary>
    private void ShowSection(SettingSectionViewModel section)
    {
        if (_switching)
        {
            return;
        }

        _switching = true;
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
            _switching = false;
        }
    }
}
