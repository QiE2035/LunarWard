using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.ZZZ.ThresholdSimulation;

public class ThresholdSimulationBossChallengeRecord
{

    /// <summary>
    /// BOSS 信息
    /// </summary>
    [JsonPropertyName("boss_info")]
    public ThresholdSimulationBossInfo BossInfo { get; set; } = null!;

    /// <summary>
    /// 挑战信息
    /// </summary>
    [JsonPropertyName("main_challenge_record")]
    public ThresholdSimulationMainChallengeRecord MainChallengeRecord { get; set; } = null!;


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

}
