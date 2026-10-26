using CommunityToolkit.Mvvm.ComponentModel;
using LunarWard.Core;

namespace LunarWard.Features.GameAccount;

/// <summary>
/// 一个已保存的游戏账号快照。<b>原样搬自</b> <c>Starward/src/Starward/Features/GameAccount/GameAccount.cs</c>。
/// </summary>
public class GameAccount : ObservableObject
{

    public string SHA256 { get; set; } = "";

    public GameBiz GameBiz { get; set; }

    public long Uid { get; set => SetProperty(ref field, value); }

    public string Name { get; set => SetProperty(ref field, value); } = "";

    public byte[] Value { get; set; } = [];

    public DateTime Time { get; set; } = DateTime.Now;

    public bool IsSaved { get; set => SetProperty(ref field, value); }

}
