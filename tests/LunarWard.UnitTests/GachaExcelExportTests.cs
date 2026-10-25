using Xunit;
using LunarWard.Launch;

namespace LunarWard.UnitTests;

/// <summary>
/// <see cref="GachaExcelExport"/> 的行计算与模板导出。
/// </summary>
public sealed class GachaExcelExportTests
{
    private static GachaRecord Record(long id, int gachaType, int rankType, string name = "祭礼剑") => new()
    {
        Uid = 100000001,
        Id = id,
        Name = name,
        ItemType = "武器",
        RankType = rankType,
        GachaType = gachaType,
        GachaTypeName = gachaType.ToString(),
        Time = new DateTime(2026, 9, 25, 12, 0, 0),
        Count = 1,
    };


    [Fact]
    public void BuildRows_ResetsPityOnFiveStar()
    {
        var rows = GachaExcelExport.BuildRows(100000001,
        [
            Record(1, 301, 3),
            Record(2, 301, 3),
            Record(3, 301, 5),
            Record(4, 301, 3),
        ]);

        Assert.Equal(new[] { 1, 2, 3, 4 }, rows.Select(x => x.Index));
        Assert.Equal(new[] { 1, 2, 3, 1 }, rows.Select(x => x.Pity));
        Assert.Equal("100000001", rows[0].Uid.ToString());
        Assert.Equal("1", rows[0].IdText);
    }


    [Fact]
    public void BuildRows_Merges400Into301Pool()
    {
        var rows = GachaExcelExport.BuildRows(100000001,
        [
            Record(1, 301, 3),
            Record(2, 400, 3),
            Record(3, 301, 3),
        ]);

        // 400（集录祈愿）与 301（角色活动祈愿）在上游算同一个可查询卡池
        Assert.Equal(new[] { 1, 2, 3 }, rows.Select(x => x.Index));
        Assert.Equal(new[] { 1, 2, 3 }, rows.Select(x => x.Pity));
        // 原始 gacha_type 保留 400，不改成 301
        Assert.Equal(new[] { 301, 400, 301 }, rows.Select(x => x.GachaType));
    }


    [Fact]
    public void BuildRows_SeparatesPools()
    {
        var rows = GachaExcelExport.BuildRows(100000001,
        [
            Record(1, 100, 3),
            Record(2, 200, 3),
            Record(3, 100, 4),
        ]);

        Assert.Equal(new[] { 1, 1, 2 }, rows.Select(x => x.Index));
        Assert.Equal(new[] { 1, 1, 2 }, rows.Select(x => x.Pity));
    }


    [Fact]
    public async Task ExportAsync_WritesWorkbook()
    {
        var dir = Path.Join(Path.GetTempPath(), "lunarward-excel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var output = Path.Join(dir, "gacha.xlsx");
        try
        {
            await GachaExcelExport.ExportAsync(output, 100000001,
            [
                Record(1, 301, 3),
                Record(2, 301, 4),
            ]);

            Assert.True(File.Exists(output));
            Assert.True(new FileInfo(output).Length > 3000);
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
                // 清理失败不影响断言
            }
        }
    }


    [Fact]
    public void Template_IsShippedNextToBinary()
    {
        // 模板随 Launch 项目输出到 bin/<config>/Assets/Template/GachaLog.xlsx
        var path = Path.Join(AppContext.BaseDirectory, GachaExcelExport.TemplateRelativePath);
        Assert.True(File.Exists(path), path);
    }
}
