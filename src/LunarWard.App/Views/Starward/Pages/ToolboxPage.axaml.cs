using System.ComponentModel;
using LunarWard.App.ViewModels;
using LunarWard.App.ViewModels.Starward;

namespace LunarWard.App.Views.Starward.Pages;

/// <summary>
/// 工具箱页（Starward <c>GameRecordPage</c> 的容器）：视图层只负责打开
/// LunarWard 独有的 wine 账号快照窗口；其余逻辑在 <see cref="ToolboxPageViewModel"/>。
/// </summary>
public partial class ToolboxPage : StarwardPage
{
    private readonly ToolboxPageViewModel? _viewModel;

    private StarwardPage? _hostedPage;


    public ToolboxPage(PageContext context) : base(context)
    {
        InitializeComponent();
        _viewModel = new ToolboxPageViewModel(context);
        DataContext = _viewModel;
        _viewModel.LocalAccountsRequested += OnLocalAccountsRequested;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }


    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ToolboxPageViewModel.SelectedItem) or nameof(ToolboxPageViewModel.HasRole))
        {
            UpdateHostedPage();
        }
    }


    /// <summary>
    /// 选中「深境螺旋」且有角色时挂载 <see cref="Record.SpiralAbyssPage"/>；
    /// 其余菜单（未移植）回到占位。Starward 的 Frame.Navigate 同理。
    /// </summary>
    private void UpdateHostedPage()
    {
        if (_viewModel is null)
        {
            return;
        }

        StarwardPage? page = null;
        if (_viewModel.SelectedItem?.Tag == "SpiralAbyss" && _viewModel.CurrentRole is { } role)
        {
            page = new Record.SpiralAbyssPage(Context, role);
        }

        if (ReferenceEquals(page, _hostedPage))
        {
            return;
        }

        _hostedPage?.OnNavigatedFrom();
        ContentHost.Content = page;
        _hostedPage = page;
        page?.OnNavigatedTo();
        _viewModel.HasHostedPage = page is not null;
    }


    private void OnLocalAccountsRequested(object? sender, EventArgs e)
    {
        var window = new Views.AccountWindow
        {
            DataContext = new AccountViewModel(Context.Services, Context.Profile),
        };
        window.Show();
    }


    public override void OnNavigatedTo()
    {
        base.OnNavigatedTo();
        _viewModel?.ReloadRoleIfNeeded();
        UpdateHostedPage();
    }


    /// <summary>
    /// 自检辅助（旧 <c>--tab</c> 开关）：0/1 选中对应战绩菜单，便于截取占位版式。
    /// </summary>
    public void SelectTab(int index)
    {
        if (_viewModel is { } vm && index >= 0 && index < vm.MenuItems.Count)
        {
            vm.SelectedItem = vm.MenuItems[index];
        }
    }


    protected override void DisposeViewModel(PageContext context)
    {
        _hostedPage?.OnNavigatedFrom();
        _hostedPage = null;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.LocalAccountsRequested -= OnLocalAccountsRequested;
            _viewModel.Dispose();
        }
    }
}
