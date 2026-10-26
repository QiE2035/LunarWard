using Avalonia.Data.Converters;
using LunarWard.Core;
using LunarWard.Core.Gacha;
using LunarWard.Core.Gacha.Genshin;
using LunarWard.Core.Gacha.StarRail;
using LunarWard.Core.Gacha.ZZZ;
using LunarWard.Features.Gacha;
using LunarWard.Features.Gacha.UIGF;
using System.Reflection;
using Xunit;

namespace LunarWard.UnitTests.Features.Gacha;

/// <summary>
/// 对齐审计（<c>docs/13-GACHA-AUDIT.md</c>）里「模型 / 统计口径 / 转换器」这几类文件的主路径回归。
/// 全部走公开面（<c>internal</c> 转换器用反射取类型 + <see cref="IValueConverter"/> 调用），不依赖数据库与 UI 线程。
/// </summary>
public class GachaAlignmentTests
{

    private static Assembly AppAssembly => typeof(GachaLogItemEx).Assembly;


    private static IValueConverter CreateConverter(string typeName)
    {
        Type type = AppAssembly.GetType(typeName, throwOnError: true)!;
        return (IValueConverter)Activator.CreateInstance(type, nonPublic: true)!;
    }


    private static GachaLogItemEx CreateItem(int gachaType, int pity = 0)
    {
        return new GachaLogItemEx
        {
            Id = 1700000000000000001,
            Uid = 100000000,
            GachaType = gachaType,
            Name = "Test",
            ItemType = "",
            RankType = 5,
            Count = 1,
            Time = new DateTime(2024, 5, 1, 12, 0, 0),
            Pity = pity,
        };
    }


    // ---------- GachaTypeStats / GachaBanner：统计卡文字口径 ----------

    [Fact]
    public void GachaTypeStats_UpText_LocksUpstreamSpellingAndFormat()
    {
        var stats = new GachaTypeStats
        {
            GachaTypeText = "角色活动祈愿",
            Count = 100,
            Count_5 = 2,
            Count_5_Up = 1,
            Average_5 = 50,
            Average_5_Up = 50,
        };

        // 上游刻意拼作 Avarage（不是 Average），不要「修正」
        Assert.Equal(" / UP", stats.Avarage_5_Desc_Text);
        Assert.Equal(" / 50.00 (1)", stats.Avarage_5_Up_Text);
    }


    [Fact]
    public void GachaTypeStats_WithoutUp_HidesUpText()
    {
        var stats = new GachaTypeStats { Count_5 = 1, Count_5_Up = 0, Average_5 = 10 };

        Assert.Equal("", stats.Avarage_5_Desc_Text);
        Assert.Equal("", stats.Avarage_5_Up_Text);
    }


    [Fact]
    public void GachaBanner_ExposesGachaTypeValueAndLocalization()
    {
        var gachaType = new GenshinGachaType(GenshinGachaType.CharacterEventWish);
        var banner = new GachaBanner(gachaType);

        Assert.Equal(301, banner.Value);
        Assert.Equal(gachaType.ToLocalization(), banner.ToLocalization());
        Assert.Equal(301, banner.GachaType.Value);
    }


    // ---------- GachaLogItemEx：保底进度分母 ----------

    [Theory]
    [InlineData(301, 90)]   // 角色活动祈愿
    [InlineData(200, 90)]   // 常驻祈愿
    [InlineData(302, 80)]   // 武器活动祈愿（原神）
    [InlineData(12, 80)]    // 光锥活动跃迁（星铁）
    [InlineData(22, 80)]    // 光锥联动跃迁（星铁）
    [InlineData(1, 90)]     // 常驻频段（ZZZ）
    [InlineData(2, 90)]     // 独家频段（ZZZ）
    [InlineData(3, 80)]     // 音擎频段（ZZZ）
    [InlineData(5, 80)]     // 邦布频段（ZZZ）
    [InlineData(103, 80)]   // 音擎回响（ZZZ）
    public void GachaLogItemEx_Progress_UsesUpstreamDenominator(int gachaType, int denominator)
    {
        var item = CreateItem(gachaType, pity: denominator / 2);

        Assert.Equal(50.0, item.Progress, 6);
    }


