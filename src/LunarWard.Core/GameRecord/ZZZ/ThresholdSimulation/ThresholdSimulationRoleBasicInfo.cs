using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.ZZZ.ThresholdSimulation;

public class ThresholdSimulationRoleBasicInfo
{

    [JsonPropertyName("server")]
    public string Server { get; set; } = null!;


    [JsonPropertyName("nickname")]
    public string NickName { get; set; } = null!;


    [JsonPropertyName("icon")]
    public string Icon { get; set; } = null!;


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

}