using LunarWard.Core.JsonConverter;
using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.ZZZ.ShiyuDefense;

public class ShiyuDefenseV2FifthLayerChallengeInfo : ShiyuDefenseV2FourthLayerChallengeInfo
{
    [JsonPropertyName("rating")]
    public string Rating { get; set; } = null!;

    [JsonPropertyName("buffer")]
    public ShiyuDefenseBuff Buff { get; set; }

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("monster_pic")]
    public string MonsterPic { get; set; } = null!;

    [JsonPropertyName("max_score")]
    public int MaxScore { get; set; }

    [JsonPropertyName("challenge_time")]
    [JsonConverter(typeof(DateTimeObjectJsonConverter))]
    public DateTime ChallengeTime { get; set; }
}