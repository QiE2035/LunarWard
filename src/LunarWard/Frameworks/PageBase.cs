using Avalonia;
using Avalonia.Controls;
using LunarWard.Core;
using LunarWard.Core.HoYoPlay;

namespace LunarWard.Frameworks;

/// <summary>
/// 上游 <c>Starward.Frameworks.PageBase</c> 的 Avalonia 等价物。
/// <para>
/// 上游继承 WinUI <c>Page</c>，用 <c>OnNavigatedTo(NavigationEventArgs)</c> 接收 GameId；
/// Avalonia 没有 Frame/NavigationEventArgs，导航由宿主直接调 <see cref="OnNavigatedTo(object?)"/>，
/// 其余（Loaded/Unloaded 钩子、GameId/GameBiz 属性、可绑定）保持上游语义。
/// </para>
/// <para>
/// 属性用 Avalonia DirectProperty 注册：<c>AvaloniaObject</c> 自身已经实现
/// <c>INotifyPropertyChanged</c>，不能再挂 <c>[INotifyPropertyChanged]</c>（MVVMTK0001）。
/// </para>
/// </summary>
public abstract class PageBase : UserControl
{

    public static readonly DirectProperty<PageBase, GameId> CurrentGameIdProperty =
        AvaloniaProperty.RegisterDirect<PageBase, GameId>(nameof(CurrentGameId), o => o.CurrentGameId);

    public static readonly DirectProperty<PageBase, GameBiz> CurrentGameBizProperty =
        AvaloniaProperty.RegisterDirect<PageBase, GameBiz>(nameof(CurrentGameBiz), o => o.CurrentGameBiz);


    private GameId _currentGameId = null!;

    private GameBiz _currentGameBiz;


    public GameId CurrentGameId
    {
        get => _currentGameId;
        protected set => SetAndRaise(CurrentGameIdProperty, ref _currentGameId, value);
    }


    public GameBiz CurrentGameBiz
    {
        get => _currentGameBiz;
        protected set => SetAndRaise(CurrentGameBizProperty, ref _currentGameBiz, value);
    }



    public PageBase()
    {
        Loaded += (_, _) => OnLoaded();
        Unloaded += (_, _) => OnUnloaded();
    }



    /// <summary>
    /// 等价上游 <c>OnNavigatedTo</c>：参数可以是 <see cref="GameId"/> 或 <see cref="GameBiz"/>。
    /// </summary>
    public virtual void OnNavigatedTo(object? parameter)
    {
        if (parameter is GameId id)
        {
            CurrentGameId = id;
            CurrentGameBiz = id.GameBiz;
        }
        else if (parameter is GameBiz biz)
        {
            CurrentGameBiz = biz;
            CurrentGameId = GameId.FromGameBiz(biz)!;
        }
    }



    protected virtual void OnLoaded()
    {

    }



    protected virtual void OnUnloaded()
    {

    }

}
