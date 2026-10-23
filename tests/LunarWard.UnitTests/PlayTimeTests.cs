using System.Text.Json;
using LunarWard.Launch;
using LunarWard.Platform;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>游戏时长统计（复用相位引擎的起止时间）。</summary>
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
    public void Record_AccumulatesTotalPerProfile()
    {
        using var temp = new TempDirectory();
        var paths = Paths(temp);
        var service = new PlayTimeService(paths);

        var start = DateTimeOffset.Now;
        service.Record("genshin-cn", Result(start, start.AddMinutes(30)));
        service.Record("genshin-cn", Result(start.AddHours(1), start.AddHours(1).AddMinutes(45)));
        service.Record("zzz-cn", Result(start, start.AddMinutes(10)));

        Assert.Equal(TimeSpan.FromMinutes(75), service.TotalFor("genshin-cn"));
        Assert.Equal(TimeSpan.FromMinutes(10), service.TotalFor("zzz-cn"));
        Assert.Equal(TimeSpan.Zero, service.TotalFor("nope"));
        Assert.Equal(3, service.Sessions.Count);
    }


    [Fact]
    public void Record_PersistsAcrossInstances()
    {
        using var temp = new TempDirectory();
        var paths = Paths(temp);
        var start = DateTimeOffset.Now;

        var first = new PlayTimeService(paths);
        first.Record("p", Result(start, start.AddMinutes(20)));

        var second = new PlayTimeService(paths);
        Assert.Equal(TimeSpan.FromMinutes(20), second.TotalFor("p"));
        Assert.Equal(ReadySignal.WineDevice.ToString(), second.LastFor("p")?.ReadySignal);
    }


    [Fact]
    public void Record_IgnoresMissingOrInvertedTimestamps()
    {
        using var temp = new TempDirectory();
        var service = new PlayTimeService(Paths(temp));

        service.Record("p", new LaunchResult { Success = true });
        service.Record("p", new LaunchResult
        {
            Success = true,
            StartedAt = DateTimeOffset.Now,
            ExitedAt = DateTimeOffset.Now.AddMinutes(-1),
        });

        Assert.Empty(service.Sessions);
    }


    [Fact]
    public void Sessions_AreOrderedNewestFirst()
    {
        using var temp = new TempDirectory();
        var service = new PlayTimeService(Paths(temp));
        var start = DateTimeOffset.Now;

        service.Record("p", Result(start, start.AddMinutes(1)));
        service.Record("p", Result(start.AddHours(2), start.AddHours(2).AddMinutes(1)));

        Assert.True(service.Sessions[0].StartedAt > service.Sessions[1].StartedAt);
    }


    [Fact]
    public void CorruptStore_IsBackedUpAndReset()
    {
        using var temp = new TempDirectory();
        var paths = Paths(temp);
        Directory.CreateDirectory(paths.DataHome);
        File.WriteAllText(Path.Join(paths.DataHome, "playtime.json"), "{ not json ");

        var service = new PlayTimeService(paths);
        Assert.Empty(service.Sessions);
        Assert.Single(Directory.GetFiles(paths.DataHome, "playtime.json.bad-*"));
    }


    [Fact]
    public void Store_PreservesUnknownFields()
    {
        using var temp = new TempDirectory();
        var paths = Paths(temp);
        Directory.CreateDirectory(paths.DataHome);
        var store = Path.Join(paths.DataHome, "playtime.json");
        File.WriteAllText(store, """
        {
          "schema_version": 1,
          "future_field": [1, 2, 3],
          "sessions": []
        }
        """);

        var service = new PlayTimeService(paths);
        var start = DateTimeOffset.Now;
        service.Record("p", Result(start, start.AddMinutes(5)));

        Assert.Contains("future_field", File.ReadAllText(store));
    }


    [Fact]
    public void Store_IsValidJsonAndAtomic()
    {
        using var temp = new TempDirectory();
        var paths = Paths(temp);
        var service = new PlayTimeService(paths);
        var start = DateTimeOffset.Now;
        service.Record("p", Result(start, start.AddMinutes(5)));

        var text = File.ReadAllText(Path.Join(paths.DataHome, "playtime.json"));
        Assert.False(File.Exists(Path.Join(paths.DataHome, "playtime.json.tmp")));
        using var document = JsonDocument.Parse(text);
        Assert.True(document.RootElement.TryGetProperty("sessions", out var sessions));
        Assert.Equal(1, sessions.GetArrayLength());
    }
}
