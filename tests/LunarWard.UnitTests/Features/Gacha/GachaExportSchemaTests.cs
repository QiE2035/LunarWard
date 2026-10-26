using System.Text.Encodings.Web;
using System.Text.Json;
using LunarWard.Core.Gacha.Genshin;
using LunarWard.Core.Gacha.StarRail;
using LunarWard.Features.Gacha.UIGF;
using Xunit;

namespace LunarWard.UnitTests.Features.Gacha;

/// <summary>
/// 导出格式回归：抽卡记录导出必须逐字符合 UIGF v3.0 / SRGF v1.0 / UIGF v4.2 的字段名与取值形态，
/// 否则别的工具（以及 Starward 自己）无法回读。这里只断言序列化结构，不碰数据库。
/// 与上游一致的几种「反直觉」写法一并锁住：
/// uid / gacha_type / item_id 写成<b>字符串</b>、time 是 <c>yyyy-MM-dd HH:mm:ss</c>、
/// export_app 默认 "Starward"、UIGF 为 v3.0、SRGF 为 v1.0。
/// </summary>
public class GachaExportSchemaTests
{
    // 与 AppConfig.JsonSerializerOptions 同构（WriteIndented + 不转义中文）
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };


    private static UIGFGenshinGachaItem CreateGenshinItem() => new()
    {
        Uid = 123456789,
        Id = 1700000000000000001,
        GachaType = 301,
        UIGFGachaType = 301,
        Name = "胡桃",
        ItemType = "角色",
        RankType = 5,
        Time = new DateTime(2024, 5, 1, 12, 34, 56, DateTimeKind.Unspecified),
        ItemId = 10000046,
        Count = 1,
    };


    [Fact]
    public void Uigf3_Info_FieldOrderAndValues_MatchUigfV3()
    {
        var file = new UIGF3File<UIGFGenshinGachaItem>
        {
            Info = new UIAF3FileInfo
            {
                Uid = 123456789,
                Lang = "zh-cn",
                ExportTimestamp = 1714560000,
                ExportTime = "2024-05-01 12:00:00",
                ExportAppVersion = "1.0.0",
                RegionTimeZone = 8,
            },
            List = [CreateGenshinItem()],
        };

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(file, Options));
        var root = doc.RootElement;
        var info = root.GetProperty("info");

        Assert.Equal("123456789", info.GetProperty("uid").GetString());              // WriteAsString
        Assert.Equal("zh-cn", info.GetProperty("lang").GetString());
        Assert.Equal(1714560000, info.GetProperty("export_timestamp").GetInt64()); // 只允许字符串读入，写出仍是数字
        Assert.Equal("2024-05-01 12:00:00", info.GetProperty("export_time").GetString());
        Assert.Equal("Starward", info.GetProperty("export_app").GetString());          // 默认值
        Assert.Equal("1.0.0", info.GetProperty("export_app_version").GetString());
        Assert.Equal("v3.0", info.GetProperty("uigf_version").GetString());
        Assert.Equal("v1.0", info.GetProperty("srgf_version").GetString());
        Assert.Equal(8, info.GetProperty("region_time_zone").GetInt32());
    }


    [Fact]
    public void Uigf3_Info_KeepsDeclaredFieldOrder()
    {
        var file = new UIGF3File<UIGFGenshinGachaItem>
        {
            Info = new UIAF3FileInfo
            {
                Uid = 1,
                Lang = "zh-cn",
                ExportTimestamp = 1,
                ExportTime = "t",
                ExportAppVersion = "v",
                RegionTimeZone = 8,
            },
            List = [],
        };

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(file, Options));
        var names = doc.RootElement.GetProperty("info").EnumerateObject().Select(x => x.Name).ToArray();
        Assert.Equal(
            new[] { "uid", "lang", "export_timestamp", "export_time", "export_app", "export_app_version", "uigf_version", "srgf_version", "region_time_zone" },
            names);
    }


    [Fact]
    public void Uigf3_ListItem_HasExpectedFieldNamesAndTypes()
    {
        var file = new UIGF3File<UIGFGenshinGachaItem>
        {
            Info = new UIAF3FileInfo { Uid = 1, Lang = "zh-cn", ExportTimestamp = 0, ExportTime = "", ExportAppVersion = "1.0.0" },
            List = [CreateGenshinItem()],
        };

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(file, Options));
        var item = doc.RootElement.GetProperty("list")[0];

        Assert.Equal("301", item.GetProperty("uigf_gacha_type").GetString());
        Assert.Equal("301", item.GetProperty("gacha_type").GetString());
        Assert.Equal("10000046", item.GetProperty("item_id").GetString());      // GachaItemIdJsonConverter 写成字符串
        Assert.Equal("2024-05-01 12:34:56", item.GetProperty("time").GetString()); // DateTimeStringJsonConverter
        Assert.Equal("胡桃", item.GetProperty("name").GetString());
        Assert.Equal("角色", item.GetProperty("item_type").GetString());
        Assert.Equal("5", item.GetProperty("rank_type").GetString());
        Assert.Equal("1700000000000000001", item.GetProperty("id").GetString());
    }


    [Fact]
    public void StarRailExport_UsesSrgfShape_ButSameInfoBlock()
    {
        var item = new StarRailGachaItem
        {
            Uid = 100000000,
            Id = 1700000000000000002,
            GachaType = 11,
            Name = "希儿",
            ItemType = "角色",
            RankType = 5,
            Time = new DateTime(2024, 5, 2, 1, 2, 3, DateTimeKind.Unspecified),
            ItemId = 1102,
            Count = 1,
        };
        var file = new UIGF3File<StarRailGachaItem>
        {
            Info = new UIAF3FileInfo { Uid = 100000000, Lang = "zh-cn", ExportTimestamp = 0, ExportTime = "", ExportAppVersion = "1.0.0", RegionTimeZone = 8 },
            List = [item],
        };

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(file, Options));
        Assert.Equal("v1.0", doc.RootElement.GetProperty("info").GetProperty("srgf_version").GetString());
        Assert.Equal("11", doc.RootElement.GetProperty("list")[0].GetProperty("gacha_type").GetString());
    }


    [Fact]
    public void Uigf4_File_HasArchiveArraysPerGame()
    {
        var file = new UIGF4File();
        file.hk4eGachaArchives!.Add(new UIGF4GachaArchive<UIGFGenshinGachaItem>
        {
            Uid = 123456789,
            Timezone = 8,
            Lang = "zh-cn",
            List = [CreateGenshinItem()],
        });

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(file, Options));
        var root = doc.RootElement;
        var info = root.GetProperty("info");

        Assert.Equal("v4.2", info.GetProperty("version").GetString());
        Assert.Equal("Starward", info.GetProperty("export_app").GetString());
        Assert.Equal("123456789", root.GetProperty("hk4e")[0].GetProperty("uid").GetString());
        Assert.Equal(8, root.GetProperty("hk4e")[0].GetProperty("timezone").GetInt32());
        Assert.Equal("zh-cn", root.GetProperty("hk4e")[0].GetProperty("lang").GetString());
        Assert.True(root.TryGetProperty("hkrpg", out _));
        Assert.True(root.TryGetProperty("nap", out _));
        Assert.True(root.TryGetProperty("hk4e_ugc", out _));
    }


    [Fact]
    public void Uigf4_Archive_OmitsNullLang()
    {
        var file = new UIGF4File();
        file.hk4eGachaArchives!.Add(new UIGF4GachaArchive<UIGFGenshinGachaItem> { Uid = 1, Timezone = 8, Lang = null, List = [] });

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(file, Options));
        Assert.False(doc.RootElement.GetProperty("hk4e")[0].TryGetProperty("lang", out _));
    }
}
