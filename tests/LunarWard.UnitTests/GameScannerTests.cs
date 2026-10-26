using LunarWard.Services;
using LunarWard.Games;
using LunarWard.Platform;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>游戏目录识别（不含 Steam 真实库，只测单根扫描）。</summary>
public sealed class GameScannerTests : IDisposable
{
    private readonly string _root;


    public GameScannerTests()
    {
        _root = Path.Join(Path.GetTempPath(), $"scan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }


    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // 忽略：临时目录清理失败不影响测试结论
        }
    }


    private GameScanner CreateScanner() => new(XdgPaths.Default);


    [Fact]
    public void ScanRoot_FindsSteamStyleLayout()
    {
        // Steam 的 steamapps/common 下是「Genshin Impact Game/YuanShen.exe」这种布局
        string game = Path.Join(_root, "Genshin Impact Game");
        Directory.CreateDirectory(Path.Join(game, "YuanShen_Data"));
        File.WriteAllText(Path.Join(game, "YuanShen.exe"), "");

        var found = CreateScanner().ScanRoot(_root);

        DetectedGame only = Assert.Single(found);
        Assert.Equal(GameCatalog.Genshin, only.Definition.Kind);
        Assert.Equal(GameEdition.China, only.Definition.Edition);
        Assert.Equal(game, only.Directory);
    }


    [Fact]
    public void ScanRoot_FindsNestedAndIgnoresOtherFolders()
    {
        string games = Path.Join(_root, "Games");
        string zenless = Path.Join(games, "ZenlessZoneZero");
        Directory.CreateDirectory(zenless);
        File.WriteAllText(Path.Join(zenless, "ZenlessZoneZero.exe"), "");
        Directory.CreateDirectory(Path.Join(_root, "SomeRandomTool"));

        var found = CreateScanner().ScanRoot(_root);

        DetectedGame only = Assert.Single(found);
        Assert.Equal(GameCatalog.Zzz, only.Definition.Kind);
    }


    [Fact]
    public void ScanRoot_EmptyDirectoryReturnsNothing()
    {
        Assert.Empty(CreateScanner().ScanRoot(_root));
    }


    [Fact]
    public void SearchRoots_IncludesSteamCommonDirectories()
    {
        var scanner = CreateScanner();

        Assert.Contains(
            scanner.SearchRoots,
            root => root.EndsWith(Path.Join("steamapps", "common"), StringComparison.Ordinal)
                    && root.Contains(".steam", StringComparison.Ordinal));
    }
}
