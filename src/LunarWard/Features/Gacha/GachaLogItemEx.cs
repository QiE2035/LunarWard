
using CommunityToolkit.Mvvm.ComponentModel;
using LunarWard.Core.Gacha;
using LunarWard.Core.Gacha.Genshin;
using LunarWard.Core.Gacha.StarRail;
using LunarWard.Core.Gacha.ZZZ;

namespace LunarWard.Features.Gacha;

[INotifyPropertyChanged]
public partial class GachaLogItemEx : GachaLogItem
{

    /// <summary>
    /// 不要删除，导出 Excel 时有用
    /// </summary>
    public string IdText => Id.ToString();

    /// <summary>
    /// 相同保底卡池中的顺序
    /// </summary>
    public int Index { get; set; }

    public int Pity { get; set; }

    public string Icon { get; set; }

    public double Progress => (double)Pity / ((GachaType is GenshinGachaType.WeaponEventWish or StarRailGachaType.LightConeEventWarp or StarRailGachaType.LightConeCollaborationWarp or ZZZGachaType.WEngineChannel or ZZZGachaType.WEngineReverberation or ZZZGachaType.BangbooChannel) ? 80 : 90) * 100;


    public bool IsPointerIn { get; set => SetProperty(ref field, value); }

    public int ItemCount { get; set; }


    public bool HasUpItem { get; set; }

    /// <summary>
    /// 是否 UP。<see cref="GachaLogItem.IsUp"/> 是接口原始字段（int，1 是 / 0 否），
    /// 这里是各 <c>GachaService</c> 依据 <c>GachaNoUp</c> 区间算出的派生标记（bool），故用 <c>new</c> 隐藏基类成员。
    /// </summary>
    public new bool IsUp { get; set; }

    public double UpTextOpacity => IsUp ? 1 : 0;

}
