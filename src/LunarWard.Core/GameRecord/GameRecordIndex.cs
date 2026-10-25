using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord;

/// <summary>
/// 暂时仅用于获取游戏角色头像
/// </summary>
internal class GameRecordIndex : IJsonOnDeserialized
{

    [JsonPropertyName("cur_head_icon_url")]
    public string CurHeadIconUrl { get; set; } = null!;


    [JsonPropertyName("role")]
    public GameRecordIndexRole Role { get; set; } = null!;


    [JsonIgnore]
    public string HeadIcon { get; set; } = null!;


    public void OnDeserialized()
    {
        if (!string.IsNullOrWhiteSpace(CurHeadIconUrl))
        {
            HeadIcon = CurHeadIconUrl;
        }
        else if (!string.IsNullOrWhiteSpace(Role?.AvatarUrl))
        {
            HeadIcon = Role.AvatarUrl;
        }
        else if (!string.IsNullOrWhiteSpace(Role?.GameHeadIcon))
        {
            HeadIcon = Role.GameHeadIcon;
        }
        else
        {
            HeadIcon = string.Empty;
        }
    }

}


internal class GameRecordIndexRole
{

    [JsonPropertyName("AvatarUrl")]
    public string AvatarUrl { get; set; } = null!;


    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = null!;

    [JsonPropertyName("region")]
    public string RegionName { get; set; } = null!;

    [JsonPropertyName("level")]
    public int Level { get; set; }


    [JsonPropertyName("game_head_icon")]
    public string GameHeadIcon { get; set; } = null!;

}