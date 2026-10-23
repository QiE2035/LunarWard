using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Services;
using LunarWard.App.Views.Starward;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 游戏设置页（对应 Starward <c>GameSettingPage</c>）+ LunarWard 独有区块。
/// </summary>
/// <remarks>
/// <para>
/// Starward 原页只有「分辨率 / 全屏 / 无边框 / 语音 / 图形」这几项 Windows 专属设置，
/// LunarWard 那一侧是空的，因此本页 = Starward 的排版骨架（左列基础设置、右列高级设置、
/// 底部 60px 应用条） + wine 运行时 / ten-layer env / 外部程序 / 区服 / 诊断。
/// </para>
/// <para>待实现：由 T1 在 <c>docs/07-UI-REPLICA.md</c> 的契约下完成。</para>
/// </remarks>
public sealed partial class GameSettingPageViewModel : ObservableObject, IDisposable
{
    private readonly PageContext _context;


    public GameSettingPageViewModel(PageContext context)
    {
        _context = context;
        Services = context.Services;
    }


    public AppServices Services { get; }

    public Views.Starward.PageContext Context => _context;

    public string GameName => _context.Profile.DisplayName;


    [ObservableProperty]
    private string _statusText = "";


    public void Dispose()
    {
    }
}


/// <summary>占位实现，T1 完成后由真实页面替换（见 docs/07-UI-REPLICA.md §6）。</summary>
public sealed partial class PlaceholderPageViewModel(string title) : ObservableObject
{
    public string Title { get; } = title;
}
