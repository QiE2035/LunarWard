using LunarWard.Core.Gacha.Genshin;
using LunarWard.Core.Gacha.StarRail;
using LunarWard.Core.Gacha.ZZZ;
using System.Text.Json.Serialization;

namespace LunarWard.Core.Gacha;


[JsonSerializable(typeof(miHoYoApiWrapper<GachaLogResult<GachaLogItem>>))]
[JsonSerializable(typeof(miHoYoApiWrapper<GachaLogResult<StarRailGachaItem>>))]
[JsonSerializable(typeof(miHoYoApiWrapper<GachaLogResult<GenshinGachaItem>>))]
[JsonSerializable(typeof(miHoYoApiWrapper<GachaLogResult<ZZZGachaItem>>))]
[JsonSerializable(typeof(miHoYoApiWrapper<GenshinBeyondGachaResult>))]
[JsonSerializable(typeof(miHoYoApiWrapper<GenshinGachaWiki>))]
[JsonSerializable(typeof(miHoYoApiWrapper<StarRailGachaWiki>))]
[JsonSerializable(typeof(miHoYoApiWrapper<StarRailGachaInfoWrapper>))]
[JsonSerializable(typeof(miHoYoApiWrapper<ZZZGachaWiki>))]
[JsonSerializable(typeof(List<GenshinBeyondGachaInfo>))]
internal partial class GachaLogJsonContext : JsonSerializerContext
{

}
