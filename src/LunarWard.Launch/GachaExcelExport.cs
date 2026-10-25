using MiniExcelLibs;

namespace LunarWard.Launch;

/// <summary>
/// 抽卡记录导出 Excel（对应 Starward <c>GenshinGachaService.ExportAsExcelAsync</c> /
/// <c>StarRailGachaService.ExportAsExcelAsync</c> / <c>ZZZGachaService.ExportAsExcelAsync</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 上游用 MiniExcel 的模板语法：<c>Assets/Template/GachaLog.xlsx</c> 里一行
/// <c>{{list.Xxx}}</c> 占位符，<c>SaveAsByTemplateAsync(output, template, new { list })</c>
/// 把行重复展开。模板是二进制素材，直接复制自 Starward 安装目录的同名文件
/// （<c>Assets/Template/GachaLog.xlsx</c>），列头与占位符完全一致：
/// uid / id / time / name / item_type / rank_type / gacha_type / index / pity。
/// </para>
/// <para>
/// <c>Index</c> 与 <c>Pity</c> 的口径照 <c>GetGachaLogItemEx</c>：行按 Id 升序输出，
/// 数值按「可查询卡池」分组（原神 400 与 301 同池）分别累加；出 5★ 时 Pity 归零
/// （当前行仍算在保底内，与上游 <c>item.Pity = ++pity; if (RankType == 5) pity = 0;</c> 一致）。
/// </para>
/// </remarks>
public static class GachaExcelExport
{
    /// <summary>模板相对路径（与上游一样放在输出目录的 <c>Assets/Template</c> 下）。</summary>
    public const string TemplateRelativePath = "Assets/Template/GachaLog.xlsx";


    /// <summary>模板文件绝对路径；文件不存在时导出应被禁用。</summary>
    public static string TemplatePath => Path.Join(AppContext.BaseDirectory, TemplateRelativePath);


    public static bool IsAvailable => File.Exists(TemplatePath);


    /// <summary>
    /// 把记录写进模板并保存到 <paramref name="output"/>（覆盖）。
    /// </summary>
    public static async Task ExportAsync(string output, long uid, IReadOnlyList<GachaRecord> records)
    {
        var rows = BuildRows(uid, records);
        await MiniExcel.SaveAsByTemplateAsync(output, TemplatePath, new { list = rows });
    }


    /// <summary>
    /// 生成模板行（单独拆出来便于单测；顺序 = <c>ORDER BY Id</c>）。
    /// </summary>
    public static List<GachaExcelRow> BuildRows(long uid, IReadOnlyList<GachaRecord> records)
    {
        var ordered = records.OrderBy(x => x.Id).ToList();

        // 上游：先按 Id 排好整个列表并返回；Index/Pity 是「按卡池分别遍历」时算出来的
        var positions = new Dictionary<long, (int Index, int Pity)>(ordered.Count);
        foreach (var group in ordered.GroupBy(x => PoolKey(x.GachaType)))
        {
            var index = 0;
            var pity = 0;
            foreach (var record in group)
            {
                index++;
                pity++;
                positions[record.Id] = (index, pity);
                if (record.RankType == 5)
                {
                    pity = 0;
                }
            }
        }

        var rows = new List<GachaExcelRow>(ordered.Count);
        foreach (var record in ordered)
        {
            var (index, pity) = positions[record.Id];
            rows.Add(new GachaExcelRow
            {
                Uid = uid,
                IdText = record.Id.ToString(),
                Time = record.Time,
                Name = record.Name,
                ItemType = record.ItemType,
                RankType = record.RankType,
                GachaType = record.GachaType,
                Index = index,
                Pity = pity,
            });
        }
        return rows;
    }


    /// <summary>可查询卡池的归并键：上游把原神 400（集录祈愿）并进 301。</summary>
    private static int PoolKey(int gachaType) => gachaType == 400 ? 301 : gachaType;
}


/// <summary>
/// 模板占位符对应的行模型。属性名必须与 <c>GachaLog.xlsx</c> 里的
/// <c>{{list.Xxx}}</c> 占位符一致（MiniExcel 按名字反射，大小写不敏感）。
/// </summary>
public sealed class GachaExcelRow
{
    public long Uid { get; init; }

    public required string IdText { get; init; }

    public DateTime Time { get; init; }

    public required string Name { get; init; }

    public required string ItemType { get; init; }

    public int RankType { get; init; }

    public int GachaType { get; init; }

    public int Index { get; init; }

    public int Pity { get; init; }
}
