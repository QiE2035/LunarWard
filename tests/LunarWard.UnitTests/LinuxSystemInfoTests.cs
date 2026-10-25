using LunarWard.Platform;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// <see cref="LinuxSystemInfo"/> 的读取逻辑：只读 /proc、/sys 的文本文件，
/// 缺失时退化为「未知」而不是抛异常。
/// </summary>
public sealed class LinuxSystemInfoTests
{
    [Fact]
    public void Read_CollectsKernelSessionAndGpu()
    {
        using var temp = new TempDirectory();
        string proc = Path.Join(temp.Path, "proc");
        string sys = Path.Join(temp.Path, "sys");
        Directory.CreateDirectory(Path.Join(proc, "sys", "kernel"));
        File.WriteAllText(Path.Join(proc, "sys", "kernel", "osrelease"), "6.12.4-1-cachyos\n");

        string device = Path.Join(sys, "class", "drm", "card0", "device");
        Directory.CreateDirectory(device);
        File.WriteAllText(Path.Join(device, "uevent"),
            "DRIVER=amdgpu\nPCI_CLASS=30000\nPCI_ID=1002:73EF\nPCI_SUBSYS_ID=1DA2:E443\n");

        LinuxSystemInfo info = LinuxSystemInfo.Read(proc, sys, name => name switch
        {
            "XDG_SESSION_TYPE" => "wayland",
            "XDG_CURRENT_DESKTOP" => "KDE",
            _ => null,
        });

        Assert.Equal("6.12.4-1-cachyos", info.Kernel);
        Assert.Equal("wayland · KDE", info.Session);
        Assert.Equal("amdgpu", info.GpuDriver);
        Assert.Equal("1002:73EF", info.GpuPciId);
    }


    [Fact]
    public void Read_FallsBackToUnknown()
    {
        using var temp = new TempDirectory();

        LinuxSystemInfo info = LinuxSystemInfo.Read(temp.Path, temp.Path, _ => null);

        Assert.Equal(LinuxSystemInfo.Unknown, info.Kernel);
        Assert.Equal(LinuxSystemInfo.Unknown, info.Session);
        Assert.Equal(LinuxSystemInfo.Unknown, info.GpuDriver);
        Assert.Equal(LinuxSystemInfo.Unknown, info.GpuPciId);
    }


    [Fact]
    public void Read_UsesSessionTypeOnly_WhenDesktopMissing()
    {
        using var temp = new TempDirectory();

        LinuxSystemInfo info = LinuxSystemInfo.Read(temp.Path, temp.Path, name =>
            name == "XDG_SESSION_TYPE" ? "x11" : null);

        Assert.Equal("x11", info.Session);
    }
}
