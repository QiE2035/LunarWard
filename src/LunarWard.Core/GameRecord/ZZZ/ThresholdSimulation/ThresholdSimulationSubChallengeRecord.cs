using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.ZZZ.ThresholdSimulation;

public class ThresholdSimulationSubChallengeRecord
{

    [JsonPropertyName("battle_id")]
    public int BattleId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    /// <summary>
    /// S A B C
    /// </summary>
    [JsonPropertyName("star")]
    public string Star { get; set; } = null!;

    [JsonPropertyName("avatar_list")]
    public List<ZZZAvatar> AvatarList { get; set; } = [];

    [JsonPropertyName("buddy")]
    public ZZZBuddy Buddy { get; set; } = null!;

    [JsonPropertyName("buffer")]
    public ThresholdSimulationBuff Buff { get; set; } = null!;


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

}