using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using LunarWard.Launch.Database;
using LunarWard.Launch.Gacha;

namespace LunarWard.Launch.Uigf4;

/// <summary>
/// UIGF v4.2 的导出 / 导入（上游 <c>UIGFGachaService</c> 的导出、解析部分）。
/// </summary>
/// <remarks>
/// <para>
/// 数据源是 <see cref="GachaLogStore"/>（SQLite，表结构与 Starward 的 <c>StarwardDatabase.db</c>
/// 一致）。导出时按「游戏 + UID」逐条读出记录，<c>uigf_gacha_type</c>（仅原神，400 → 301）与 <c>timezone</c>（UID 首位推断）
/// 的算法与上游逐行一致；导入时保留用户的时区偏移输入，把记录时间整体平移后再落盘。
/// </para>
/// <para>
/// 千星奇域（<c>hk4e_ugc</c>）走 <see cref="GenshinBeyondGachaStore"/>（Starward 的
/// <c>GenshinBeyondGachaItem</c> 表），导出与导入都与上游一致。
/// </para>
/// </remarks>
public sealed class Uigf4Service
{
    /// <summary>千星奇域在导出列表里的游戏标记（上游用 <c>IsGenshinBeyond</c> 布尔区分）。</summary>
    public const string BeyondGame = "genshin-beyond";


    private readonly GachaLogStore _store;

    private readonly GenshinBeyondGachaStore? _beyond;

    private readonly string _appVersion;


    public Uigf4Service(GachaLogStore store, string appVersion, GenshinBeyondGachaStore? beyond = null)
    {
        _store = store;
        _appVersion = appVersion;
        _beyond = beyond;
    }


    /// <summary>导出列表的数据源（每个「游戏 + UID」一行，千星奇域单独成行）。</summary>
    public IReadOnlyList<GachaArchiveSummary> GetArchives()
    {
        List<GachaArchiveSummary> list = [.. _store.GetSummaries()];
        if (_beyond is not null)
        {
            foreach (long uid in _beyond.GetUids())
            {
                List<Gacha.GenshinBeyondGachaItemEx> items = _beyond.GetItems(uid);
                if (items.Count == 0)
                {
                    continue;
                }
                Core.Gacha.Genshin.GenshinBeyondGachaItem last = items.OrderBy(x => x.Id).Last();
                list.Add(new GachaArchiveSummary(
                    BeyondGame, uid, items.Count, null, last.Time, last.ItemName, last.OpGachaType));
            }
        }
        return list;
    }


    /// <summary>导出文件名（上游是 <c>Starward_UIGF_{时间}.json</c>，这里换成应用自己的名字）。</summary>
    public static string FileName(DateTime time) => $"LunarWard_UIGF_{time:yyyyMMdd_HHmmss}.json";


    /// <summary>
    /// 生成 UIGF v4.2 JSON（缩进、中文不转义，与上游 <c>AppConfig.JsonSerializerOptions</c> 一致）。
    /// </summary>
    public string BuildJson(IEnumerable<GachaArchiveSummary> selections)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        var file = new Uigf4File
        {
            Info = new Uigf4FileInfo
            {
                ExportTimestamp = now.ToUnixTimeSeconds(),
                ExportTime = now.ToString("yyyy-MM-dd HH:mm:ss"),
                ExportApp = "LunarWard",
                ExportAppVersion = _appVersion,
                Version = "v4.2",
            },
        };

        foreach (var archive in selections)
        {
            switch (archive.Game)
            {
                case GachaLogStore.Genshin:
                    file.GenshinArchives!.Add(BuildGenshinArchive(archive));
                    break;
                case GachaLogStore.StarRail:
                    file.StarRailArchives!.Add(BuildStarRailArchive(archive));
                    break;
                case GachaLogStore.Zzz:
                    file.ZzzArchives!.Add(BuildArchive(archive));
                    break;
                case BeyondGame:
                    file.GenshinBeyondArchives!.Add(BuildBeyondArchive(archive));
                    break;
            }
        }

