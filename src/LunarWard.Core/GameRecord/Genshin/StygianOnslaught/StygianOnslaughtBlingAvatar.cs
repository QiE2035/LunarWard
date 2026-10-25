using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.Genshin.StygianOnslaught;

/// <summary>
/// 赋光之人
/// </summary>
public class StygianOnslaughtBlingAvatar
{

    [JsonPropertyName("avatar_id")]
    public int AvatarId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("element")]
    public string Element { get; set; } = null!;

    [JsonPropertyName("image")]
    public string Image { get; set; } = null!;

    [JsonPropertyName("rarity")]
    public int Rarity { get; set; }

    /// <summary>
    /// 最终是否上榜
    /// </summary>
    [JsonPropertyName("is_plus")]
    public bool IsPlus { get; set; }


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

}