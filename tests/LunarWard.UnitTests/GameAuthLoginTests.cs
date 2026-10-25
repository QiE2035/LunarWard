using LunarWard.App.ViewModels.Starward;
using LunarWard.Core;
using LunarWard.Core.Account;
using LunarWard.Launch;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 通行证授权登录（Starward GameAuthLoginButton）：cookie 里的 stoken/mid 解析、
/// 客户端参数校验、授权登录开关的配置持久化。
/// </summary>
public class GameAuthLoginTests
{

    [Fact]
    public void ParseCookie_PicksStokenAndMid_RegardlessOfOrder()
    {
        const string cookie = "ltuid_v2=123456; mid=abcDEF123; stoken=v2_stoken_value; cookie_token_v2=zzz";

        var (stoken, mid) = GameAuthLoginViewModel.ParseCookie(cookie);

        Assert.Equal("v2_stoken_value", stoken);
        Assert.Equal("abcDEF123", mid);
    }


    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ltuid_v2=123456; cookie_token_v2=zzz")]
    [InlineData("stoken=; mid=;")]
    public void ParseCookie_ReturnsNulls_WhenMissing(string? cookie)
    {
        var (stoken, mid) = GameAuthLoginViewModel.ParseCookie(cookie);

        Assert.Null(stoken);
        Assert.Null(mid);
    }


    [Fact]
    public async Task Client_Throws_WhenStokenOrMidBlank()
    {
        var client = new GameAuthLoginClient();

        await Assert.ThrowsAsync<miHoYoApiException>(() => client.GetHyperionAidAsync("", "mid"));
        await Assert.ThrowsAsync<miHoYoApiException>(() => client.GetHyperionAidAsync("stoken", " "));
    }


    [Fact]
    public void LauncherConfig_KeepsLoginAuthTicketSwitch()
    {
        var path = Path.Join(Path.GetTempPath(), $"lw-auth-{Guid.NewGuid():N}.json");
        try
        {
            var config = new LauncherConfig();
            config.Launcher.EnableLoginAuthTicket = true;
            config.Save(path);

            var loaded = LauncherConfig.Load(path, out var warning);

            Assert.Null(warning);
            Assert.True(loaded.Launcher.EnableLoginAuthTicket);

            loaded.Launcher.EnableLoginAuthTicket = null;
            loaded.Save(path);
            Assert.Null(LauncherConfig.Load(path, out _).Launcher.EnableLoginAuthTicket);
        }
        finally
        {
            File.Delete(path);
        }
    }

}
