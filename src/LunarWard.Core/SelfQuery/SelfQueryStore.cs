using System.Text.Json;

namespace LunarWard.Core.SelfQuery;

/// <summary>
/// 游戏内「数据查询」记录的本地持久化。
///
/// <para>上游 Starward 用 SQLite（Dapper + <c>INSERT OR REPLACE</c>）；LunarWard 没有数据库层，
/// 这里改用单个 JSON 文件保存三类记录，语义对齐：以 (Uid, Type, Id) 去重、按月统计与删除、
/// 「检索全部」时清掉涉及月份再整体重写。</para>
/// </summary>
public sealed class SelfQueryStore
{

    private readonly string _file;

    private readonly Lock _lock = new();

    private SelfQueryStoreData? _data;



    /// <param name="file">JSON 文件路径（父目录会自动创建）。</param>
    public SelfQueryStore(string file)
    {
        _file = file;
    }



    private SelfQueryStoreData Data
    {
        get
        {
            if (_data is null)
            {
                Load();
            }
            return _data!;
        }
    }



    private void Load()
    {
        lock (_lock)
        {
            if (_data is not null)
            {
                return;
            }
            try
            {
                if (File.Exists(_file))
                {
                    var bytes = File.ReadAllBytes(_file);
                    if (bytes.Length > 0)
                    {
                        _data = JsonSerializer.Deserialize<SelfQueryStoreData>(bytes, SelfQueryStoreJsonContext.Default.SelfQueryStoreData);
                    }
                }
            }
            catch (Exception)
            {
                // 文件损坏时从空库开始（与上游「查不到就是没有」一致），不阻塞页面。
            }
            _data ??= new SelfQueryStoreData();
        }
    }



