using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using LunarWard.App.ViewModels.Starward;

namespace LunarWard.App.Views.Starward.Pages;

/// <summary>
/// 抽卡记录行里星级的颜色：5★ 金、4★ 紫、其余二级色。
/// </summary>
/// <remarks>
/// <para>
/// 三个刷子由页面资源注入（<c>StarRecordGoldBrush</c> / <c>StarRecordPurpleBrush</c> /
/// <c>StarTextSecondary</c>，都定义在 <c>Styles/Starward.axaml</c>），
/// 因此页面里不会出现裸色值。
/// </para>
/// <para>
/// 取值对应 Starward <c>Features/Gacha/GachaStatsCard.xaml</c> 的
/// <c>Rarity5ForegroundBrush</c>（#FFA500 系，本仓库收敛为 #FFD700 的
/// <c>StarRecordGoldBrush</c>）与 <c>Rarity4ForegroundBrush</c>。
/// </para>
/// </remarks>
public sealed class GachaRarityBrushConverter : IValueConverter
{
    public IBrush? Gold { get; set; }

    public IBrush? Purple { get; set; }

    public IBrush? Other { get; set; }


    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var rank = value switch
        {
            int number => number,
            long number => (int)number,
            _ => 0,
        };

        return rank >= 5 ? Gold : rank == 4 ? Purple : Other;
    }


    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}


public partial class GachaPage : StarwardPage
{
    private GachaPageViewModel? _viewModel;


    public GachaPage(PageContext context) : base(context)
    {
        InitializeComponent();
        _viewModel = new GachaPageViewModel(context);
        DataContext = _viewModel;
    }


    protected override void DisposeViewModel(PageContext context)
    {
        _viewModel?.Dispose();
        _viewModel = null;
    }
}
