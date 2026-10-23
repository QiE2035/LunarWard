using LunarWard.Core.Gacha;
using LunarWard.Core;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 移植 Starward.Core 时引入的平台相关缺陷回归测试。
/// </summary>
/// <remarks>
/// 上游在 Windows 上把 <c>YuanShen_Data\webCaches\...</c> 当路径常量用，
/// <c>Path.Join</c> 在 Windows 上认反斜杠。Linux 的 <c>Path.Join</c> 只认 <c>/</c>，
/// 反斜杠会变成**文件名的一部分**，于是"从网页缓存里找抽卡链接"永远失败。
/// 这类 bug 不会编译报错，也不会有异常——只会静默找不到文件。
/// </remarks>
public class GachaCachePathTests
{
    [Theory]
    [InlineData(GameBiz.hk4e_cn)]
    [InlineData(GameBiz.hk4e_bilibili)]
    [InlineData(GameBiz.hk4e_global)]
    [InlineData(GameBiz.hkrpg_cn)]
    [InlineData(GameBiz.nap_cn)]
    [InlineData(GameBiz.nap_global)]
    public void CachePaths_HaveNoBackslashes(string gameBiz)
    {
        var biz = new LunarWard.Core.GameBiz(gameBiz);
        var webCaches = GachaLogClient.GetWebCachesFolderPath(biz, "/games/x");
        var cacheFile = GachaLogClient.GetGachaCacheFilePath(biz, "/games/x");

        Assert.DoesNotContain('\\', webCaches);
        Assert.DoesNotContain('\\', cacheFile);
    }


    [Fact]
    public void CachePath_IsUnderGameDirectory()
    {
        var biz = new LunarWard.Core.GameBiz(GameBiz.hk4e_cn);
        var path = GachaLogClient.GetGachaCacheFilePath(biz, "/games/hk4e_cn");

        Assert.StartsWith("/games/hk4e_cn/", path, StringComparison.Ordinal);
        Assert.EndsWith("/data_2", path, StringComparison.Ordinal);
        Assert.Contains("/YuanShen_Data/webCaches/", path, StringComparison.Ordinal);
    }


    [Fact]
    public void GetGachaUrlFromWebCache_FindsTheUrlInARealisticLayout()
    {
        using var temp = new TempDirectory();
        var game = temp.Combine("hk4e_cn");
        var cacheDir = Path.Join(game, "YuanShen_Data", "webCaches", "Cache", "Cache_Data");
        Directory.CreateDirectory(cacheDir);

        const string marker = "https://webstatic.mihoyo.com/hk4e/event/e20190909gacha/index.html?authkey=abc123";
        File.WriteAllBytes(Path.Join(cacheDir, "data_2"),
            System.Text.Encoding.UTF8.GetBytes("garbage\0" + marker + "\0more"));

        var biz = new LunarWard.Core.GameBiz(GameBiz.hk4e_cn);
        var url = GachaLogClient.GetGachaUrlFromWebCache(biz, game);

        Assert.NotNull(url);
        Assert.Contains("authkey=abc123", url);
    }


    [Fact]
    public void GetWebCachesFolderPath_DoesNotThrowForAllKnownGames()
    {
        foreach (var biz in new[]
                 {
                     GameBiz.hk4e_cn, GameBiz.hk4e_global, GameBiz.hkrpg_cn,
                     GameBiz.hkrpg_global, GameBiz.nap_cn, GameBiz.nap_global,
                 })
        {
            var path = GachaLogClient.GetWebCachesFolderPath(new LunarWard.Core.GameBiz(biz), "/g");
            Assert.False(string.IsNullOrEmpty(path));
        }
    }


    [Fact]
    public void InstallPathProvider_IsTheOnlyWindowsHook()
    {
        // 默认应为 null（Linux 下没有注册表实现被注入）
        Assert.Null(GachaLogClient.InstallPathProvider);

        try
        {
            GachaLogClient.InstallPathProvider = new FakeInstallPathProvider();
            Assert.NotNull(GachaLogClient.InstallPathProvider);
        }
        finally
        {
            GachaLogClient.InstallPathProvider = null;
        }
    }


    private sealed class FakeInstallPathProvider : IGameInstallPathProvider
    {
        public string? GetInstallPath(LunarWard.Core.GameBiz gameBiz) => null;

        public string? GetLauncherPath(string registryKey) => null;
    }
}


/// <summary>Linux 侧的注册表替代实现：从 Wine prefix 的 <c>user.reg</c> 读安装路径。</summary>
public class WineRegistryInstallPathProviderTests
{
    /// <summary>
    /// Wine 的 <c>user.reg</c> 里，值形如
    /// <c>"InstallPath"="C:\\Program Files\\Genshin Impact"</c>，且键名中的反斜杠被转义成 <c>\\\\</c>。
    /// </summary>
    private const string SampleUserReg = """
    WINE REGISTRY Version 2
    ;; All keys relative to \\User\\S-1-5-21-0-0-0-1000

    [Software\\\\miHoYo\\\\原神] 1600000000
    #time=1d9a2b3c4e5f6a7
    "InstallPath"="C:\\Program Files\\Genshin Impact"
    "GamePath"="C:\\Program Files\\Genshin Impact\\YuanShen.exe"
    """;


    [Fact]
    public void ReadsInstallPathFromUserReg()
    {
        var provider = new LunarWard.Games.WineRegistryInstallPathProvider(
            "/prefix", driveZRoot: "/home/u/.local/share/wine/drive_c");

        // 1) 解析出来的是原始 Windows 路径
        var raw = provider.ParseInstallPath(SampleUserReg, "Software\\miHoYo\\原神");
        Assert.NotNull(raw);
        Assert.Equal(@"C:\Program Files\Genshin Impact", raw);

        // 2) 翻译成 Linux 路径（C: → <prefix>/drive_c）
        var path = provider.ResolveFromContent(SampleUserReg, "Software\\miHoYo\\原神");
        Assert.NotNull(path);
        Assert.StartsWith("/prefix/drive_c/", path, StringComparison.Ordinal);
        Assert.DoesNotContain('\\', path);
        Assert.Contains("Genshin Impact", path);
    }


    [Theory]
    [InlineData(@"C:\Program Files\Genshin Impact", "/prefix/drive_c/Program Files/Genshin Impact")]
    [InlineData(@"D:\Games\ZZZ", "/prefix/drive_d/Games/ZZZ")]
    [InlineData(@"Z:\home\u\games", "/home/u/games")]
    [InlineData("/already/unix", "/already/unix")]
    [InlineData(@"relative\path", "/prefix/drive_c/relative/path")]
    public void TranslateToLinux_HandlesAllDriveForms(string windowsPath, string expected)
    {
        var provider = new LunarWard.Games.WineRegistryInstallPathProvider(
            "/prefix", driveZRoot: "/");
        Assert.Equal(expected, provider.TranslateToLinux(windowsPath));
    }


    [Fact]
    public void MissingKey_ReturnsNull()
    {
        var provider = new LunarWard.Games.WineRegistryInstallPathProvider("/prefix");
        Assert.Null(provider.ParseInstallPath(SampleUserReg, "Software\\miHoYo\\Star Rail"));
    }


    [Fact]
    public void EmptyOrGarbage_ReturnsNull()
    {
        var provider = new LunarWard.Games.WineRegistryInstallPathProvider("/prefix");
        Assert.Null(provider.ParseInstallPath("", "Software\\miHoYo\\原神"));
        Assert.Null(provider.ParseInstallPath("not a registry file", "Software\\miHoYo\\原神"));
    }
}