        return JsonSerializer.Serialize(file, JsonOptions);
    }


    private Uigf4Archive<Uigf4GenshinBeyondItem> BuildBeyondArchive(GachaArchiveSummary summary)
    {
        if (_beyond is null)
        {
            throw new InvalidOperationException("GenshinBeyondGachaStore is required for hk4e_ugc export.");
        }
        var archive = new Uigf4Archive<Uigf4GenshinBeyondItem>
        {
            Uid = summary.Uid,
            Timezone = GuessTimezone(summary.Uid),
            Lang = NormalizeLang(summary.Lang),
        };
        foreach (var item in _beyond.GetItems(summary.Uid).OrderBy(x => x.Id))
        {
            archive.List.Add(new Uigf4GenshinBeyondItem
            {
                Uid = item.Uid,
                Id = item.Id,
                Region = item.Region ?? "",
                OpGachaType = item.OpGachaType,
                ScheduleId = item.ScheduleId,
                ItemId = item.ItemId,
                ItemType = item.ItemType,
                Name = item.ItemName,
                RankType = item.RankType,
                IsUp = item.IsUp,
                Time = item.Time.ToString("yyyy-MM-dd HH:mm:ss"),
            });
        }
        return archive;
    }


    private Uigf4Archive<Uigf4GenshinItem> BuildGenshinArchive(GachaArchiveSummary summary)
    {
        var archive = new Uigf4Archive<Uigf4GenshinItem>
        {
            Uid = summary.Uid,
            Timezone = GuessTimezone(summary.Uid),
            Lang = NormalizeLang(summary.Lang),
        };
        foreach (var item in _store.GetItems(summary.Game, summary.Uid))
        {
            archive.List.Add(new Uigf4GenshinItem
            {
                Uid = item.Uid,
                Id = item.Id,
                GachaType = item.GachaType,
                // 400 集录祈愿在 UIGF 里归并到 301（上游同款）
                UigfGachaType = item.GachaType == 400 ? 301 : item.GachaType,
                ItemId = item.ItemId,
                Count = item.Count,
                Time = item.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                Name = item.Name,
                ItemType = item.ItemType,
                RankType = item.RankType,
            });
        }
        return archive;
    }


    private Uigf4Archive<Uigf4StarRailItem> BuildStarRailArchive(GachaArchiveSummary summary)
    {
        var archive = new Uigf4Archive<Uigf4StarRailItem>
        {
            Uid = summary.Uid,
            Timezone = GuessTimezone(summary.Uid),
            Lang = NormalizeLang(summary.Lang),
        };
        foreach (var item in _store.GetItems(summary.Game, summary.Uid))
        {
            archive.List.Add(new Uigf4StarRailItem
            {
                Uid = item.Uid,
                Id = item.Id,
                GachaType = item.GachaType,
                ItemId = item.ItemId,
                GachaId = item.GachaId,
                Count = item.Count,
                Time = item.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                Name = item.Name,
                ItemType = item.ItemType,
                RankType = item.RankType,
            });
        }
        return archive;
    }


    private Uigf4Archive<Uigf4GachaItem> BuildArchive(GachaArchiveSummary summary)
    {
        var archive = new Uigf4Archive<Uigf4GachaItem>
        {
            Uid = summary.Uid,
            Timezone = GuessTimezone(summary.Uid),
            Lang = NormalizeLang(summary.Lang),
        };
        foreach (var item in _store.GetItems(summary.Game, summary.Uid))
        {
            archive.List.Add(new Uigf4GachaItem
            {
                Uid = item.Uid,
                Id = item.Id,
                GachaType = item.GachaType,
                ItemId = item.ItemId,
                Count = item.Count,
                Time = item.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                Name = item.Name,
                ItemType = item.ItemType,
                RankType = item.RankType,
            });
        }
        return archive;
    }


    /// <summary>
    /// 读入文件并拆成「每个存档一行」。空 list 的存档不产生行（上游同款）；
    /// 无法解析时抛 <see cref="Uigf4ImportException"/>，由界面显示错误。
    /// </summary>
    public IReadOnlyList<Uigf4ImportArchive> ParseFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return Parse(stream, path);
        }
        catch (IOException ex)
        {
            throw new Uigf4ImportException(Uigf4ImportErrorKind.InvalidFile, path, ex.Message, ex);
        }
    }


    /// <summary>解析一段 UIGF v4.2 JSON（自检与剪贴板场景用，等价于 <see cref="ParseFile"/>）。</summary>
    public IReadOnlyList<Uigf4ImportArchive> ParseJson(string json, string displayName = "")
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return Parse(stream, displayName);
    }


    private IReadOnlyList<Uigf4ImportArchive> Parse(Stream stream, string path)
    {
        Uigf4File? file;
        try
        {
            file = JsonSerializer.Deserialize<Uigf4File>(stream, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new Uigf4ImportException(Uigf4ImportErrorKind.InvalidFile, path, ex.Message, ex);
        }

        if (file is null)
        {
            throw new Uigf4ImportException(Uigf4ImportErrorKind.InvalidFile, path, "parsed result is null");
        }

        var result = new List<Uigf4ImportArchive>();
        foreach (var archive in file.GenshinArchives ?? [])
        {
            AddImportArchive(result, GachaLogStore.Genshin, archive, archive.List.Select(ToDomain));
        }
        foreach (var archive in file.StarRailArchives ?? [])
        {
            AddImportArchive(result, GachaLogStore.StarRail, archive, archive.List.Select(ToDomain));
        }
        foreach (var archive in file.ZzzArchives ?? [])
        {
            AddImportArchive(result, GachaLogStore.Zzz, archive, archive.List.Select(ToDomain));
        }
        foreach (var archive in file.GenshinBeyondArchives ?? [])
        {
            if (archive.List.Count == 0)
            {
                continue;
            }
            var items = archive.List.Select(x => new GachaArchiveItem
            {
                Id = x.Id,
                Uid = x.Uid,
                GachaType = x.OpGachaType,
                ItemId = x.ItemId,
                Count = 1,
                Time = ParseTime(x.Time),
                Name = x.Name,
                ItemType = x.ItemType,
                RankType = x.RankType,
                Region = x.Region,
                ScheduleId = x.ScheduleId,
                IsUp = x.IsUp,
            }).ToList();
            result.Add(new Uigf4ImportArchive
            {
                Game = BeyondGame,
                Uid = archive.Uid,
                // 上游导入千星奇域时不给默认时区（GachaUidArchiveDisplay.Timezone 默认 0），由用户按需改。
                Timezone = 0,
                Lang = NormalizeLang(archive.Lang),
                Items = items,
            });
        }
        return result;
    }


    private static void AddImportArchive<T>(
        List<Uigf4ImportArchive> result,
        string game,
        Uigf4Archive<T> archive,
        IEnumerable<GachaArchiveItem> items)
    {
        var list = items.ToList();
        if (list.Count == 0)
        {
            return;
        }
        result.Add(new Uigf4ImportArchive
        {
            Game = game,
            Uid = archive.Uid,
            Lang = NormalizeLang(archive.Lang),
            Items = list,
        });
    }


    private static GachaArchiveItem ToDomain(Uigf4GachaItem item) => new()
    {
        Id = item.Id,
        Uid = item.Uid,
        GachaType = item.GachaType,
        ItemId = item.ItemId,
        Count = item.Count,
        Time = ParseTime(item.Time),
        Name = item.Name,
        ItemType = item.ItemType,
        RankType = item.RankType,
    };


    private static GachaArchiveItem ToDomain(Uigf4StarRailItem item)
    {
        GachaArchiveItem archiveItem = ToDomain((Uigf4GachaItem)item);
        archiveItem.GachaId = item.GachaId;
        return archiveItem;
    }


    /// <summary>
    /// 把选中的存档写入本地存档（上游 <c>ImportAsync</c>：校验字段、按 UID 一致性、
    /// 时间不早于 2020-09-01，最后 <c>INSERT OR REPLACE</c>）。
    /// </summary>
    public Uigf4ImportResult Import(Uigf4ImportArchive archive)
    {
        if (archive.Unsupported)
        {
            throw new Uigf4ImportException(
                Uigf4ImportErrorKind.UnsupportedSection, uid: archive.Uid, field: "hk4e_ugc");
        }

        if (archive.Game == BeyondGame)
        {
            return ImportBeyond(archive);
        }

        var records = new List<GachaRecord>(archive.Items.Count);
        bool missingName = false;
        foreach (var item in archive.Items)
        {
            Validate(item, archive.Game, archive.Uid, out bool noName);
            missingName |= noName;
            records.Add(new GachaRecord
            {
                Id = item.Id,
                Uid = item.Uid == 0 ? archive.Uid : item.Uid,
                GachaType = item.GachaType,
                GachaTypeName = GachaService.GetPoolName(archive.Game, item.GachaType),
                ItemId = item.ItemId,
                GachaId = item.GachaId,
                Count = item.Count == 0 ? 1 : item.Count,
                Time = item.Time.AddHours(archive.Timezone),
                Name = item.Name,
                ItemType = item.ItemType,
                RankType = item.RankType,
            });
        }

        _store.Merge(archive.Game, records, archive.Lang);
        return new Uigf4ImportResult(!missingName);
    }


    /// <summary>
    /// 千星奇域导入（上游 <c>UIGFGachaService.ImportForGenshinBeyond</c>：校验后整条写
    /// <c>GenshinBeyondGachaItem</c>）。
    /// </summary>
    private Uigf4ImportResult ImportBeyond(Uigf4ImportArchive archive)
    {
        if (_beyond is null)
        {
            throw new Uigf4ImportException(
                Uigf4ImportErrorKind.UnsupportedSection, uid: archive.Uid, field: "hk4e_ugc");
        }

        var items = new List<GenshinBeyondGachaItemEx>(archive.Items.Count);
        foreach (GachaArchiveItem item in archive.Items)
        {
            if (item.Id == 0)
            {
                throw new Uigf4ImportException(Uigf4ImportErrorKind.MissingField, uid: archive.Uid, field: "id");
            }
            if (item.ItemId == 0)
            {
                throw new Uigf4ImportException(Uigf4ImportErrorKind.MissingField, uid: archive.Uid, field: "item_id");
            }
            if (item.GachaType == 0)
            {
                throw new Uigf4ImportException(Uigf4ImportErrorKind.MissingField, uid: archive.Uid, field: "op_gacha_type");
            }
            if (item.Time < new DateTime(2025, 10, 1))
            {
                throw new Uigf4ImportException(Uigf4ImportErrorKind.MissingField, uid: archive.Uid, field: "time");
            }
            if (item.RankType == 0)
            {
                throw new Uigf4ImportException(Uigf4ImportErrorKind.MissingField, uid: archive.Uid, field: "rank_type");
            }
            if (item.Uid != 0 && item.Uid != archive.Uid)
            {
                throw new Uigf4ImportException(
                    Uigf4ImportErrorKind.UidMismatch, uid: archive.Uid, expectedUid: archive.Uid,
                    foundUid: item.Uid, field: "uid");
            }
            items.Add(new GenshinBeyondGachaItemEx
            {
                Id = item.Id,
                Uid = item.Uid == 0 ? archive.Uid : item.Uid,
                Region = item.Region,
                OpGachaType = item.GachaType,
                ScheduleId = item.ScheduleId,
                ItemType = item.ItemType,
                ItemId = item.ItemId,
                ItemName = item.Name,
                RankType = item.RankType,
                IsUp = item.IsUp,
                Time = item.Time.AddHours(archive.Timezone),
            });
        }
        _beyond.InsertItems(items);
        return new Uigf4ImportResult(true);
    }


    private static void Validate(GachaArchiveItem item, string game, long archiveUid, out bool missingName)
    {
        if (item.Id == 0)
        {
            throw new Uigf4ImportException(Uigf4ImportErrorKind.MissingField, uid: archiveUid, field: "id");
        }
        if (item.ItemId == 0)
        {
            throw new Uigf4ImportException(Uigf4ImportErrorKind.MissingField, uid: archiveUid, field: "item_id");
        }
        if (item.GachaType == 0)
        {
            throw new Uigf4ImportException(Uigf4ImportErrorKind.MissingField, uid: archiveUid, field: "gacha_type");
        }
        if (game == GachaLogStore.StarRail && item.GachaId == 0)
        {
            // 上游 UIGFGachaService.cs:575：星铁记录必须有 gacha_id
            throw new Uigf4ImportException(Uigf4ImportErrorKind.MissingField, uid: archiveUid, field: "gacha_id");
        }
        if (item.Time < new DateTime(2020, 9, 1))
        {
            throw new Uigf4ImportException(Uigf4ImportErrorKind.MissingField, uid: archiveUid, field: "time");
        }
        if (item.RankType == 0)
        {
            throw new Uigf4ImportException(Uigf4ImportErrorKind.MissingField, uid: archiveUid, field: "rank_type");
        }
        if (item.Uid != 0 && item.Uid != archiveUid)
        {
            throw new Uigf4ImportException(
                Uigf4ImportErrorKind.UidMismatch, uid: archiveUid, expectedUid: archiveUid, foundUid: item.Uid);
        }
        missingName = string.IsNullOrWhiteSpace(item.Name);
    }


    /// <summary>UID 首位推断服务器时区（上游同款：6 → -5、7 → 1、其余 8）。</summary>
    public static int GuessTimezone(long uid)
    {
        string text = uid.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (text.Length == 0)
        {
            return 8;
        }
        return text[0] switch
        {
            '6' => -5,
            '7' => 1,
            _ => 8,
        };
    }


    private static DateTime ParseTime(string? text)
        => DateTime.TryParse(text, out DateTime time) ? time : default;


    private static string? NormalizeLang(string? lang)
        => string.IsNullOrWhiteSpace(lang) ? null : lang.Trim().ToLowerInvariant();


    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };
}


