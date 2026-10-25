using LunarWard.App.ViewModels.Starward;
using LunarWard.Core.GameRecord;

namespace LunarWard.App.Views.Starward.Pages.Record;

/// <summary>
/// 开拓月历页（Starward <c>Features/GameRecord/StarRail/TrailblazeCalendarPage</c>）。
/// </summary>
public partial class TrailblazeCalendarPage : StarwardPage
{
    public TrailblazeCalendarPage(PageContext context, GameRecordRole role) : base(context)
    {
        InitializeComponent();
        DataContext = new TrailblazeCalendarPageViewModel(context, role);
    }
}