    [Fact]
    public void GachaLogItemEx_UpFlagAndOpacity_FollowDerivedBool()
    {
        var item = CreateItem(301);

        item.IsUp = true;
        Assert.Equal(1.0, item.UpTextOpacity, 6);

        item.IsUp = false;
        Assert.Equal(0.0, item.UpTextOpacity, 6);

        // 基类的原始 int 字段与派生 bool 是两个独立存储（见 docs/13 G-9 相关说明）
        Assert.Equal(0, ((GachaLogItem)item).IsUp);
    }


    [Fact]
    public void GachaLogItemEx_IdText_IsPlainDigits()
    {
        Assert.Equal("1700000000000000001", CreateItem(301).IdText);
    }


    [Fact]
    public void GachaLogItemEx_IsPointerIn_RaisesPropertyChanged()
    {
        var item = CreateItem(301);
        string? changed = null;
        item.PropertyChanged += (_, e) => changed = e.PropertyName;

        item.IsPointerIn = true;

        Assert.True(item.IsPointerIn);
        Assert.Equal(nameof(GachaLogItemEx.IsPointerIn), changed);
    }


    // ---------- GachaLogUrl / UIGF4ImportException ----------

    [Fact]
    public void GachaLogUrl_Constructor_StampsCurrentTime()
    {
        var before = DateTime.Now.AddSeconds(-2);
        var url = new GachaLogUrl(GameBiz.hk4e, 100000000, "https://example.com/?authkey=abc");
        var after = DateTime.Now.AddSeconds(2);

        Assert.Equal(GameBiz.hk4e, url.GameBiz);
        Assert.Equal(100000000, url.Uid);
        Assert.Equal("https://example.com/?authkey=abc", url.Url);
        Assert.InRange(url.Time, before, after);
    }


    [Fact]
    public void GachaLogUrl_ParameterlessConstructor_HasEmptyPayload()
    {
        var url = new GachaLogUrl();

        Assert.Equal(0, url.Uid);
        Assert.True(string.IsNullOrEmpty(url.Url));
    }


    [Fact]
    public void UIGF4ImportException_CarriesGameUidAndMessage()
    {
        var ex = new UIGF4ImportException(GameBiz.hkrpg, 123456789, "boom");

        Assert.Equal(GameBiz.hkrpg, ex.Game);
        Assert.Equal(123456789, ex.Uid);
        Assert.Equal("boom", ex.Message);
    }


    // ---------- GachaUidArchiveDisplay：导入导出列表的展示 ----------

    [Theory]
    [InlineData("hk4e")]
    [InlineData("hkrpg")]
    [InlineData("nap")]
    public void GachaUidArchiveDisplay_ArchiveName_MatchesGame(string gameBiz)
    {
        var game = new GameBiz(gameBiz);
        var display = new GachaUidArchiveDisplay { Game = game, Uid = 1 };

        string expected = gameBiz switch
        {
            "hk4e" => LunarWard.Language.Lang.GachaLogService_WishRecords,
            "hkrpg" => LunarWard.Language.Lang.GachaLogService_WarpRecords,
            _ => LunarWard.Language.Lang.GachaLogService_SignalSearchRecords,
        };
        Assert.Equal(expected, display.ArchiveName);
    }


    [Fact]
    public void GachaUidArchiveDisplay_TimeZoneOffset_ShiftsLastItemTime()
    {
        var display = new GachaUidArchiveDisplay
        {
            Game = GameBiz.hk4e,
            Uid = 1,
            LastItemTime = new DateTime(2024, 5, 1, 4, 0, 0),
        };

        display.Timezone = 8;

        Assert.Equal(new DateTime(2024, 5, 1, 12, 0, 0), display.LastItemTimeOffest);
    }


    // ---------- 稀有度背景图转换器 ----------

    [Theory]
    [InlineData("RarityToGIBgConverter", "Rarity_{0}_Background.png")]
    [InlineData("RarityToSRBgConverter", "FrameIconRarity0{0}.png")]
    public void RarityBackgroundConverters_MapOneToFive_AndFallBackToTransparent(string typeName, string pattern)
    {
        var converter = CreateConverter($"LunarWard.Features.Gacha.{typeName}");

        for (int rarity = 1; rarity <= 5; rarity++)
        {
            string actual = (string)converter.Convert(rarity, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture)!;
            Assert.Equal($"ms-appx:///Assets/Image/{string.Format(pattern, rarity)}", actual);
        }

        foreach (int other in new[] { 0, 6, 100 })
        {
            string actual = (string)converter.Convert(other, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture)!;
            Assert.Equal("ms-appx:///Assets/Image/Transparent.png", actual);
        }
    }


