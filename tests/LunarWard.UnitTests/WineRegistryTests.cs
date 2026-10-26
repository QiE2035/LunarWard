using LunarWard.Services;
using LunarWard.Platform;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 游戏设置页的注册表读写（对应 Starward <c>GameSettingService</c>）。
/// 只测纯函数与键名映射；真正的 <c>wine reg</c> 往返由 <c>--page game-setting</c> 自检覆盖。
/// </summary>
public class WineRegistryTests
{
    [Fact]
    public void ParseDword_ReadsHexValue()
    {
        const string output = """
            HKEY_CURRENT_USER\Software\miHoYo\原神
                Screenmanager_Resolution_Width_h182942802    REG_DWORD    0x780
            """;

        Assert.Equal(1920, WineRegistry.ParseDword(output, "Screenmanager_Resolution_Width_h182942802"));
    }


    [Fact]
    public void ParseDword_ReadsDecimalValue()
    {
        const string output = "    Flag    REG_DWORD    3\n";

        Assert.Equal(3, WineRegistry.ParseDword(output, "Flag"));
    }


    [Fact]
    public void ParseDword_IgnoresOtherValuesAndTypes()
    {
        const string output = """
                Other    REG_DWORD    0x10
                Flag     REG_BINARY    0102
            """;

        Assert.Null(WineRegistry.ParseDword(output, "Flag"));
        Assert.Null(WineRegistry.ParseDword(output, "Missing"));
    }


    [Fact]
    public void ParseBinary_ReadsHexBytes()
    {
        const string output = """
            HKEY_CURRENT_USER\Software\miHoYo\原神
                GENERAL_DATA_h2389025596    REG_BINARY    7B7D00
            """;

        var data = WineRegistry.ParseBinary(output, "GENERAL_DATA_h2389025596");

        Assert.Equal([0x7B, 0x7D, 0x00], data);
    }


    [Fact]
    public void ParseBinary_ReturnsNullForOddOrInvalidHex()
    {
        Assert.Null(WineRegistry.ParseBinary("    X    REG_BINARY    ABC\n", "X"));
        Assert.Null(WineRegistry.ParseBinary("    X    REG_BINARY    ZZZZ\n", "X"));
    }


    [Fact]
    public void Utf8Z_RoundTripsWithTrailingNul()
    {
        var data = WineRegistry.EncodeUtf8Z("""{"deviceVoiceLanguageType":2}""");

        Assert.Equal(0, data[^1]);
        Assert.Equal("""{"deviceVoiceLanguageType":2}""", WineRegistry.DecodeUtf8Z(data));
    }


    [Theory]
    [InlineData("genshin", "China", @"HKEY_CURRENT_USER\Software\miHoYo\原神")]
    [InlineData("genshin", "Global", @"HKEY_CURRENT_USER\Software\miHoYo\Genshin Impact")]
    [InlineData("zzz", "China", @"HKEY_CURRENT_USER\Software\miHoYo\绝区零")]
    [InlineData("zzz", "Global", @"HKEY_CURRENT_USER\Software\miHoYo\ZenlessZoneZero")]
    public void RegistryKeyFor_MatchesStarward(string game, string edition, string expected)
    {
        Assert.Equal(expected, GameSettingRegistry.RegistryKeyFor(game, edition));
    }


    [Fact]
    public void RegistryKeyFor_UnsupportedGameReturnsNull()
    {
        Assert.Null(GameSettingRegistry.RegistryKeyFor("hkrpg", "China"));
    }


    [Fact]
    public void SupportsVoiceLanguage_OnlyGenshin()
    {
        Assert.True(GameSettingRegistry.SupportsVoiceLanguage("genshin"));
        Assert.False(GameSettingRegistry.SupportsVoiceLanguage("zzz"));
    }


    [Fact]
    public void FormatResolution_UsesMultiplicationSign()
    {
        Assert.Equal("1920 × 1080", GameSettingRegistry.FormatResolution(1920, 1080));
    }
}

