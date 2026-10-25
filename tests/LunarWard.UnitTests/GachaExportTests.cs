using System.Text.Json;
using LunarWard.Launch;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 旧格式导出（Starward <c>ExportAsJsonAsync</c>：原神 UIGF v3.0、星铁 SRGF v1.0、绝区零 JSON）。
/// </summary>
public class GachaExportTests
{
    private static GachaRecord Record(long id, int gachaType, int itemId, DateTime time, long uid = 100000001)
        => new()
        {
            Id = id,
            Name = "测试",
            ItemType = "角色",
            RankType = 5,
            GachaType = gachaType,
            GachaTypeName = "卡池",
            Time = time,
            Uid = uid,
            ItemId = itemId,
            IsUp = true,
        };


    private static JsonDocument Export(string gameKind, params GachaRecord[] records)
        => JsonDocument.Parse(GachaExport.BuildJson(records, gameKind, 100000001, "zh-cn", "9.9.9"));


    [Fact]
    public void InfoCarriesVersionsAndRegionTimeZone()
    {
        var t = new DateTime(2026, 1, 2, 3, 4, 5);
        using var doc = Export("genshin", Record(1, 301, 10000002, t));

        var info = doc.RootElement.GetProperty("info");
        Assert.Equal(100000001, info.GetProperty("uid").GetInt64());
        Assert.Equal("zh-cn", info.GetProperty("lang").GetString());
        Assert.Equal("v3.0", info.GetProperty("uigf_version").GetString());
        Assert.Equal("v1.0", info.GetProperty("srgf_version").GetString());
        Assert.Equal(8, info.GetProperty("region_time_zone").GetInt32());
        Assert.Equal("LunarWard", info.GetProperty("export_app").GetString());
        Assert.Equal("9.9.9", info.GetProperty("export_app_version").GetString());
    }


    [Fact]
    public void GenshinMapsGachaType400To301()
    {
        var t = new DateTime(2026, 1, 2);
        using var doc = Export("genshin",
            Record(1, 400, 1, t),
            Record(2, 200, 2, t.AddMinutes(1)));

        var list = doc.RootElement.GetProperty("list");
        Assert.Equal(301, list[0].GetProperty("uigf_gacha_type").GetInt32());
        Assert.Equal(200, list[1].GetProperty("uigf_gacha_type").GetInt32());
        Assert.Equal(400, list[0].GetProperty("gacha_type").GetInt32());
    }


    [Fact]
    public void StarRailKeepsGachaTypeAndOmitsUigfGachaType()
    {
        var t = new DateTime(2026, 1, 2);
        using var doc = Export("starrail", Record(1, 11, 3, t));

        var item = doc.RootElement.GetProperty("list")[0];
        Assert.Equal(11, item.GetProperty("gacha_type").GetInt32());
        Assert.False(item.TryGetProperty("uigf_gacha_type", out _));
        Assert.Equal("2026-01-02 00:00:00", item.GetProperty("time").GetString());
        Assert.Equal("1", item.GetProperty("is_up").GetString());
    }


    [Fact]
    public void OnlySelectedUidOrderedById()
    {
        var t = new DateTime(2026, 1, 2);
        using var doc = Export("zzz",
            Record(2, 2, 1, t.AddHours(1)),
            Record(1, 2, 1, t),
            Record(3, 2, 1, t.AddHours(2), uid: 200000002));

        var list = doc.RootElement.GetProperty("list");
        Assert.Equal(2, list.GetArrayLength());
        Assert.Equal(1, list[0].GetProperty("id").GetInt64());
        Assert.Equal(2, list[1].GetProperty("id").GetInt64());
    }


    [Theory]
    [InlineData("genshin", "UIGF v3.0")]
    [InlineData("starrail", "SRGF v1.0")]
    [InlineData("zzz", "JSON")]
    public void LegacyFormatNameFollowsGame(string gameKind, string expected)
        => Assert.Equal(expected, GachaExport.LegacyFormatName(gameKind));


    [Fact]
    public void FileNameUsesStandardNameAndUid()
    {
        var name = GachaExport.FileName("starrail", 100000001, new DateTime(2026, 5, 6, 7, 8, 9));

        Assert.Equal("SRGF-v1.0-100000001-20260506-070809.json", name);
    }

}
