using System.Text.Json;
using LunarWard.Launch;
using LunarWard.Launch.Database;
using LunarWard.Launch.Uigf4;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// UIGF v4.2 导出 / 导入（Starward <c>UIGFGachaService</c> 与 <c>UIGF4GachaWindow</c>）。
/// </summary>
public sealed class Uigf4Tests : IDisposable
{
    private readonly string _dir = Path.Join(Path.GetTempPath(), "lunarward-uigf4-" + Guid.NewGuid().ToString("N"));

    private readonly string _file;

    private readonly GachaLogStore _store;

    private readonly Uigf4Service _service;


    public Uigf4Tests()
    {
        Directory.CreateDirectory(_dir);
        _file = Path.Join(_dir, "LunarWardDatabase.db");
        _store = new GachaLogStore(new LunarWardDatabase(_file));
        _service = new Uigf4Service(_store, "9.9.9");
    }


    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (IOException)
        {
        }
    }


    private static GachaRecord Record(
        long id,
        int gachaType,
        int itemId = 10000002,
        long uid = 100000001,
        DateTime? time = null)
        => new()
        {
            Id = id,
            Name = "测试物品",
            ItemType = "角色",
            RankType = 5,
            GachaType = gachaType,
            GachaTypeName = "卡池",
            Time = time ?? new DateTime(2026, 1, 2, 3, 4, 5),
            Uid = uid,
            ItemId = itemId,
            IsUp = true,
        };


    // ── 时区推断（上游 GuessTimezone） ───────────────────────────────

    [Theory]
    [InlineData(600000001, -5)]
    [InlineData(700000001, 1)]
    [InlineData(100000001, 8)]
    [InlineData(200000001, 8)]
    public void GuessTimezoneFollowsFirstUidDigit(long uid, int expected)
        => Assert.Equal(expected, Uigf4Service.GuessTimezone(uid));


    // ── 导出信封 ──────────────────────────────────────────────────────

    [Fact]
    public void ExportWritesInfoAndOnlySelectedSections()
    {
        _store.Merge(GachaLogStore.Genshin, [Record(1, 301)], "zh-cn");
        _store.Merge(GachaLogStore.Zzz, [Record(2, 2001)], "zh-cn");

        var json = _service.BuildJson([_store.GetSummaries()[0]]);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var info = root.GetProperty("info");
        Assert.Equal("v4.2", info.GetProperty("version").GetString());
        Assert.Equal("LunarWard", info.GetProperty("export_app").GetString());
        Assert.Equal("9.9.9", info.GetProperty("export_app_version").GetString());

        Assert.True(root.TryGetProperty("hk4e", out var genshin));
        Assert.Equal(1, genshin.GetArrayLength());
        // 没选中的区段整段为空数组（上游同样会写出其余空数组）
        Assert.Equal(0, root.GetProperty("hkrpg").GetArrayLength());
        Assert.Equal(0, root.GetProperty("nap").GetArrayLength());
    }


    [Fact]
    public void ExportMapsGenshin400To301AndCarriesTimezoneAndLang()
    {
        _store.Merge(GachaLogStore.Genshin, [Record(1, 400)], "zh-cn");

        using var doc = JsonDocument.Parse(_service.BuildJson(_store.GetSummaries()));
        var archive = doc.RootElement.GetProperty("hk4e")[0];

        // uid 按 UIGF 规范写成字符串（上游 [JsonNumberHandling(WriteAsString)]）
        Assert.Equal("100000001", archive.GetProperty("uid").GetString());
        Assert.Equal(8, archive.GetProperty("timezone").GetInt32());
        Assert.Equal("zh-cn", archive.GetProperty("lang").GetString());

        var item = archive.GetProperty("list")[0];
        // 数值字段全部写成字符串（上游对 uid/id/gacha_type 等统一 WriteAsString）
        Assert.Equal("400", item.GetProperty("gacha_type").GetString());
        Assert.Equal("301", item.GetProperty("uigf_gacha_type").GetString());
        Assert.Equal("2026-01-02 03:04:05", item.GetProperty("time").GetString());
    }


    [Fact]
    public void ExportFileNameCarriesTimestamp()
    {
        var name = Uigf4Service.FileName(new DateTime(2026, 1, 2, 3, 4, 5));
        Assert.Equal("LunarWard_UIGF_20260102_030405.json", name);
    }


    // ── 导入 ──────────────────────────────────────────────────────────

    [Fact]
    public void ImportFileParsesEachArchiveAndCountsRecords()
    {
        string path = Path.Join(_dir, "import.json");
        File.WriteAllText(path, """
        {
          "info": { "version": "v4.2" },
          "hk4e": [
            { "uid": 100000001, "timezone": 8, "lang": "zh-cn", "list": [
              { "uid": "100000001", "id": "1", "gacha_type": "301", "item_id": "10000002",
                "count": "1", "time": "2026-01-02 03:04:05", "name": "测试", "item_type": "角色", "rank_type": "5" }
            ] }
          ],
          "hkrpg": [],
          "nap": []
        }
        """);

        var archives = _service.ParseFile(path);
        var archive = Assert.Single(archives);
        Assert.Equal(GachaLogStore.Genshin, archive.Game);
        Assert.Equal(100000001, archive.Uid);
        Assert.Equal(1, archive.Count);
        Assert.Equal("测试", archive.LastItemName);
        Assert.False(archive.Unsupported);
    }


    [Fact]
    public void ImportAppliesTimezoneOffsetAndMergesById()
    {
        var archive = new Uigf4ImportArchive
        {
            Game = GachaLogStore.Genshin,
            Uid = 700000001,
            Timezone = 1,
            Items =
            [
                new GachaArchiveItem
                {
                    Id = 10,
                    Uid = 700000001,
                    GachaType = 301,
                    ItemId = 10000002,
                    Count = 1,
                    Time = new DateTime(2026, 1, 2, 3, 4, 5),
                    Name = "测试物品",
                    ItemType = "角色",
                    RankType = 5,
                },
            ],
        };

        var result = _service.Import(archive);

        Assert.True(result.Success);
        var items = _store.GetItems(GachaLogStore.Genshin, 700000001);
        var item = Assert.Single(items);
        Assert.Equal(new DateTime(2026, 1, 2, 4, 4, 5), item.Time);
        var summary = Assert.Single(_store.GetSummaries());
        Assert.Equal("测试物品", summary.LastItemName);
    }


    [Fact]
    public void ImportRejectsMissingRequiredFields()
    {
        var archive = new Uigf4ImportArchive
        {
            Game = GachaLogStore.Genshin,
            Uid = 100000001,
            Items =
            [
                new GachaArchiveItem
                {
                    Id = 0,
                    GachaType = 301,
                    ItemId = 10000002,
                    Time = new DateTime(2026, 1, 2),
                    RankType = 5,
                },
            ],
        };

        var ex = Assert.Throws<Uigf4ImportException>(() => _service.Import(archive));
        Assert.Equal(Uigf4ImportErrorKind.MissingField, ex.Kind);
        Assert.Equal("id", ex.Field);
    }


    [Fact]
    public void ImportRejectsUidMismatch()
    {
        var archive = new Uigf4ImportArchive
        {
            Game = GachaLogStore.StarRail,
            Uid = 100000001,
            Items =
            [
                new GachaArchiveItem
                {
                    Id = 1,
                    Uid = 200000002,
                    GachaType = 1,
                    ItemId = 1001,
                    Time = new DateTime(2026, 1, 2),
                    RankType = 5,
                },
            ],
        };

        var ex = Assert.Throws<Uigf4ImportException>(() => _service.Import(archive));
        Assert.Equal(Uigf4ImportErrorKind.UidMismatch, ex.Kind);
        Assert.Equal(200000002, ex.FoundUid);
    }


    [Fact]
    public void UgcSectionParsesButImportIsRefused()
    {
        string path = Path.Join(_dir, "ugc.json");
        File.WriteAllText(path, """
        {
          "info": { "version": "v4.2" },
          "hk4e_ugc": [
            { "uid": 100000001, "timezone": 8, "list": [
              { "uid": 100000001, "id": 1, "op_gacha_type": 1000, "item_id": 1, "count": 1,
                "time": "2026-01-02 03:04:05", "name": "奇域", "item_type": "角色", "rank_type": 4 }
            ] }
          ]
        }
        """);

        var archive = Assert.Single(_service.ParseFile(path));
        Assert.True(archive.Unsupported);

        var ex = Assert.Throws<Uigf4ImportException>(() => _service.Import(archive));
        Assert.Equal(Uigf4ImportErrorKind.UnsupportedSection, ex.Kind);
    }


    [Fact]
    public void BrokenJsonRaisesInvalidFile()
    {
        string path = Path.Join(_dir, "broken.json");
        File.WriteAllText(path, "{ not json");

        var ex = Assert.Throws<Uigf4ImportException>(() => _service.ParseFile(path));
        Assert.Equal(Uigf4ImportErrorKind.InvalidFile, ex.Kind);
        Assert.Equal(path, ex.Path);
    }


    [Fact]
    public void ExportThenImportRoundTripsThroughDisk()
    {
        _store.Merge(GachaLogStore.Genshin, [Record(1, 301), Record(2, 301)], "zh-cn");
        string json = _service.BuildJson(_store.GetSummaries());
        string path = Path.Join(_dir, "roundtrip.json");
        File.WriteAllText(path, json);

        var archive = Assert.Single(_service.ParseFile(path));
        Assert.True(_service.Import(archive).Success);

        // 同一 Id 覆盖，不会变 4 条
        Assert.Equal(2, _store.GetItems(GachaLogStore.Genshin, 100000001).Count);
    }
}
