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


    public ToolboxPage(PageContext context) : base(context)
    {
        InitializeComponent();
        _viewModel = new ToolboxPageViewModel(context);
        DataContext = _viewModel;
        _viewModel.LocalAccountsRequested += OnLocalAccountsRequested;
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
        if (_viewModel is not null)
        {
            _viewModel.LocalAccountsRequested -= OnLocalAccountsRequested;
            _viewModel.Dispose();
        }
    }
}
