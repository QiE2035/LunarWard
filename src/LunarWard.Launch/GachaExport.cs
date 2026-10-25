using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LunarWard.Launch;

/// <summary>
/// 抽卡记录导出（Starward <c>GenshinGachaService.ExportAsJsonAsync</c> /
/// <c>StarRailGachaService</c> / <c>ZZZGachaService</c> 的旧版格式）。
/// </summary>
/// <remarks>
/// 三个游戏的导出信封完全一致：<c>{ "info": {...}, "list": [...] }</c>，
/// 差别只在于：原神每条记录多一个 <c>uigf_gacha_type</c>（400 归并到 301），
/// 星铁的 <c>info</c> 里 <c>srgf_version</c> 为 <c>v1.0</c>、<c>uigf_version</c> 仍写着 <c>v3.0</c>（上游同款）。
/// Excel 导出（MiniExcel 模板）暂未移植。
/// </remarks>
public static class GachaExport
{
    /// <summary>旧格式名称（Starward <c>LegacyFormatName</c> 随游戏变化）。</summary>
    public static string LegacyFormatName(string gameKind) => gameKind switch
    {
        "starrail" or "hkrpg" => "SRGF v1.0",
        "zzz" => "JSON",
        _ => "UIGF v3.0",
    };


    /// <summary>导出文件名里的标准名（与旧格式名称一致，去掉空格）。</summary>
    public static string FileName(string gameKind, long uid, DateTime time)
        => $"{LegacyFormatName(gameKind).Replace(' ', '-')}-{uid}-{time:yyyyMMdd-HHmmss}.json";


    /// <summary>
    /// 生成 UIGF v3.0 / SRGF v1.0 JSON（缩进、中文不转义，与上游 <c>AppConfig.JsonSerializerOptions</c> 一致）。
    /// </summary>
    public static string BuildJson(
        IReadOnlyList<GachaRecord> records,
        string gameKind,
        long uid,
        string lang,
        string appVersion)
    {
        var ordered = records.Where(r => r.Uid == uid).OrderBy(r => r.Id).ToList();
        var time = DateTimeOffset.Now;

        var file = new LegacyGachaFile
        {
            Info = new LegacyGachaFileInfo
            {
                Uid = uid,
                Lang = lang,
                ExportTimestamp = time.ToUnixTimeSeconds(),
                ExportTime = time.ToString("yyyy-MM-dd HH:mm:ss"),
                ExportApp = "LunarWard",
                ExportAppVersion = appVersion,
                // 原神按 UIGF v3.0，星铁按 SRGF v1.0；两个字段上游都会写，保持同款
                UigfVersion = "v3.0",
                SrgfVersion = "v1.0",
                RegionTimeZone = uid.ToString()[0] switch
                {
                    '6' => -5,
                    '7' => 1,
                    _ => 8,
                },
            },
            List = ordered.Select(r => new LegacyGachaItem
            {
                Uid = r.Uid,
                GachaType = r.GachaType,
                // 原神每条都要写 uigf_gacha_type（400 集录祈愿归并到 301），其它游戏不写
                UigfGachaType = gameKind is "genshin" or "hk4e"
                    ? (r.GachaType == 400 ? 301 : r.GachaType)
                    : null,
                ItemId = r.ItemId,
                Count = r.Count,
                Time = r.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                Name = r.Name,
                ItemType = r.ItemType,
                RankType = r.RankType,
                Id = r.Id,
                IsUp = r.IsUp ? "1" : "0",
            }).ToList(),
        };

        return JsonSerializer.Serialize(file, JsonOptions);
    }


    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}


/// <summary>UIGF v3.0 / SRGF v1.0 的文件信封。</summary>
public sealed class LegacyGachaFile
{
    [JsonPropertyName("info")]
    public LegacyGachaFileInfo Info { get; set; } = new();

    [JsonPropertyName("list")]
    public List<LegacyGachaItem> List { get; set; } = [];
}


/// <summary>信封里的 <c>info</c>（<c>UIAF3FileInfo</c>）。</summary>
public sealed class LegacyGachaFileInfo
{
    [JsonPropertyName("uid")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long Uid { get; set; }

    [JsonPropertyName("lang")]
    public string Lang { get; set; } = "";

    [JsonPropertyName("export_timestamp")]
    public long ExportTimestamp { get; set; }

    [JsonPropertyName("export_time")]
    public string ExportTime { get; set; } = "";

    [JsonPropertyName("export_app")]
    public string ExportApp { get; set; } = "LunarWard";

    [JsonPropertyName("export_app_version")]
    public string ExportAppVersion { get; set; } = "";

    [JsonPropertyName("uigf_version")]
    public string UigfVersion { get; set; } = "v3.0";

    [JsonPropertyName("srgf_version")]
    public string SrgfVersion { get; set; } = "v1.0";

    [JsonPropertyName("region_time_zone")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int RegionTimeZone { get; set; }
}


/// <summary>信封里的一条记录。</summary>
public sealed class LegacyGachaItem
{
    [JsonPropertyName("uid")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long Uid { get; set; }

    [JsonPropertyName("gacha_type")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int GachaType { get; set; }

    /// <summary>仅原神：每条都写，400（集录祈愿）归并到 301。</summary>
    [JsonPropertyName("uigf_gacha_type")]
    public int? UigfGachaType { get; set; }

    [JsonPropertyName("item_id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int ItemId { get; set; }

    [JsonPropertyName("count")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int Count { get; set; } = 1;

    [JsonPropertyName("time")]
    public string Time { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("item_type")]
    public string ItemType { get; set; } = "";

    [JsonPropertyName("rank_type")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int RankType { get; set; }

    [JsonPropertyName("id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long Id { get; set; }

    /// <summary>绝区零 / 星铁接口带的 <c>is_up</c>（UIGF 扩展字段）。</summary>
    [JsonPropertyName("is_up")]
    public string IsUp { get; set; } = "0";
}
