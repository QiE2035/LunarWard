using System.Text.Json.Serialization;

namespace LunarWard.Core.SelfQuery;


[JsonSerializable(typeof(miHoYoApiWrapper<SelfQueryUserInfo>))]
[JsonSerializable(typeof(miHoYoApiWrapper<SelfQueryListWrapper<GenshinQueryItem>>))]
[JsonSerializable(typeof(miHoYoApiWrapper<SelfQueryListWrapper<GenshinSelfQueryItem_BattlePath>>))]
[JsonSerializable(typeof(miHoYoApiWrapper<SelfQueryListWrapper<GenshinSelfQueryItem_BeyondBattlePath>>))]
[JsonSerializable(typeof(miHoYoApiWrapper<SelfQueryListWrapper<GenshinSelfQueryItem_BeyondCostume>>))]
[JsonSerializable(typeof(miHoYoApiWrapper<SelfQueryListWrapper<GenshinSelfQueryItem_BeyondCostumeSuit>>))]
[JsonSerializable(typeof(miHoYoApiWrapper<SelfQueryListWrapper<StarRailQueryItem>>))]
[JsonSerializable(typeof(miHoYoApiWrapper<SelfQueryListWrapper<ZZZQueryItem>>))]
internal partial class SelfQueryJsonContext : JsonSerializerContext
{

}


/// <summary>
/// <see cref="SelfQueryStore"/> 的本地文件序列化上下文。
/// （查询 API 的上下文在 <see cref="SelfQueryJsonContext"/>，两者互不影响。）
/// </summary>
[JsonSerializable(typeof(SelfQueryStoreData))]
internal partial class SelfQueryStoreJsonContext : JsonSerializerContext
{

}
