using System.Text.Json.Serialization;

namespace LunarWard.Core.SelfQuery;


/// <summary>
/// 账号记录查询 API 的序列化上下文（与持久化无关；持久化走 <c>SelfQueryLogStore</c> 的 SQLite 表）。
/// </summary>
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
