using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.StarRail.ChallengePeak;

public class ChallengePeakRole
{
    [JsonPropertyName("server")]
    public string Server { get; set; } = null!;

    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = null!;

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("role_id")]
    public string RoleId { get; set; } = null!;


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}