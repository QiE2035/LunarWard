using LunarWard.Launch;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>工具箱状态（设备指纹）与上游 AppConfig 的字段对齐。</summary>
public class ToolboxSettingsTests
{
    [Fact]
    public void Defaults_AreEmptyAndNull()
    {
        var toolbox = new ToolboxSettings();
        Assert.Equal("", toolbox.DeviceId);
        Assert.Equal("", toolbox.DeviceFp);
        Assert.Null(toolbox.DeviceFpLastUpdate);
    }


    [Fact]
    public void Config_RoundTripsToolboxSection()
    {
        var dir = Path.Join(Path.GetTempPath(), "lw-toolbox-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Join(dir, "config.json");
        try
        {
            var config = new LauncherConfig();
            config.Toolbox.DeviceId = "device-123";
            config.Toolbox.DeviceFp = "fp-456";
            config.Toolbox.DeviceFpLastUpdate = DateTimeOffset.Parse("2026-10-25T14:00:00+08:00");
            config.Save(file);

            var loaded = LauncherConfig.Load(file, out var warning);
            Assert.Null(warning);
            Assert.Equal("device-123", loaded.Toolbox.DeviceId);
            Assert.Equal("fp-456", loaded.Toolbox.DeviceFp);
            Assert.Equal(DateTimeOffset.Parse("2026-10-25T14:00:00+08:00"), loaded.Toolbox.DeviceFpLastUpdate);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }


    [Fact]
    public void Config_WithoutToolboxSection_UsesDefaults()
    {
        var dir = Path.Join(Path.GetTempPath(), "lw-toolbox-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Join(dir, "config.json");
        try
        {
            File.WriteAllText(file, """{"schema_version":1,"launcher":{"default_runtime":"x"},"runtimes":{},"profiles":{}}""");
            var loaded = LauncherConfig.Load(file, out _);
            Assert.Equal("", loaded.Toolbox.DeviceId);
            Assert.Equal("", loaded.Toolbox.DeviceFp);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
