using LunarWard.App.ViewModels.Starward;

namespace LunarWard.App.Views.Starward.Pages;

public partial class ToolboxPage : StarwardPage
{
    private readonly ToolboxPageViewModel? _viewModel;


    public ToolboxPage(PageContext context) : base(context)
    {
        InitializeComponent();
        _viewModel = new ToolboxPageViewModel(context);
        DataContext = _viewModel;
    }


    public override void OnNavigatedTo()
    {
        // 账号列表可能被别的入口改过（保存/切换），进入页面时重新读一次
        _viewModel?.Accounts.ReloadCommand.Execute(null);
    }


    /// <summary>自检辅助：切到指定 Tab（0 = 每日便笺，1 = 账号记录）。</summary>
    public void SelectTab(int index)
    {
        if (index == 1)
        {
            _viewModel?.ShowAccountsCommand.Execute(null);
        }
        else
        {
            _viewModel?.ShowDailyNoteCommand.Execute(null);
        }
    }


    protected override void DisposeViewModel(PageContext context)
    {
        _viewModel?.Dispose();
    }
}
