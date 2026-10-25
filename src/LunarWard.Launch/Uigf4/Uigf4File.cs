using System.Text.Json.Serialization;

namespace LunarWard.Launch.Uigf4;

/// <summary>
/// UIGF v4.2 文件（上游 <c>Features/Gacha/UIGF/UIGF4File.cs</c>）。
/// </summary>
/// <remarks>
/// 与上游一致：<c>hk4e</c> / <c>hkrpg</c> / <c>nap</c> / <c>hk4e_ugc</c> 四个区段各是一组
/// 「游戏 + UID」的 archive。LunarWard 目前只写回前三个（千星奇域抽卡页的记录格式与
/// 原神不同，尚未并入同一份存档），<c>hk4e_ugc</c> 只读不写。
/// </remarks>
public sealed class Uigf4File
{
    [JsonPropertyName("info")]
    public Uigf4FileInfo Info { get; set; } = new();


    [JsonPropertyName("hk4e")]
    public List<Uigf4Archive<Uigf4GenshinItem>>? GenshinArchives { get; set; } = [];


    [JsonPropertyName("hkrpg")]
    public List<Uigf4Archive<Uigf4GachaItem>>? StarRailArchives { get; set; } = [];


    [JsonPropertyName("nap")]
    public List<Uigf4Archive<Uigf4GachaItem>>? ZzzArchives { get; set; } = [];


    /// <summary>千星奇域（v4.2 新增）。只读：导入时单独成行并提示暂不支持写入。</summary>
    [JsonPropertyName("hk4e_ugc")]
    public List<Uigf4Archive<Uigf4GenshinBeyondItem>>? GenshinBeyondArchives { get; set; } = [];
}


/// <summary>UIGF v4.2 信封里的 <c>info</c>。</summary>
public sealed class Uigf4FileInfo
{
    /// <summary>导出时间戳（秒）。</summary>
    [JsonPropertyName("export_timestamp")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long ExportTimestamp { get; set; }


    [JsonPropertyName("export_time")]
    public string ExportTime { get; set; } = "";


    [JsonPropertyName("export_app")]
    public string ExportApp { get; set; } = "LunarWard";


    [JsonPropertyName("export_app_version")]
    public string ExportAppVersion { get; set; } = "";


    [JsonPropertyName("version")]
    public string Version { get; set; } = "v4.2";
}


/// <summary>
/// 一个「游戏 + UID」的存档区段。
/// </summary>
public sealed class Uigf4Archive<T>
{
    [JsonPropertyName("uid")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long Uid { get; set; }


    /// <summary>服务器时区（原神 6 开头 -5、7 开头 1，其余 8；与上游同款推断）。</summary>
    [JsonPropertyName("timezone")]
    public int Timezone { get; set; }


    /// <summary>
    /// 语言代码。标准定义了 enum，空字符串不是合法取值，
    /// 因此未知语言时整个省略该字段（<c>JsonIgnore</c> + null）。
    /// </summary>
    [JsonPropertyName("lang")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Lang { get; set; }


    [JsonPropertyName("list")]
    public List<T> List { get; set; } = [];
}


/// <summary>UIGF v4.2 的一条抽卡记录（三个游戏共有字段）。</summary>
public class Uigf4GachaItem
{
    [JsonPropertyName("uid")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long Uid { get; set; }


    [JsonPropertyName("id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long Id { get; set; }


    [JsonPropertyName("gacha_type")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public int GachaType { get; set; }


    [JsonPropertyName("name")]
    public string Name { get; set; } = "";


    [JsonPropertyName("item_type")]
    public string ItemType { get; set; } = "";


    [JsonPropertyName("rank_type")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public int RankType { get; set; }


    /// <summary>服务器时区的时间，<c>yyyy-MM-dd HH:mm:ss</c>。</summary>
    [JsonPropertyName("time")]
    public string Time { get; set; } = "";


    [JsonPropertyName("item_id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public int ItemId { get; set; }


    [JsonPropertyName("count")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public int Count { get; set; } = 1;
}


/// <summary>原神记录：额外带 <c>uigf_gacha_type</c>（400 集录祈愿归并到 301，与上游一致）。</summary>
public sealed class Uigf4GenshinItem : Uigf4GachaItem
{
    [JsonPropertyName("uigf_gacha_type")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public int UigfGachaType { get; set; }
}


/// <summary>千星奇域记录（<c>hk4e_ugc</c>）：只用于读取上游导出的文件。</summary>
public sealed class Uigf4GenshinBeyondItem
{
    [JsonPropertyName("uid")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long Uid { get; set; }


    [JsonPropertyName("id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long Id { get; set; }


    [JsonPropertyName("region")]
    public string Region { get; set; } = "";


    [JsonPropertyName("op_gacha_type")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public int OpGachaType { get; set; }


    [JsonPropertyName("schedule_id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public int ScheduleId { get; set; }


    [JsonPropertyName("item_type")]
    public string ItemType { get; set; } = "";


    [JsonPropertyName("name")]
    public string Name { get; set; } = "";


    [JsonPropertyName("item_id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public int ItemId { get; set; }


    [JsonPropertyName("rank_type")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public int RankType { get; set; }


    [JsonPropertyName("time")]
    public string Time { get; set; } = "";


    [JsonPropertyName("count")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public int Count { get; set; } = 1;
}
