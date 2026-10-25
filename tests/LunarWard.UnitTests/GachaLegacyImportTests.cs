using LunarWard.Launch;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 旧格式导入（Starward <c>GachaLogService.ImportGachaLogAsync</c>：UIGF v3.0 / JSON 信封）。
/// </summary>
public class GachaLegacyImportTests
{
    private static GachaRecord Record(long id, int gachaType = 301, int rank = 5, long uid = 100000001)
        => new()
        {
            Id = id,
            Name = "琴",
            ItemType = "角色",
            RankType = rank,
            GachaType = gachaType,
            GachaTypeName = "角色活动祈愿",
            Time = new DateTime(2026, 1, 2, 3, 4, (int)(id % 60)),
            Uid = uid,
            IsUp = true,
            ItemId = 10000002,
        };


    [Fact]
    public void ExportedGenshinFileImportsBack()
    {
        string json = GachaExport.BuildJson([Record(1), Record(2, rank: 4)], "genshin", 100000001, "zh-cn", "9.9.9");

        var records = GachaLegacyImport.Parse(json, "genshin");

        Assert.Equal(2, records.Count);
        Assert.Equal(1, records[0].Id);
        Assert.Equal("琴", records[0].Name);
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 1), records[0].Time);
        Assert.Equal("角色活动祈愿", records[0].GachaTypeName);
        Assert.True(records[0].IsUp);
        Assert.Equal(4, records[1].RankType);
    }


    [Fact]
    public void ExportedZzzFileImportsBackWithZzzPoolNames()
    {
        string json = GachaExport.BuildJson([Record(5, gachaType: 2)], "zzz", 100000001, "zh-cn", "9.9.9");

        var records = GachaLegacyImport.Parse(json, "zzz");

        var record = Assert.Single(records);
        Assert.Equal(2, record.GachaType);
        Assert.Equal("独家频段", record.GachaTypeName);
    }


    [Fact]
    public void NumericStringsAreAccepted()
    {
        const string json = """
        {
          "list": [
            { "uid": "100000001", "id": "7", "gacha_type": "301", "item_id": "10000002",
              "count": "1", "rank_type": "5", "time": "2026-01-02 03:04:05",
              "name": "琴", "item_type": "角色", "is_up": "1" }
          ]
        }
        """;

        var record = Assert.Single(GachaLegacyImport.Parse(json, "genshin"));
        Assert.Equal(7, record.Id);
        Assert.True(record.IsUp);
    }


    [Fact]
    public void BareArrayIsAccepted()
    {
        const string json = """
        [ { "uid": 100000001, "id": 8, "gacha_type": 301, "item_id": 10000002, "count": 1,
            "rank_type": 5, "time": "2026-01-02 03:04:05", "name": "琴", "item_type": "角色" } ]
        """;

        Assert.Single(GachaLegacyImport.Parse(json, "genshin"));
    }


    [Fact]
    public void MissingListRaisesReadableError()
    {
        var ex = Assert.Throws<GachaLegacyImportException>(() => GachaLegacyImport.Parse("""{ "info": {} }""", "genshin"));
        Assert.Contains("list", ex.Message);
    }


    [Fact]
    public void BrokenJsonRaisesReadableError()
    {
        Assert.Throws<GachaLegacyImportException>(() => GachaLegacyImport.Parse("{ nope", "genshin"));
    }


    [Fact]
    public void BadRankTypeRaisesReadableError()
    {
        const string json = """
        { "list": [ { "uid": 1, "id": 2, "gacha_type": 301, "item_id": 3, "count": 1,
                      "rank_type": 9, "time": "2026-01-02 03:04:05", "name": "x", "item_type": "角色" } ] }
        """;

        var ex = Assert.Throws<GachaLegacyImportException>(() => GachaLegacyImport.Parse(json, "genshin"));
        Assert.Contains("rank_type", ex.Message);
    }


    [Fact]
    public void MergeByIdOverwritesSameIdAndKeepsNewestFirst()
    {
        var existing = new[] { Record(1), Record(2) };
        var incoming = new[] { Record(2, rank: 4), Record(3) };

        var merged = GachaService.MergeById(existing, incoming);

        Assert.Equal(3, merged.Count);
        Assert.Equal(4, merged.Single(r => r.Id == 2).RankType);
        Assert.Equal(3, merged[0].Id);
    }
}
