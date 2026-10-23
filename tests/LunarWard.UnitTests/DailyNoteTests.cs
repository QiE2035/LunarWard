using LunarWard.Core.GameRecord;
using LunarWard.Core.GameRecord.Genshin.DailyNote;
using LunarWard.Core.GameRecord.StarRail.DailyNote;
using LunarWard.Core.GameRecord.ZZZ.DailyNote;
using LunarWard.Launch;
using LunarWard.Platform;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>便笺归一化（原神 / 绝区零 / 星穹铁道 → 统一形态）与凭据存储。</summary>
public class DailyNoteTests
{
    private static GameRecordRole Role(string nickname = "旅行者", long uid = 100000000)
        => new() { Nickname = nickname, Uid = uid, Region = "cn_gf01", Level = 60 };


    [Fact]
    public void Genshin_ToSummary_MapsResinAndTasks()
    {
        var note = new GenshinDailyNote
        {
            CurrentResin = 120,
            MaxResin = 200,
            ResinRecoveryTime = TimeSpan.FromMinutes(123),
            FinishedTaskNum = 4,
            TotalTaskNum = 4,
            CurrentExpeditionNum = 5,
            MaxExpeditionNum = 5,
            CurrentHomeCoin = 100,
            MaxHomeCoin = 2400,
            RemainResinDiscountNum = 3,
            ResinDiscountNumLimit = 3,
        };

        var summary = DailyNoteService.ToSummary(note, Role());

        Assert.Equal("genshin", summary.GameKind);
        Assert.Equal("原粹树脂", summary.PrimaryName);
        Assert.Equal(120, summary.PrimaryCurrent);
        Assert.Equal(200, summary.PrimaryMax);
        Assert.Equal(0.6, summary.PrimaryPercent, 3);
        Assert.Equal(TimeSpan.FromMinutes(123), summary.PrimaryRecovery);
        Assert.True(summary.DailyTasksDone);
        Assert.Equal(4, summary.Items.Count);
        Assert.Contains(summary.Items, i => i.Name == "每日委托" && i.Value == "4/4");
        Assert.Equal("旅行者", summary.Nickname);
        Assert.Equal(100000000, summary.Uid);
    }


    [Fact]
    public void Genshin_DailyTasksNotDone()
    {
        var note = new GenshinDailyNote { FinishedTaskNum = 2, TotalTaskNum = 4 };
        Assert.False(DailyNoteService.ToSummary(note, Role()).DailyTasksDone);
    }


    [Fact]
    public void Genshin_ZeroTasksIsNotDone()
    {
        var note = new GenshinDailyNote { FinishedTaskNum = 0, TotalTaskNum = 0 };
        Assert.False(DailyNoteService.ToSummary(note, Role()).DailyTasksDone);
    }


    [Fact]
    public void Zzz_ToSummary_MapsEnergy()
    {
        var note = new ZZZDailyNote
        {
            Energy = new Energy
            {
                Progress = new EnergyProgress { Current = 100, Max = 240 },
                Restore = TimeSpan.FromMinutes(456),
            },
            Vitality = new Vitality { Current = 200, Max = 200 },
            VhsSale = new VhsSale { SaleState = "SaleStateDoing" },
            CardSign = "CardSignDone",
        };

        var summary = DailyNoteService.ToSummary(note, Role("绳匠"));

        Assert.Equal("zzz", summary.GameKind);
        Assert.Equal("电量", summary.PrimaryName);
        Assert.Equal(100, summary.PrimaryCurrent);
        Assert.Equal(240, summary.PrimaryMax);
        Assert.Equal(TimeSpan.FromMinutes(456), summary.PrimaryRecovery);
        Assert.Equal("绳匠", summary.Nickname);
        Assert.Contains(summary.Items, i => i.Name == "活跃度" && i.Value == "200/200");
        Assert.Contains(summary.Items, i => i.Name == "刮刮卡" && i.Value == "已完成");
    }


