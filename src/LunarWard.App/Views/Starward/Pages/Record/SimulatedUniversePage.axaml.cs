using LunarWard.App.ViewModels.Starward;
using LunarWard.Core.GameRecord;

namespace LunarWard.App.Views.Starward.Pages.Record;

/// <summary>
/// 模拟宇宙页（Starward <c>Features/GameRecord/StarRail/SimulatedUniversePage</c>）。
/// </summary>
public partial class SimulatedUniversePage : StarwardPage
{
    public SimulatedUniversePage(PageContext context, GameRecordRole role) : base(context)
    {
        InitializeComponent();
        DataContext = new SimulatedUniversePageViewModel(context, role);
    }
}
