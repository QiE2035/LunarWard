using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.ZZZ.DeadlyAssault;

/// <summary>
/// 危局强袭战Boss
/// </summary>
public class DeadlyAssaultBoss
{

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = null!;

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("race_icon")]
    public string RaceIcon { get; set; } = null!;

    [JsonPropertyName("bg_icon")]
    public string BgIcon { get; set; } = null!;


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

}



