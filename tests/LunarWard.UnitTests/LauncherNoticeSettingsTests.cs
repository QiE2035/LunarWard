using LunarWard.Launch;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 启动器「游戏公告」相关开关（对应 Starward <c>AppConfig.ShowGameAnnouncement</c> /
/// <c>DisableRedDotReminderForGameNotices</c>）的默认值与往返持久化。
/// </summary>
public class LauncherNoticeSettingsTests
{

    [Fact]
    public void DefaultsShowNoticeAndEnableRedDot()
    {
        var settings = new LauncherSettings();

        Assert.True(settings.ShowGameNotice);
        Assert.False(settings.DisableNoticeRedDot);
    }


    [Fact]
    public void RoundTripKeepsNoticeToggles()
    {
        string path = Path.Join(Path.GetTempPath(), $"lw-notice-{Guid.NewGuid():N}.json");
        try
        {
            var config = new LauncherConfig();
            config.Launcher.ShowGameNotice = false;
            config.Launcher.DisableNoticeRedDot = true;
            config.Save(path);

            var loaded = LauncherConfig.Load(path, out string? warning);

            Assert.Null(warning);
            Assert.False(loaded.Launcher.ShowGameNotice);
            Assert.True(loaded.Launcher.DisableNoticeRedDot);
        }
        finally
        {
            File.Delete(path);
        }
    }


    [Fact]
    public void OldConfigWithoutFieldsFallsBackToDefaults()
    {
        string path = Path.Join(Path.GetTempPath(), $"lw-notice-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{"schema_version":1,"launcher":{"Language":"en-us"}}""");

            var loaded = LauncherConfig.Load(path, out string? warning);

            Assert.Null(warning);
            Assert.True(loaded.Launcher.ShowGameNotice);
            Assert.False(loaded.Launcher.DisableNoticeRedDot);
        }
        finally
        {
            File.Delete(path);
        }
    }

}
