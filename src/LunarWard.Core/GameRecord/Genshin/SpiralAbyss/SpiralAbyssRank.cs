using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.Genshin.SpiralAbyss;

/// <summary>
/// 深境螺旋最值统计
/// </summary>
public class SpiralAbyssRank
{


    [JsonPropertyName("avatar_id")]
    public int AvatarId { get; set; }


    [JsonPropertyName("avatar_icon")]
    public string AvatarIcon { get; set; } = null!;


    [JsonPropertyName("value")]
    public int Value { get; set; }


    [JsonPropertyName("rarity")]
    public int Rarity { get; set; }


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

}
