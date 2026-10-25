using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.ZZZ.DeadlyAssault;

/// <summary>
/// 危局强袭战Buff
/// </summary>
public class DeadlyAssaultBuff
{

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = null!;

    /// <summary>
    /// 描述，格式化富文本
    /// </summary>
    [JsonPropertyName("desc")]
    public string Desc { get; set; } = null!;

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

}



