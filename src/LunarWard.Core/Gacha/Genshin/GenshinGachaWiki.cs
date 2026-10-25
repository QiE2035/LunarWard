using System.Text.Json.Serialization;

namespace LunarWard.Core.Gacha.Genshin;

public class GenshinGachaWiki
{

    [JsonPropertyName("game")]
    public string Game { get; set; } = null!;

    [JsonIgnore]
    public string Language { get; set; } = null!;

    [JsonPropertyName("all_avatar")]
    public List<GenshinGachaInfo> AllAvatar { get; set; } = [];

    [JsonPropertyName("all_weapon")]
    public List<GenshinGachaInfo> AllWeapon { get; set; } = [];

}