    [Theory]
    [InlineData(2, "B_Level_S.png")]
    [InlineData(3, "A_Level_S.png")]
    [InlineData(4, "S_Level_S.png")]
    [InlineData(5, "Transparent.png")]
    public void RarityToZZZIconConverter_MapsSABLevels(int rarity, string file)
    {
        var converter = CreateConverter("LunarWard.Features.Gacha.RarityToZZZIconConverter");

        string actual = (string)converter.Convert(rarity, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture)!;

        Assert.Equal($"ms-appx:///Assets/Image/{file}", actual);
    }


    // ---------- 保底进度背景渐变：三套保底档位 + 变色分界 ----------

    private static Avalonia.Media.LinearGradientBrush PityBrush(int gachaType, int pity)
    {
        var converter = CreateConverter("LunarWard.Features.Gacha.GachaPityProgressBackgroundBrushConverter");
        return (Avalonia.Media.LinearGradientBrush)converter.Convert(CreateItem(gachaType, pity), typeof(object), null, System.Globalization.CultureInfo.InvariantCulture)!;
    }


    [Theory]
    [InlineData(301, 74)]   // 原神角色池：74 抽起变红，90 保底
    [InlineData(302, 63)]   // 原神武器池：63 抽起变红，80 保底
    [InlineData(12, 63)]    // 星铁光锥池
    [InlineData(22, 63)]    // 星铁联动光锥池
    [InlineData(3, 65)]     // ZZZ 音擎池：65 抽起变红，80 保底
    [InlineData(5, 65)]     // ZZZ 邦布池
    public void PityBrush_TurnsRedAtUpstreamThreshold(int gachaType, int point)
    {
        var green = PityBrush(gachaType, point - 1);
        var red = PityBrush(gachaType, point);

        Assert.Equal(Avalonia.Media.Color.FromArgb(0xFF, 0x00, 0xE0, 0x79), green.GradientStops[0].Color);
        Assert.Equal(Avalonia.Media.Color.FromArgb(0xFF, 0xC8, 0x3C, 0x23), red.GradientStops[0].Color);
    }


    [Theory]
    [InlineData(301, 90)]
    [InlineData(302, 80)]
    [InlineData(3, 80)]
    public void PityBrush_UsesUpstreamGuaranteeForOffset(int gachaType, int guarantee)
    {
        var brush = PityBrush(gachaType, guarantee / 2);

        Assert.Equal(3, brush.GradientStops.Count);
        Assert.Equal(0.4, brush.Opacity, 6);
        Assert.Equal(0.5, brush.GradientStops[1].Offset, 6);
        Assert.Equal(0.5, brush.GradientStops[2].Offset, 6);
        Assert.Equal(Avalonia.Media.Colors.Transparent, brush.GradientStops[2].Color);
        Assert.Equal(new Avalonia.Point(0, 0), brush.StartPoint.Point);
        Assert.Equal(new Avalonia.Point(1, 0), brush.EndPoint.Point);
        Assert.Equal(Avalonia.RelativeUnit.Relative, brush.StartPoint.Unit);
        Assert.Equal(Avalonia.RelativeUnit.Relative, brush.EndPoint.Unit);
    }


    [Fact]
    public void PityBrush_IgnoresNonGachaLogItem()
    {
        var converter = CreateConverter("LunarWard.Features.Gacha.GachaPityProgressBackgroundBrushConverter");

        Assert.Null(converter.Convert("not an item", typeof(object), null, System.Globalization.CultureInfo.InvariantCulture));
    }


    // ---------- 自助查询统计数字转换器 ----------

    [Theory]
    [InlineData(12L, "+12")]
    [InlineData(-12L, "-12")]
    [InlineData(0L, "-")]
    public void SelfQueryStatsNumberConverter_SignedFormat(long value, string expected)
    {
        var converter = CreateConverter("LunarWard.Features.SelfQuery.SelfQueryStatsNumberConverter");

        Assert.Equal(expected, converter.Convert(value, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture));
    }


    [Fact]
    public void SelfQueryStatsNumberConverter_ConvertBack_Throws()
    {
        var converter = CreateConverter("LunarWard.Features.SelfQuery.SelfQueryStatsNumberConverter");

        Assert.Throws<NotImplementedException>(() => converter.ConvertBack("1", typeof(long), null, System.Globalization.CultureInfo.InvariantCulture));
    }

}
