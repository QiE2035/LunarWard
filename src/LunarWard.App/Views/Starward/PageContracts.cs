using LunarWard.App.Services;
using LunarWard.App.ViewModels;
using LunarWard.App.ViewModels.Starward;

namespace LunarWard.App.Views.Starward;

/// <summary>Starward 左侧导航栏的页面。顺序与 Starward MainView 一致。</summary>
public enum ShellPage
{
    Launcher,
    GameSetting,
    Screenshot,
    Gacha,
    Toolbox,
    Settings,
}


/// <summary>
/// 页面构造上下文。每个 Starward 页面都是 <c>UserControl</c>，构造函数接收本对象并自行设置 DataContext。
/// </summary>
public sealed class PageContext
{
    public required AppServices Services { get; init; }

    public required ShellViewModel Shell { get; init; }

    /// <summary>当前游戏档案。切换游戏后面页会被重建，因此页面内可安全缓存。</summary>
    public required ProfileItemViewModel Profile { get; init; }

    public required LogService Log { get; init; }
}
