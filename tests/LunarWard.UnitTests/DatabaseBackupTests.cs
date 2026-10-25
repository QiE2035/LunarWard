using LunarWard.Launch;
using LunarWard.Launch.Database;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>文件管理页「备份数据库」（SQLite <c>VACUUM INTO</c>）的端到端校验。</summary>
public class DatabaseBackupTests
{
    private static GachaRecord Record(long id, string name) => new()
    {
        Id = id,
        Uid = 100000001,
        GachaType = 301,
        ItemId = 10000000 + (int)id,
        ItemType = "武器",
        GachaTypeName = "角色活动祈愿",
        Name = name,
        Time = new DateTime(2026, 1, 1).AddMinutes(id),
        RankType = id % 2 == 0 ? 4 : 3,
        Count = 1,
    };


    [Fact]
    public void BackupTo_WritesConsistentSnapshotThatCanBeReopened()
    {
        using var temp = new TempDirectory();
        var source = new LunarWardDatabase(Path.Join(temp.Path, LunarWardDatabase.FileName));
        new GachaLogStore(source).Merge("genshin", [Record(1, "A"), Record(2, "B")], "zh-cn");

        var backupPath = Path.Join(temp.Combine("backup"), "LunarWard_20261025.db");
        source.BackupTo(backupPath);

        Assert.True(File.Exists(backupPath));
        var restored = new GachaLogStore(new LunarWardDatabase(backupPath));
        var items = restored.GetAll("genshin");
        Assert.Equal(2, items.Count);
        Assert.Equal("B", items[1].Name);
    }


    [Fact]
    public void BackupTo_OverwritesExistingTargetAtomically()
    {
        using var temp = new TempDirectory();
        var source = new LunarWardDatabase(Path.Join(temp.Path, LunarWardDatabase.FileName));
        new GachaLogStore(source).Merge("genshin", [Record(1, "A")], "zh-cn");

        var target = temp.Combine("out.db");
        File.WriteAllText(target, "stale");
        source.BackupTo(target);

        var restored = new GachaLogStore(new LunarWardDatabase(target));
        Assert.Single(restored.GetAll("genshin"));
        Assert.False(File.Exists(target + ".tmp"));
    }
}