/// <summary>解析出来的一个「游戏 + UID」存档（导入列表的一行）。</summary>
public sealed class Uigf4ImportArchive
{
    public required string Game { get; init; }

    public long Uid { get; init; }

    /// <summary>用户可改的时区偏移（小时）；上游同一个 NumberBox，默认 0。</summary>
    public int Timezone { get; set; }

    public string? Lang { get; init; }

    public IReadOnlyList<GachaArchiveItem> Items { get; init; } = [];

    /// <summary>千星奇域等暂不支持写入的区段（选中导入时显式报错）。</summary>
    public bool Unsupported { get; init; }


    public int Count => Items.Count;

    public DateTime LastItemTime => Items.Count == 0 ? default : Items.Max(x => x.Time);

    public string LastItemName => Items.Count == 0 ? "" : Items.MaxBy(x => x.Id)?.Name ?? "";
}


/// <summary>导入结果：<see cref="Success"/> 为 false 表示有记录缺少物品名（上游同款提示）。</summary>
public sealed record Uigf4ImportResult(bool Success);


/// <summary>UIGF 导入失败的类别（界面层负责拼本地化文案）。</summary>
public enum Uigf4ImportErrorKind
{
    /// <summary>文件不是合法的 UIGF v4 JSON。</summary>
    InvalidFile,

    /// <summary>某条记录缺少必需字段（<see cref="Uigf4ImportException.Field"/> 是字段名）。</summary>
    MissingField,

    /// <summary>记录里的 uid 与所在存档不一致。</summary>
    UidMismatch,

    /// <summary>区段暂不支持写入。</summary>
    UnsupportedSection,
}
