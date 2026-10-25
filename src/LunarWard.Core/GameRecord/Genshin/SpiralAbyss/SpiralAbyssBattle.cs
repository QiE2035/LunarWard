using LunarWard.Core.JsonConverter;
using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.Genshin.SpiralAbyss;


/// <summary>
/// 深境螺旋一场战斗
/// </summary>
public class SpiralAbyssBattle
{

    [JsonPropertyName("index")]
    public int Index { get; set; }


    [JsonPropertyName("timestamp")]
    [JsonConverter(typeof(TimestampStringJsonConverter))]
    public DateTimeOffset Time { get; set; }


    [JsonPropertyName("avatars")]
    public List<SpiralAbyssAvatar> Avatars { get; set; } = [];


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

}
