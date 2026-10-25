using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.ZZZ.ShiyuDefense;

public class ShiyuDefenseWrapper
{

    [JsonPropertyName("hadal_ver")]
    public string HadalVer { get; set; } = null!;

    [JsonPropertyName("hadal_info_v1")]
    public ShiyuDefenseInfo? InfoV1 { get; set; }

    [JsonPropertyName("hadal_info_v2")]
    public ShiyuDefenseInfoV2? InfoV2 { get; set; }

    [JsonPropertyName("nick_name")]
    public string NickName { get; set; } = null!;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = null!;

}