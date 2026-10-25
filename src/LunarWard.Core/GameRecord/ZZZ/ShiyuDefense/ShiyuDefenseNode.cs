using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.ZZZ.ShiyuDefense;


public class ShiyuDefenseNode
{

    [JsonPropertyName("avatars")]
    public List<ZZZAvatar> Avatars { get; set; } = [];

    [JsonPropertyName("buddy")]
    public ZZZBuddy Buddy { get; set; } = null!;

    /// <summary>
    /// 有利属性
    /// </summary>
    [JsonPropertyName("element_type_list")]
    public List<int> ElementTypeList { get; set; } = [];

    /// <summary>
    /// 敌人
    /// </summary>
    [JsonPropertyName("monster_info")]
    public ShiyuDefenseMonsterInfo MonsterInfo { get; set; } = null!;

    /// <summary>
    /// 战斗时间，秒
    /// </summary>
    [JsonPropertyName("battle_time")]
    public int BattleTime { get; set; }


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

}



