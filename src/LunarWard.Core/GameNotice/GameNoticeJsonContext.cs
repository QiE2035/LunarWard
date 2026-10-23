using System.Text.Json.Serialization;

namespace LunarWard.Core.GameNotice;


[JsonSerializable(typeof(miHoYoApiWrapper<AlertAnn>))]
internal partial class GameNoticeJsonContext : JsonSerializerContext
{

}