/// <summary>
/// 真 wine 的注册表往返：默认跳过（要 wineboot 建 prefix），
/// 设 <c>LUNARWARD_REAL_WINE=1</c> + <c>LUNARWARD_TEST_WINE=&lt;wine 路径&gt;</c> 启用。
/// </summary>
public class WineRegistryRealWineTests
{
    private static bool Enabled =>
        Environment.GetEnvironmentVariable("LUNARWARD_REAL_WINE") is "1" or "true" or "yes";


    [Fact]
    public async Task WriteThenRead_Dword_RoundTrips()
    {
        var wine = Environment.GetEnvironmentVariable("LUNARWARD_TEST_WINE");
        if (!Enabled || string.IsNullOrEmpty(wine) || !File.Exists(wine))
        {
            return;
        }

        using var temp = new TempDirectory();
        var paths = new XdgPaths(temp.Combine("c"), temp.Combine("d"), temp.Combine("k"));
        using var log = new LunarWard.Services.LogService(paths);
        var registry = new WineRegistry(UnixProcessLauncher.Default, log);
        var prefix = temp.Combine("prefix");
        Directory.CreateDirectory(prefix);

        // 初始化 prefix（~8s）
        var env = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["WINEPREFIX"] = prefix,
            ["WINEDEBUG"] = "-all",
            ["WINEDLLOVERRIDES"] = "mscoree=;mshtml=",
            ["HOME"] = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ["PATH"] = Environment.GetEnvironmentVariable("PATH") ?? "/usr/bin:/bin",
        };
        using (var boot = UnixProcessLauncher.Default.Start(new ProcessRequest
        {
            FileName = wine,
            Arguments = ["wineboot", "-u"],
            WorkingDirectory = prefix,
            Environment = env,
            RedirectOutput = true,
            RedirectError = true,
            NewSession = false,
        }))
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            await boot.WaitForExitAsync(cts.Token);
        }

        var key = LunarWard.Services.GameSettingRegistry.RegistryKeyFor("genshin", "China")!;
        Assert.True(await registry.WriteDwordAsync(wine, prefix, key, "LunarWardTestFlag", 1920));
        Assert.Equal(1920, await registry.ReadDwordAsync(wine, prefix, key, "LunarWardTestFlag"));
    }

    [Fact]
    public void ParseString_ReadsRegSzValue()
    {
        const string output = """
            HKEY_CURRENT_USER\Software\miHoYo\HYP\1_1\hk4e_cn
                "GameInstallPath"    REG_SZ    C:\Program Files\Genshin Impact\Genshin Impact Game
            """;

        Assert.Equal(
            "C:\Program Files\Genshin Impact\Genshin Impact Game",
            WineRegistry.ParseString(output, "GameInstallPath"));
    }


    [Fact]
    public void ParseString_IsCaseInsensitiveAndIgnoresOtherTypes()
    {
        const string output = """
                "gameinstallpath"    REG_SZ    /mnt/games/hk4e
                Regular    REG_DWORD    0x1
            """;

        Assert.Equal("/mnt/games/hk4e", WineRegistry.ParseString(output, "GameInstallPath"));
        Assert.Null(WineRegistry.ParseString(output, "Regular"));
        Assert.Null(WineRegistry.ParseString(output, "Missing"));
    }


    [Fact]
    public void ParseString_ReturnsNullForEmptyValue()
    {
        // 本地化文案（"值未设置"/"value not set"）一律当空值处理，不抛、不返回文案。
        Assert.Null(WineRegistry.ParseString("""    "GameInstallPath"    REG_SZ    (值未设置)""", "GameInstallPath"));
        Assert.Null(WineRegistry.ParseString("""    "GameInstallPath"    REG_SZ""", "GameInstallPath"));
        Assert.Null(WineRegistry.ParseString("", "GameInstallPath"));
    }

}
