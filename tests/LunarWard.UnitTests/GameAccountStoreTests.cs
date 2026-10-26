using LunarWard.Wine;
using LunarWard.Launch.Database;
using LunarWard.Platform;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 已保存账号从 <c>accounts.json</c> 迁到上游同款 <c>GameAccount</c> 表
/// （<c>Database/GameAccountStore.cs</c>，列照抄 Starward <c>DatabaseService</c>）。
/// </summary>
public sealed class GameAccountStoreTests : IDisposable
{
    private readonly string _dir = Path.Join(Path.GetTempPath(), "lunarward-acct-" + Guid.NewGuid().ToString("N"));

    private readonly GameAccountStore _store;


    public GameAccountStoreTests()
    {
        Directory.CreateDirectory(_dir);
        _store = new GameAccountStore(new LunarWardDatabase(Path.Join(_dir, LunarWardDatabase.FileName)));
    }


    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (IOException)
        {
        }
    }


    private static GameAccountSnapshot Snapshot(string biz, string name, long uid, params byte[] value)
        => new()
        {
            Sha256 = GameAccountSnapshot.ComputeSha256(value),
            GameBiz = biz,
            Uid = uid,
            Name = name,
            Value = value,
            Time = new DateTime(2026, 10, 25, 12, 0, 0),
        };


    [Fact]
    public void SaveAndGet_RoundTripsBlob()
    {
        var account = Snapshot("hk4e_cn", "主号", 100000001, 0x47, 0x4B, 0x30, 0x00, 0xFF);

        _store.Save(account);

        GameAccountSnapshot? loaded = _store.Get(account.Sha256);
        Assert.NotNull(loaded);
        Assert.Equal("hk4e_cn", loaded!.GameBiz);
        Assert.Equal(100000001, loaded.Uid);
        Assert.Equal("主号", loaded.Name);
        Assert.Equal(account.Value, loaded.Value);
        Assert.Equal(account.Time, loaded.Time);

        // 与上游一致：文件权限收紧到 0600
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite,
            File.GetUnixFileMode(Path.Join(_dir, LunarWardDatabase.FileName)));
    }


    [Fact]
    public void SaveTwice_SameSha_Replaces()
    {
        var first = Snapshot("hk4e_cn", "旧名字", 1, 1, 2, 3);
        var second = Snapshot("hk4e_cn", "新名字", 1, 1, 2, 3);

        _store.Save(first);
        _store.Save(second);

        Assert.Single(_store.GetAll());
        Assert.Equal("新名字", _store.Get(first.Sha256)!.Name);
    }


    [Fact]
    public void GetAll_FiltersByGameBiz()
    {
        _store.Save(Snapshot("hk4e_cn", "原神国服", 1, 1));
        _store.Save(Snapshot("hk4e_global", "原神国际", 2, 2));
        _store.Save(Snapshot("nap_cn", "绝区零", 3, 3));

        Assert.Equal(3, _store.GetAll().Count);
        Assert.Single(_store.GetAll("hk4e_cn"));
        Assert.Single(_store.GetAll("nap_cn"));
    }


    [Fact]
    public void Rename_OnlyTouchesName()
    {
        var account = Snapshot("hkrpg_cn", "旧", 4, 9, 9);
        _store.Save(account);

        _store.Rename(account.Sha256, "新");

        GameAccountSnapshot loaded = _store.Get(account.Sha256)!;
        Assert.Equal("新", loaded.Name);
        Assert.Equal(account.Value, loaded.Value);
    }


    [Fact]
    public void Delete_RemovesOnlyThatRow()
    {
        var keep = Snapshot("hk4e_cn", "保留", 1, 1);
        var drop = Snapshot("hk4e_cn", "删除", 2, 2);
        _store.Save(keep);
        _store.Save(drop);

        _store.Delete(drop.Sha256);

        Assert.Single(_store.GetAll());
        Assert.Null(_store.Get(drop.Sha256));
    }

    [Fact]
    public void MigrateLegacyJson_ImportsRowsAndRenamesFile()
    {
        string json = Path.Join(_dir, "accounts.json");
        File.WriteAllText(json, """
            [
              {
                "sha256": "ABC",
                "gameBiz": "hk4e_cn",
                "uid": 100000001,
                "name": "旧存档",
                "value": "AQID",
                "time": "2026-10-24T20:00:00+08:00"
              }
            ]
            """);

        Assert.True(_store.MigrateLegacyJson(_dir));

        GameAccountSnapshot loaded = _store.Get("ABC")!;
        Assert.Equal("旧存档", loaded.Name);
        Assert.Equal(100000001, loaded.Uid);
        Assert.Equal(new byte[] { 1, 2, 3 }, loaded.Value);
        Assert.False(File.Exists(json));
        Assert.True(File.Exists(json + ".migrated"));
    }


    [Fact]
    public void MigrateLegacyJson_NoFile_ReturnsFalse()
    {
        Assert.False(_store.MigrateLegacyJson(_dir));
    }
}
