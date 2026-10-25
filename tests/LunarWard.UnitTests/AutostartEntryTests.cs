using LunarWard.Platform;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>XDG autostart（LunarWard 的 Linux 扩展，对应上游 Windows 注册表 Run 键）。</summary>
public sealed class AutostartEntryTests
{
    [Fact]
    public void Path_IsUnderConfigHomeAutostart()
    {
        string configHome = Path.Join(Path.GetTempPath(), "cfg-autostart-a");
        Assert.Equal(Path.Join(configHome, "autostart", "lunarward.desktop"), AutostartEntry.PathFor(configHome));
    }


    [Fact]
    public void BuildContent_UsesStartHiddenAndQuotesPathWithSpace()
    {
        string content = AutostartEntry.BuildContent("/opt/My Games/lunarward");

        Assert.Contains("Type=Application", content);
        Assert.Contains("--start-hidden", content);
        Assert.Contains("Exec=\"/opt/My Games/lunarward\" --start-hidden", content);
        Assert.DoesNotContain("Terminal=true", content);
    }


    [Fact]
    public void BuildContent_EscapesFieldCodesAndQuotes()
    {
        string content = AutostartEntry.BuildContent("/tmp/a b%c\"d");

        // % 是 Desktop Entry 字段码 → %%；双引号 → \"；含空格 → 整串用双引号包住
        Assert.Contains("\"/tmp/a b%%c\\\"d\"", content);
    }


    [Fact]
    public void SetEnabled_WritesAndDeletesEntry()
    {
        string configHome = Path.Join(Path.GetTempPath(), $"cfg-autostart-{Guid.NewGuid():N}");
        try
        {
            Assert.False(AutostartEntry.IsEnabled(configHome));

            Assert.True(AutostartEntry.SetEnabled(true, "/usr/bin/lunarward", configHome));
            Assert.True(AutostartEntry.IsEnabled(configHome));
            string path = AutostartEntry.PathFor(configHome);
            Assert.Contains("--start-hidden", File.ReadAllText(path));

            Assert.False(AutostartEntry.SetEnabled(false, "/usr/bin/lunarward", configHome));
            Assert.False(AutostartEntry.IsEnabled(configHome));
            Assert.False(File.Exists(path));
        }
        finally
        {
            string dir = Path.GetDirectoryName(AutostartEntry.PathFor(configHome))!;
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
