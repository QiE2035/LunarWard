using LunarWard.Launch;
using LunarWard.Launch.Database;
using LunarWard.Platform;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 游戏时长统计（复用相位引擎的起止时间）。存储已从自造的 <c>playtime.json</c>
/// 改为 Starward 同款 SQLite 表 <c>PlayTimeStats</c>（见 <see cref="PlayTimeStore" />）。
/// </summary>
public class PlayTimeTests
{
    private static XdgPaths Paths(TempDirectory temp)
        => new(temp.Combine("c"), temp.Combine("d"), temp.Combine("k"));


    private static LaunchResult Result(DateTimeOffset start, DateTimeOffset end, int? exitCode = 0)
        => new()
        {
            Success = true,
            StartedAt = start,
            ExitedAt = end,
            GameExitCode = exitCode,
            Signal = ReadySignal.WineDevice,
        };


    [Fact]
    public void Record_AccumulatesTotalPerGameBiz()
    {
        using var temp = new TempDirectory();
        var paths = Paths(temp);
        var service = new PlayTimeService(paths);

        var start = DateTimeOffset.Now;
        service.Record("hk4e_cn", 0, Result(start, start.AddMinutes(30)));
        service.Record("hk4e_cn", 0, Result(start.AddHours(1), start.AddHours(1).AddMinutes(45)));
        service.Record("nap_cn", 0, Result(start, start.AddMinutes(10)));

        Assert.Equal(TimeSpan.FromMinutes(75), service.TotalFor("hk4e_cn"));
        Assert.Equal(TimeSpan.FromMinutes(10), service.TotalFor("nap_cn"));
        Assert.Equal(TimeSpan.Zero, service.TotalFor("nope"));
        Assert.Equal(2, service.Sessions("hk4e_cn").Count);
        Assert.Single(service.Sessions("nap_cn"));
    }


    [Fact]
    public void Record_PersistsAcrossInstances()
    {
        using var temp = new TempDirectory();
        var paths = Paths(temp);
        var start = DateTimeOffset.Now;

        var first = new PlayTimeService(paths);
        first.Record("hkrpg_cn", 1234, Result(start, start.AddMinutes(20)));

        // 新实例（新连接）读同一个库
        var second = new PlayTimeService(paths);
        Assert.Equal(TimeSpan.FromMinutes(20), second.TotalFor("hkrpg_cn"));
        Assert.NotNull(second.LastFor("hkrpg_cn"));
    }


    [Fact]
    public void Record_IgnoresMissingOrInvertedTimestamps()
    {
        using var temp = new TempDirectory();
        var service = new PlayTimeService(Paths(temp));

        service.Record("hk4e_cn", 0, new LaunchResult { Success = true });
        service.Record("hk4e_cn", 0, new LaunchResult
        {
            Success = true,
            StartedAt = DateTimeOffset.Now,
            ExitedAt = DateTimeOffset.Now.AddMinutes(-1),
        });

        Assert.Empty(service.Sessions("hk4e_cn"));
    }


    [Fact]
    public void Sessions_AreOrderedNewestFirst()
    {
        using var temp = new TempDirectory();
        var service = new PlayTimeService(Paths(temp));
        var start = DateTimeOffset.Now;

        service.Record("hk4e_cn", 0, Result(start, start.AddMinutes(1)));
        service.Record("hk4e_cn", 0, Result(start.AddHours(2), start.AddHours(2).AddMinutes(1)));

        var sessions = service.Sessions("hk4e_cn");
        Assert.True(sessions[0].StartedAt > sessions[1].StartedAt);
    }


    [Fact]
    public void Record_SameSessionTwice_IsOneRow()
    {
        using var temp = new TempDirectory();
        var service = new PlayTimeService(Paths(temp));
        var start = DateTimeOffset.Now;
        var result = Result(start, start.AddMinutes(15));

        service.Record("hk4e_cn", 42, result);
        service.Record("hk4e_cn", 42, result);

        // 上游 PlayTimeStats 的唯一索引是 (GameBiz, StartTime, Pid)
        Assert.Single(service.Sessions("hk4e_cn"));
        Assert.Equal(TimeSpan.FromMinutes(15), service.TotalFor("hk4e_cn"));
    }


    [Fact]
    public void Record_WritesIntoStarwardSchema_NotJsonFile()
    {
        using var temp = new TempDirectory();
        var paths = Paths(temp);
        var service = new PlayTimeService(paths);
        var start = DateTimeOffset.Now;
        service.Record("hk4e_cn", 7, Result(start, start.AddMinutes(5)));

        // 不再产生 playtime.json
        Assert.False(File.Exists(Path.Join(paths.DataHome, "playtime.json")));
        // 落在与抽卡同一个库的 PlayTimeStats 表
        Assert.True(File.Exists(Path.Join(paths.DataHome, LunarWardDatabase.FileName)));

        var store = PlayTimeStore.OpenDefault(paths.DataHome);
        PlayTimeRecord record = Assert.Single(store.GetAll());
        Assert.Equal("hk4e_cn", record.GameBiz);
        Assert.Equal(7, record.Pid);
        Assert.Equal(start.ToUnixTimeMilliseconds(), record.StartedAt);
    }

    [Fact]
    public void RecordSession_AppendsExternalSession()
    {
        using var temp = new TempDirectory();
        var service = new PlayTimeService(Paths(temp));
        var start = DateTimeOffset.Now.AddMinutes(-20);

        // 上游 playtime --pid 子命令：外部启动的游戏退出后补一条统计
        service.RecordSession("nap_cn", 4321, start, start.AddMinutes(18));

        PlaySession? last = service.LastFor("nap_cn");
        Assert.NotNull(last);
        Assert.Equal("nap_cn", last!.GameBiz);
        Assert.Equal(18, (int)last.Duration.TotalMinutes);
        Assert.Equal(18, (int)service.TotalFor("nap_cn").TotalMinutes);
    }
}
