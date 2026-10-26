using Dapper;
using LunarWard.Features.Database;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>文件管理页「备份数据库」：<see cref="DatabaseService.BackupDatabase"/> 的热备份语义。</summary>
public class DatabaseBackupTests
{
    private const string Key = "backup-test";


    [Fact]
    public void BackupDatabase_WritesSnapshotThatCanBeReopened()
    {
        using var temp = new TempDirectory();
        DatabaseService.SetDatabase(temp.Path);
        DatabaseService.SetValue(Key, "A");

        var backupPath = Path.Join(temp.Combine("backup"), "LunarWard_20261025.db");
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
        DatabaseService.BackupDatabase(backupPath);

        Assert.True(File.Exists(backupPath));
        Assert.Equal("A", ReadValue(backupPath));
    }


    [Fact]
    public void BackupDatabase_OverwritesPreviousBackup()
    {
        using var temp = new TempDirectory();
        var target = temp.Combine("out.db");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        DatabaseService.SetDatabase(temp.Path);
        DatabaseService.SetValue(Key, "old");
        DatabaseService.BackupDatabase(target);
        Assert.Equal("old", ReadValue(target));

        DatabaseService.SetValue(Key, "new");
        DatabaseService.BackupDatabase(target);
        Assert.Equal("new", ReadValue(target));
        Assert.False(File.Exists(target + ".tmp"));
    }


    private static string? ReadValue(string file)
    {
        SqliteConnection.ClearAllPools();
        using var con = new SqliteConnection($"DataSource={file};Pooling=False;");
        con.Open();
        return con.QueryFirstOrDefault<string>("SELECT Value FROM KVT WHERE Key = @key;", new { key = Key });
    }
}