    [Fact]
    public void Zzz_MissingEnergy_DoesNotThrow()
    {
        var summary = DailyNoteService.ToSummary(new ZZZDailyNote(), Role());
        Assert.Equal(0, summary.PrimaryCurrent);
        Assert.Equal(0, summary.PrimaryMax);
        Assert.Equal(0, summary.PrimaryPercent);
    }


    [Fact]
    public void StarRail_ToSummary_MapsStamina()
    {
        var note = new StarRailDailyNote
        {
            CurrentStamina = 180,
            MaxStamina = 300,
            StaminaRecoverTime = TimeSpan.FromMinutes(60),
            CurrentTrainScore = 500,
            MaxTrainScore = 500,
            AcceptedExpeditionNum = 4,
            TotalExpeditionNum = 4,
            WeeklyCocoonCnt = 3,
            WeeklyCocoonLimit = 3,
            CurrentReserveStamina = 2400,
        };

        var summary = DailyNoteService.ToSummary(note, Role());

        Assert.Equal("hkrpg", summary.GameKind);
        Assert.Equal("开拓力", summary.PrimaryName);
        Assert.Equal(180, summary.PrimaryCurrent);
        Assert.Equal(300, summary.PrimaryMax);
        Assert.True(summary.DailyTasksDone);
        Assert.Contains(summary.Items, i => i.Name == "每日实训" && i.Value == "500/500");
        Assert.Contains(summary.Items, i => i.Name == "后备开拓力" && i.Value == "2400");
    }


    [Fact]
    public void Credentials_RoundTrip()
    {
        using var temp = new TempDirectory();
        var paths = new XdgPaths(temp.Combine("c"), temp.Combine("d"), temp.Combine("k"));
        var service = new DailyNoteService(paths);

        Assert.Null(service.GetCredentials("cn"));

        service.SaveCredentials("cn", "token=abc; uid=100", "100", "cn_gf01");
        var entry = service.GetCredentials("cn");

        Assert.NotNull(entry);
        Assert.Equal("token=abc; uid=100", entry!.Cookie);
        Assert.Equal("100", entry.Uid);
        Assert.Equal("cn_gf01", entry.Region);
    }


    [Fact]
    public void Credentials_FileIsPrivate()
    {
        using var temp = new TempDirectory();
        var paths = new XdgPaths(temp.Combine("c"), temp.Combine("d"), temp.Combine("k"));
        var service = new DailyNoteService(paths);
        service.SaveCredentials("cn", "secret-cookie");

        var mode = File.GetUnixFileMode(service.StorePath);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
    }


    [Fact]
    public void Credentials_ClearRemovesEntry()
    {
        using var temp = new TempDirectory();
        var paths = new XdgPaths(temp.Combine("c"), temp.Combine("d"), temp.Combine("k"));
        var service = new DailyNoteService(paths);

        service.SaveCredentials("cn", "a");
        service.SaveCredentials("global", "b");
        service.ClearCredentials("cn");

        Assert.Null(service.GetCredentials("cn"));
        Assert.NotNull(service.GetCredentials("global"));
    }


    [Fact]
    public void Credentials_RejectEmptyCookie()
    {
        using var temp = new TempDirectory();
        var paths = new XdgPaths(temp.Combine("c"), temp.Combine("d"), temp.Combine("k"));
        var service = new DailyNoteService(paths);

        Assert.Throws<ArgumentException>(() => service.SaveCredentials("cn", "   "));
    }


    [Fact]
    public async Task Fetch_WithoutCookie_ThrowsWithHelpfulMessage()
    {
        using var temp = new TempDirectory();
        var paths = new XdgPaths(temp.Combine("c"), temp.Combine("d"), temp.Combine("k"));
        var service = new DailyNoteService(paths);

        var role = Role();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.FetchAsync("genshin", new LunarWard.Core.GameBiz("hk4e_cn"), role));
        Assert.Contains("cookie", ex.Message);
    }
}
