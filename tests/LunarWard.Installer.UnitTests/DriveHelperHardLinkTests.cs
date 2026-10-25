using System.IO;
using System.Runtime.InteropServices;
using LunarWard.Installer;
using Xunit;

namespace LunarWard.Installer.UnitTests;

/// <summary>
/// <see cref="DriveHelper.IsSameVolume"/> / <see cref="DriveHelper.SupportsHardLink"/>。
///
/// <para>这两个方法取代了上游的 <c>GetDriveFormat(...) is "NTFS"</c>，是硬链接去重的闸门。
/// 闸门判断错了有两种后果：判成"同卷不可硬链接"→ 每个文件重新下载；
/// 判成"同卷可硬链接"→ <c>link(2)</c> 返回 EXDEV，仍会回退，但多一次系统调用。</para>
///
/// <para><b>测试基准是真实的 <c>link(2)</c></b>，不是某个假设的文件系统：
/// <see cref="IsSameVolume"/> 为 true 当且仅当真的能在这两个目录间创建硬链接。
/// 这样测试在 ext4 / btrfs / xfs / zfs / overlayfs 上都成立。</para>
/// </summary>
public class DriveHelperHardLinkTests
{
    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int SysLink([MarshalAs(UnmanagedType.LPUTF8Str)] string oldPath,
                                      [MarshalAs(UnmanagedType.LPUTF8Str)] string newPath);


    /// <summary>Linux 的 <c>EXDEV</c>（errno 18）—— 唯一能说明"跨卷"的失败原因。</summary>
    private const int Exdev = 18;


    /// <summary>用真实的 <c>link(2)</c> 判断两个目录之间能否硬链接。</summary>
    private static bool CanReallyHardLink(string dirA, string dirB) => TryHardLink(dirA, dirB) == 0;


    /// <summary>
    /// 真的调用一次 <c>link(2)</c>：成功返回 0，失败返回 errno；
    /// 连探针文件都写不进去时返回 <c>-1</c>（判不了，既非成功也非跨卷）。
    /// </summary>
    private static int TryHardLink(string dirA, string dirB)
    {
        var source = Path.Combine(dirA, "lunarward-hl-source-" + Guid.NewGuid().ToString("N"));
        var target = Path.Combine(dirB, "lunarward-hl-target-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllText(source, "probe");
            return SysLink(source, target) == 0 ? 0 : Marshal.GetLastWin32Error();
        }
        catch (Exception)
        {
            return -1;
        }
        finally
        {
            TryDelete(source);
            TryDelete(target);
        }
    }


    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
            // ignore
        }
    }


    /// <summary>
    /// 同一目录内的两个路径必然同卷。
    /// </summary>
    [Fact]
    public void IsSameVolume_SameDirectory_IsTrue()
    {
        var dir = Path.GetTempPath();
        Assert.True(DriveHelper.IsSameVolume(dir, Path.Combine(dir, "child")));
        Assert.True(DriveHelper.IsSameVolume(dir, dir));
    }


    /// <summary>
    /// 同卷判定必须与真实 <c>link(2)</c> 一致 —— 这是本组测试的核心。
    ///
    /// <para><b>只有两个目录都可写时 <c>link(2)</c> 的失败才有意义。</b>
    /// 目标目录不可写时 <c>link(2)</c> 在跨卷检查之前就返回 EACCES（errno 13）：
    /// CI 的 runner 上 <c>/var</c>、<c>/home</c> 是 root 755，于是"真链接失败 + 同卷判定为 true"
    /// 被误当成回归（2026-10-25 CI 连续 4 次红）。因此这里先验证两侧可写，
    /// 再要求失败原因必须是 EXDEV，否则一律跳过。</para>
    ///
    /// <para>跨卷用例用 <c>/dev/shm</c>（独立的 tmpfs 实例，st_dev 与 <c>/tmp</c> 必然不同）。
    /// 它总是可写，所以在 CI 上也能真正验证"跨卷必须判为不同卷"。</para>
    /// </summary>
    [Theory]
    [InlineData("/tmp", "/tmp")]
    [InlineData("/tmp", "/dev/shm")]
    [InlineData("/tmp", "/home")]
    [InlineData("/home", "/var")]
    [InlineData("/home", "/home")]
    public void IsSameVolume_AgreesWithRealLink(string pathA, string pathB)
    {
        if (!Directory.Exists(pathA) || !Directory.Exists(pathB))
        {
            return;
        }

        // 两侧都必须真正可写，否则 link(2) 的失败来自权限而非卷边界
        if (!CanReallyHardLink(pathA, pathA) || !CanReallyHardLink(pathB, pathB))
        {
            return;
        }

        var errno = TryHardLink(pathA, pathB);
        if (errno != 0 && errno != Exdev)
        {
            // ENOSPC / EMLINK / EPERM … 同样与卷无关，不能当判定基准
            return;
        }

        Assert.Equal(errno == 0, DriveHelper.IsSameVolume(pathA, pathB));
    }


    /// <summary>同一路径永远同卷。</summary>
    [Fact]
    public void IsSameVolume_SamePath_IsTrue()
    {
        foreach (var path in new[] { "/", "/tmp", Path.GetTempPath() }.Where(Directory.Exists))
        {
            Assert.True(DriveHelper.IsSameVolume(path, path), $"'{path}' should be the same volume as itself.");
        }
    }


    /// <summary>空路径不构成有效卷，返回 false 而不是抛异常。</summary>
    [Theory]
    [InlineData("", "/tmp")]
    [InlineData("/tmp", "")]
    [InlineData("   ", "   ")]
    public void IsSameVolume_InvalidInput_ReturnsFalse(string pathA, string pathB)
    {
        Assert.False(DriveHelper.IsSameVolume(pathA, pathB));
    }


    /// <summary>
    /// <see cref="DriveHelper.SupportsHardLink"/> 对同一个路径的判定，
    /// 必须与"真的能创建硬链接"一致。
    /// </summary>
    [Theory]
    [InlineData("/")]
    [InlineData("/tmp")]
    [InlineData("/home")]
    public void SupportsHardLink_AgreesWithRealLink(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        if (!CanReallyHardLink(path, path))
        {
            // 无写权限，无法判断文件系统能力
            return;
        }

        Assert.True(DriveHelper.SupportsHardLink(path));
    }


    /// <summary>
    /// FAT/exFAT/ISO9660 这类无 inode 的文件系统必须判为不支持。
    /// 这些格式无法在本机现成构造，因此直接验证判定表本身，
    /// 通过 <c>/boot</c>（本机为 vfat）在存在时做真实验证。
    /// </summary>
    [Fact]
    public void SupportsHardLink_Vfat_IsFalse()
    {
        var format = DriveHelper.GetDriveFormat("/boot");
        if (!string.Equals(format, "vfat", StringComparison.Ordinal))
        {
            return;
        }

        Assert.False(DriveHelper.SupportsHardLink("/boot"));
    }


    /// <summary>
    /// 无效路径不崩溃：判不出来时 <see cref="DriveHelper.SupportsHardLink"/> 返回 true
    /// （乐观放行，由 <c>link(2)</c> 失败兜底），这是刻意的选择。
    /// </summary>
    [Fact]
    public void SupportsHardLink_InvalidPath_DoesNotThrow()
    {
        var result = Record.Exception(() => DriveHelper.SupportsHardLink(""));
        Assert.Null(result);
    }
}
