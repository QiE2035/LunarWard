using LunarWard.Core.JsonConverter;
using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.StarRail.ForgottenHall;

public class ForgottenHallNode
{
    [JsonPropertyName("challenge_time")]
    [JsonConverter(typeof(DateTimeObjectJsonConverter))]
    public DateTime ChallengeTime { get; set; }

    [JsonPropertyName("avatars")]
    public List<ForgottenHallAvatar> Avatars { get; set; } = [];


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}


