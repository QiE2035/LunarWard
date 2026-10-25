using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.Genshin.ImaginariumTheater;

public class ImaginariumTheaterFightStatisicAvatar
{

    [JsonPropertyName("avatar_id")]
    public int AvatarId { get; set; }


    [JsonPropertyName("avatar_icon")]
    public string AvatarIcon { get; set; } = null!;


    [JsonPropertyName("value")]
    public string Value { get; set; } = null!;


    [JsonPropertyName("rarity")]
    public int Rarity { get; set; }


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

}

