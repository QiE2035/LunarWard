using LunarWard.Core.HoYoPlay;

namespace LunarWard.Features.GameSelector;

/// <summary>
/// 游戏选择器里的一个游戏（含各区服）。<b>原样搬自</b> <c>Starward/src/Starward/Features/GameSelector/GameBizDisplay.cs</c>。
/// </summary>
public class GameBizDisplay
{

    public GameInfo GameInfo { get; set; } = null!;


    public List<GameBizIcon> Servers { get; set; } = new();

}