    private void Save()
    {
        lock (_lock)
        {
            var dir = Path.GetDirectoryName(_file);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }
            var bytes = JsonSerializer.SerializeToUtf8Bytes(_data ?? new SelfQueryStoreData(), SelfQueryStoreJsonContext.Default.SelfQueryStoreData);
            File.WriteAllBytes(_file, bytes);
        }
    }





    #region Genshin

    public List<long> GetGenshinUids()
    {
        return Data.Genshin.Select(x => x.Uid).Distinct().OrderBy(x => x).ToList();
    }


    public (long Add, long Sub) GetGenshinQueryItemsNumSum(long uid, GenshinQueryType type)
    {
        var items = Data.Genshin.Where(x => x.Uid == uid && x.Type == type).ToList();
        return (items.Where(x => x.AddNum > 0).Sum(x => x.AddNum), items.Where(x => x.AddNum < 0).Sum(x => x.AddNum));
    }


    public long GetGenshinLastId(long uid, GenshinQueryType type)
    {
        return Data.Genshin.Where(x => x.Uid == uid && x.Type == type).Select(x => (long?)x.Id).Max() ?? 0;
    }


    public List<string> GetGenshinMonths(long uid, GenshinQueryType type)
    {
        return Data.Genshin.Where(x => x.Uid == uid && x.Type == type)
                           .Select(x => x.DateTime.ToString("yyyy-MM"))
                           .Distinct().OrderByDescending(x => x).ToList();
    }


    public List<GenshinQueryItem> GetGenshinQueryItems(long uid, GenshinQueryType type, string? month)
    {
        return Data.Genshin.Where(x => x.Uid == uid && x.Type == type)
                           .Where(x => month is null || x.DateTime.ToString("yyyy-MM") == month)
                           .OrderByDescending(x => x.Id).ToList();
    }


    public void DeleteGenshinMonth(long uid, GenshinQueryType type, string month)
    {
        Data.Genshin.RemoveAll(x => x.Uid == uid && x.Type == type && x.DateTime.ToString("yyyy-MM") == month);
        Save();
    }


    public void InsertGenshinQueryItems(long uid, GenshinQueryType type, List<GenshinQueryItem> items, bool all = false)
    {
        lock (_lock)
        {
            if (all)
            {
                foreach (var month in items.Select(x => x.DateTime.ToString("yyyy-MM")).Distinct())
                {
                    Data.Genshin.RemoveAll(x => x.Uid == uid && x.Type == type && x.DateTime.ToString("yyyy-MM") == month);
                }
            }
            foreach (var item in items)
            {
                Data.Genshin.RemoveAll(x => x.Uid == uid && x.Type == type && x.Id == item.Id);
                Data.Genshin.Add(item);
            }
        }
        Save();
    }

    #endregion





    #region Star Rail

    public List<long> GetStarRailUids()
    {
        return Data.StarRail.Select(x => x.Uid).Distinct().OrderBy(x => x).ToList();
    }


    public (long Add, long Sub) GetStarRailQueryItemsNumSum(long uid, StarRailQueryType type)
    {
        var items = Data.StarRail.Where(x => x.Uid == uid && x.Type == type).ToList();
        return (items.Where(x => x.AddNum > 0).Sum(x => x.AddNum), items.Where(x => x.AddNum < 0).Sum(x => x.AddNum));
    }


    public long GetStarRailLastId(long uid, StarRailQueryType type)
    {
        return Data.StarRail.Where(x => x.Uid == uid && x.Type == type).Select(x => (long?)x.Id).Max() ?? 0;
    }


    public List<string> GetStarRailMonths(long uid, StarRailQueryType type)
    {
        return Data.StarRail.Where(x => x.Uid == uid && x.Type == type)
                            .Select(x => x.Time.ToString("yyyy-MM"))
                            .Distinct().OrderByDescending(x => x).ToList();
    }


    public List<StarRailQueryItem> GetStarRailQueryItems(long uid, StarRailQueryType type, string? month)
    {
        return Data.StarRail.Where(x => x.Uid == uid && x.Type == type)
                            .Where(x => month is null || x.Time.ToString("yyyy-MM") == month)
                            .OrderByDescending(x => x.Id).ToList();
    }


    public void DeleteStarRailMonth(long uid, StarRailQueryType type, string month)
    {
        Data.StarRail.RemoveAll(x => x.Uid == uid && x.Type == type && x.Time.ToString("yyyy-MM") == month);
        Save();
    }


    public void InsertStarRailQueryItems(long uid, StarRailQueryType type, List<StarRailQueryItem> items, bool all = false)
    {
        lock (_lock)
        {
            if (all)
            {
                foreach (var month in items.Select(x => x.Time.ToString("yyyy-MM")).Distinct())
                {
                    Data.StarRail.RemoveAll(x => x.Uid == uid && x.Type == type && x.Time.ToString("yyyy-MM") == month);
                }
            }
            foreach (var item in items)
            {
                Data.StarRail.RemoveAll(x => x.Uid == uid && x.Type == type && x.Id == item.Id);
                Data.StarRail.Add(item);
            }
        }
        Save();
    }

    #endregion





    #region ZZZ

    public List<long> GetZZZUids()
    {
        return Data.ZZZ.Select(x => x.Uid).Distinct().OrderBy(x => x).ToList();
    }


    public (long Add, long Sub) GetZZZQueryItemsNumSum(long uid, ZZZQueryType type)
    {
        var items = Data.ZZZ.Where(x => x.Uid == uid && x.Type == type).ToList();
        return (items.Where(x => x.AddNum > 0).Sum(x => x.AddNum), items.Where(x => x.AddNum < 0).Sum(x => x.AddNum));
    }


    public long GetZZZLastId(long uid, ZZZQueryType type)
    {
        return Data.ZZZ.Where(x => x.Uid == uid && x.Type == type).Select(x => (long?)x.Id).Max() ?? 0;
    }


    public List<string> GetZZZMonths(long uid, ZZZQueryType type)
    {
        return Data.ZZZ.Where(x => x.Uid == uid && x.Type == type)
                       .Select(x => x.DateTime.ToString("yyyy-MM"))
                       .Distinct().OrderByDescending(x => x).ToList();
    }


    public List<ZZZQueryItem> GetZZZQueryItems(long uid, ZZZQueryType type, string? month)
    {
        return Data.ZZZ.Where(x => x.Uid == uid && x.Type == type)
                       .Where(x => month is null || x.DateTime.ToString("yyyy-MM") == month)
                       .OrderByDescending(x => x.Id).ToList();
    }


    public void DeleteZZZMonth(long uid, ZZZQueryType type, string month)
    {
        Data.ZZZ.RemoveAll(x => x.Uid == uid && x.Type == type && x.DateTime.ToString("yyyy-MM") == month);
        Save();
    }


    public void InsertZZZQueryItems(long uid, ZZZQueryType type, List<ZZZQueryItem> items, bool all = false)
    {
        lock (_lock)
        {
            if (all)
            {
                foreach (var month in items.Select(x => x.DateTime.ToString("yyyy-MM")).Distinct())
                {
                    Data.ZZZ.RemoveAll(x => x.Uid == uid && x.Type == type && x.DateTime.ToString("yyyy-MM") == month);
                }
            }
            foreach (var item in items)
            {
                Data.ZZZ.RemoveAll(x => x.Uid == uid && x.Type == type && x.Id == item.Id);
                Data.ZZZ.Add(item);
            }
        }
        Save();
    }

    #endregion

}





/// <summary>JSON 文件结构（一个文件装三类记录，与上游三张表对应）。</summary>
public sealed class SelfQueryStoreData
{
    public List<GenshinQueryItem> Genshin { get; set; } = new();

    public List<StarRailQueryItem> StarRail { get; set; } = new();

    public List<ZZZQueryItem> ZZZ { get; set; } = new();
}
