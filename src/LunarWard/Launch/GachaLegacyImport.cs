using System.Globalization;
using System.Text.Json;

namespace LunarWard.Launch;

/// <summary>
/// 旧版抽卡记录导入（Starward <c>GachaLogService.ImportGachaLogAsync</c>：
/// 原神 UIGF v3.0、绝区零 JSON 都是 <c>{ "info": {...}, "list": [...] }</c> 信封）。
/// </summary>
/// <remarks>
/// 本仓库只做「读文件 → 统一记录」这一步，去重与落盘交给
/// <see cref="GachaService.MergeById"/> 和 <c>GachaLogStore</c>。
/// 其它工具导出的裸数组也接受（没有 <c>info</c> 时只校验记录本身）。
/// </remarks>
public static class GachaLegacyImport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };


    /// <summary>
    /// 解析旧版导出文件。<paramref name="gameKind"/> 决定卡池名与校验口径（<c>genshin</c> / <c>zzz</c>）。
    /// </summary>
    public static IReadOnlyList<GachaRecord> Parse(string json, string gameKind)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new GachaLegacyImportException("文件是空的。");
        }

        List<LegacyGachaItem> items;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            items = doc.RootElement.ValueKind switch
            {
                JsonValueKind.Array => JsonSerializer.Deserialize<List<LegacyGachaItem>>(doc.RootElement.GetRawText(), JsonOptions) ?? [],
                JsonValueKind.Object => ParseEnvelope(doc.RootElement),
                _ => throw new GachaLegacyImportException("文件不是 JSON 对象或数组。"),
            };
        }
        catch (JsonException ex)
        {
            throw new GachaLegacyImportException("文件不是合法的 UIGF v3.0 / JSON。", ex);
        }

        if (items.Count == 0)
        {
            throw new GachaLegacyImportException("文件里没有抽卡记录。");
        }

        var records = new List<GachaRecord>(items.Count);
        foreach (LegacyGachaItem item in items)
        {
            if (item.Uid <= 0)
            {
                throw new GachaLegacyImportException("记录缺少 uid。");
            }
            if (item.Id <= 0)
            {
                throw new GachaLegacyImportException("记录缺少 id。");
            }
            if (item.RankType is < 3 or > 5)
            {
                throw new GachaLegacyImportException($"记录 {item.Id} 的 rank_type 不是 3/4/5。");
            }
            if (!TryParseTime(item.Time, out DateTime time))
            {
                throw new GachaLegacyImportException($"记录 {item.Id} 的时间无法解析：{item.Time}");
            }

            records.Add(new GachaRecord
            {
                Id = item.Id,
                Name = item.Name ?? "",
                ItemType = item.ItemType ?? "",
                RankType = item.RankType,
                GachaType = item.GachaType,
                GachaTypeName = GachaService.GetPoolName(gameKind, item.GachaType),
                Time = time,
                Uid = item.Uid,
                IsUp = item.IsUp is "1" or "true" or "True",
                ItemId = item.ItemId,
                Count = item.Count <= 0 ? 1 : item.Count,
            });
        }

        return records;
    }


    /// <summary>读<b>文件</b>（UTF-8），失败抛 <see cref="GachaLegacyImportException"/>。</summary>
    public static IReadOnlyList<GachaRecord> ParseFile(string path, string gameKind)
    {
        try
        {
            return Parse(File.ReadAllText(path), gameKind);
        }
        catch (IOException ex)
        {
            throw new GachaLegacyImportException($"读取文件失败：{ex.Message}", ex);
        }
    }


    private static List<LegacyGachaItem> ParseEnvelope(JsonElement root)
    {
        // 兼容 hk4e / hkrpg 这类按区段包一层的 UIGF v4 文件？不做：v4 走 UIGF 窗口。
        if (!root.TryGetProperty("list", out JsonElement list) || list.ValueKind != JsonValueKind.Array)
        {
            throw new GachaLegacyImportException("找不到 list 数组，不是 UIGF v3.0 / SRGF v1.0 文件。");
        }

        return JsonSerializer.Deserialize<List<LegacyGachaItem>>(list.GetRawText(), JsonOptions) ?? [];
    }


    private static bool TryParseTime(string? text, out DateTime time)
    {
        if (DateTime.TryParseExact(
                text,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out time))
        {
            return true;
        }

        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
    }
}


/// <summary>旧版导入失败（文件格式不对 / 缺字段 / 读不了）。</summary>
public sealed class GachaLegacyImportException : Exception
{
    public GachaLegacyImportException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
