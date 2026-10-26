using System.Reflection;
using System.Text.Json.Serialization;
using Xunit;

namespace LunarWard.UnitTests.Features.Gacha;

/// <summary>
/// 「模型 / 数据表」类文件的主路径回归：
/// <list type="bullet">
/// <item><c>Features/Gacha/GachaNoUp.cs</c>（移植版）与 <c>Core/Gacha/GachaNoUp.cs</c>（本地遗留 + <c>ComputeIsUp</c>）
/// 的三游戏区间表必须逐项一致，否则服务层算出来的 UP 标记会跟着变（docs/13 §五 G-9）。</item>
/// <item><c>ZZZGachaToolbox/{ItemList,IconInfo}.cs</c> 的 JSON 字段映射（米游社图鉴接口的 snake_case 字段）。</item>
/// </list>
/// </summary>
public class GachaModelParityTests
{

    [Fact]
    public void GachaNoUp_FeaturesCopy_ContainsSameCardPoolsAsCoreLegacyCopy()
    {
        var coreKeys = LunarWard.Core.Gacha.GachaNoUp.Dictionary.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var featureKeys = LunarWard.Features.Gacha.GachaNoUp.Dictionary.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray();

        Assert.Equal(coreKeys, featureKeys);
        Assert.Equal(3, coreKeys.Select(x => x[..4]).Distinct().Count()); // hk4e / hkrpg / nap
    }


    [Fact]
    public void GachaNoUp_FeaturesCopy_ContainsSameItemPeriodsAsCoreLegacyCopy()
    {
        foreach ((string key, var corePool) in LunarWard.Core.Gacha.GachaNoUp.Dictionary)
        {
            Assert.True(LunarWard.Features.Gacha.GachaNoUp.Dictionary.TryGetValue(key, out var featurePool), $"缺少卡池 {key}");

            Assert.Equal(corePool.Items.Count, featurePool.Items.Count);

            foreach ((int itemId, var coreItem) in corePool.Items)
            {
                Assert.True(featurePool.Items.TryGetValue(itemId, out var featureItem), $"{key} 缺少物品 {itemId}");
                Assert.Equal(coreItem.NoUpTimes, featureItem.NoUpTimes);
            }
        }
    }


    [Fact]
    public void GachaNoUp_ComputeIsUp_OnlyExistsOnCoreLegacyCopy()
    {
        Type core = typeof(LunarWard.Core.Gacha.GachaNoUp);
        Type feature = typeof(LunarWard.Features.Gacha.GachaNoUp);

        Assert.NotNull(core.GetMethod("ComputeIsUp", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(feature.GetMethod("ComputeIsUp", BindingFlags.Public | BindingFlags.Static));
    }


    [Fact]
    public void GachaNoUp_FeatureCopy_KeepsUpstreamPlainShape()
    {
        // 移植版是净上游写法：可空引用类型用 `null!` 的初始化器一个都没有（Core 遗留版才有）
        var pool = typeof(LunarWard.Features.Gacha.GachaNoUp);
        var item = typeof(LunarWard.Features.Gacha.GachaNoUpItem);

        Assert.Equal("Game", pool.GetProperty("Game")!.Name);
        Assert.Equal("Name", pool.GetProperty("Name")!.Name);
        Assert.Equal("NoUpTimes", item.GetProperty("NoUpTimes")!.Name);
    }


    private static Type ZZZ(string name)
    {
        Type? type = typeof(LunarWard.Features.Gacha.GachaLogItemEx).Assembly.GetType($"LunarWard.Features.Gacha.ZZZGachaToolbox.{name}");
        Assert.NotNull(type);
        return type!;
    }


    [Theory]
    [InlineData("weapon")]
    [InlineData("avatar_list")]
    [InlineData("buddy_list")]
    public void ZZZItemList_MapsUpstreamJsonFieldNames(string jsonName)
    {
        string[] properties = ZZZ("ItemList").GetProperties()
            .Where(x => x.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name == jsonName)
            .Select(x => x.Name)
            .ToArray();

        Assert.Single(properties);
    }


    [Theory]
    [InlineData("name_mi18n", "NameMi18n")]
    [InlineData("full_name_mi18n", "FullNameMi18n")]
    [InlineData("element_type", "ElementType")]
    [InlineData("camp_name_mi18n", "CampNameMi18n")]
    [InlineData("avatar_profession", "AvatarProfession")]
    [InlineData("group_icon_path", "GroupIconPath")]
    [InlineData("hollow_icon_path", "HollowIconPath")]
    public void ZZZItemListAvatar_MapsUpstreamJsonFieldNames(string jsonName, string propertyName)
    {
        var property = ZZZ("ItemListAvatar").GetProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal(jsonName, property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name);
    }


    [Theory]
    [InlineData("avatar_icon")]
    [InlineData("buddy_icon")]
    public void ZZZIconInfo_MapsUpstreamJsonFieldNames(string jsonName)
    {
        string[] properties = ZZZ("IconInfo").GetProperties()
            .Where(x => x.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name == jsonName)
            .Select(x => x.Name)
            .ToArray();

        Assert.Single(properties);
    }


    [Theory]
    [InlineData("square_avatar", "SquareAvatar")]
    [InlineData("rectangle_avatar", "RectangleAvatar")]
    [InlineData("vertical_painting", "VerticalPainting")]
    [InlineData("vertical_painting_color", "VerticalPaintingColor")]
    [InlineData("avatar_us_full_name", "AvatarUsFullName")]
    [InlineData("teaser_avatar", "TeaserAvatar")]
    public void ZZZIconInfoItem_MapsUpstreamJsonFieldNames(string jsonName, string propertyName)
    {
        var property = ZZZ("IconInfoItem").GetProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal(jsonName, property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name);
    }

}
