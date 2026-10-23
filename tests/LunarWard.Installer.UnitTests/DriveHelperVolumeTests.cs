using System.IO;
using LunarWard.Installer;
using Xunit;

namespace LunarWard.Installer.UnitTests;

/// <summary>
/// <see cref="DriveHelper"/> 的卷探测。
///
/// <para>这些测试的核心方法：<b>把 <see cref="DriveInfo"/> 直连同一路径的结果当作基准</b>，
/// 而不是断言某个具体的文件系统名。否则换一台机器（ext4 / xfs / zfs）测试就会假失败。</para>
///
/// <para>回归的原始 bug：<c>Path.GetPathRoot</c> 在 Unix 上恒定返回 <c>/</c>，
/// 于是所有查询都落在根卷上 —— <c>/tmp</c>（tmpfs）会被报成根卷的 <c>btrfs</c>。</para>
/// </summary>
public class DriveHelperVolumeTests
{
    /// <summary>本机确实存在、且大概率位于不同挂载点的一组路径。</summary>
    private static IEnumerable<string> CandidatePaths()
    {
        yield return "/";
        yield return "/tmp";
        yield return "/home";
        yield return "/var";
        yield return "/boot";

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home))
        {
            yield return home;
        }
    }


    /// <summary>
    /// <see cref="DriveHelper.GetDriveFormat"/> 必须与 <see cref="DriveInfo"/> 直连的结果一致。
    /// 这是原 bug 的直接回归断言：修复前 <c>/tmp</c> 会返回根卷的 btrfs 而不是 tmpfs。
    /// </summary>
    [Fact]
    public void GetDriveFormat_MatchesDriveInfoDirectly()
    {
        var mismatches = new List<string>();

        foreach (var path in CandidatePaths().Where(Directory.Exists))
        {
            string direct;
            try
            {
                direct = new DriveInfo(path).DriveFormat;
            }
            catch (Exception)
            {
                // 该路径无法查询（权限/卷未就绪），DriveHelper 返回 null 也是可接受的
                continue;
            }

            var actual = DriveHelper.GetDriveFormat(path);
            if (!string.Equals(direct, actual, StringComparison.Ordinal))
            {
                mismatches.Add($"{path}: expected '{direct}', got '{actual}'");
            }
        }

        Assert.Empty(mismatches);
    }


    /// <summary>可用空间必须与 <see cref="DriveInfo.AvailableFreeSpace"/> 一致。</summary>
    [Fact]
    public void GetDriveAvailableSpace_MatchesDriveInfoDirectly()
    {
        foreach (var path in CandidatePaths().Where(Directory.Exists))
        {
            long direct;
            try
            {
                direct = new DriveInfo(path).AvailableFreeSpace;
            }
            catch (Exception)
            {
                continue;
            }

            Assert.Equal(direct, DriveHelper.GetDriveAvailableSpace(path));
        }
    }


    /// <summary>总容量必须与 <see cref="DriveInfo.TotalSize"/> 一致。</summary>
    [Fact]
    public void GetDriveTotalSpace_MatchesDriveInfoDirectly()
    {
        foreach (var path in CandidatePaths().Where(Directory.Exists))
        {
            long direct;
            try
            {
                direct = new DriveInfo(path).TotalSize;
            }
            catch (Exception)
            {
                continue;
            }

            Assert.Equal(direct, DriveHelper.GetDriveTotalSpace(path));
        }
    }


    /// <summary>
    /// <see cref="DriveHelper.GetPathRoot"/> 不能再恒定返回 <c>/</c>：
    /// 对 <c>/tmp</c> 这类独立挂载点，它必须返回该挂载点本身。
    /// </summary>
    [Fact]
    public void GetPathRoot_ReturnsRealMountPoint_NotAlwaysSlash()
    {
        if (!Directory.Exists("/tmp"))
        {
            return;
        }

        var rootOfTmp = DriveHelper.GetPathRoot("/tmp");
        Assert.NotNull(rootOfTmp);
        Assert.EndsWith("/", rootOfTmp);

        // 只有当 /tmp 确实是独立挂载点时，才断言它不是 "/"
        var tmpFormat = new DriveInfo("/tmp").DriveFormat;
        var rootFormat = new DriveInfo("/").DriveFormat;
        if (!string.Equals(tmpFormat, rootFormat, StringComparison.Ordinal))
        {
            Assert.NotEqual("/", rootOfTmp);
            Assert.Equal("/tmp/", rootOfTmp);
        }
    }


    /// <summary>
    /// 目标目录尚不存在时（安装前的空间预检就是这种场景），
    /// 必须回退到最近的已存在祖先，而不是抛 <see cref="DriveNotFoundException"/>。
    /// </summary>
    [Fact]
    public void GetDriveAvailableSpace_NonexistentPath_FallsBackToExistingAncestor()
    {
        var parent = Path.Combine(Path.GetTempPath(), "lunarward-nonexistent-probe-" + Guid.NewGuid().ToString("N"));
        var missing = Path.Combine(parent, "a", "b", "c");

        long expected;
        try
        {
            expected = new DriveInfo(Path.GetTempPath()).AvailableFreeSpace;
        }
        catch (Exception)
        {
            return;
        }

        Assert.Equal(expected, DriveHelper.GetDriveAvailableSpace(missing));
        Assert.Equal(new DriveInfo(Path.GetTempPath()).DriveFormat, DriveHelper.GetDriveFormat(missing));
    }


    /// <summary>完全无效的路径应当抛 <see cref="DriveNotFoundException"/>（对齐上游行为）。</summary>
    [Fact]
    public void GetDriveAvailableSpace_InvalidPath_ThrowsDriveNotFound()
    {
        Assert.Throws<DriveNotFoundException>(() => DriveHelper.GetDriveAvailableSpace(""));
        Assert.Throws<DriveNotFoundException>(() => DriveHelper.GetDriveAvailableSpace("   "));
    }


    /// <summary><see cref="DriveHelper.GetDriveType"/> 对空路径返回 <see cref="DriveType.NoRootDirectory"/>。</summary>
    [Fact]
    public void GetDriveType_EmptyPath_IsNoRootDirectory()
    {
        Assert.Equal(DriveType.NoRootDirectory, DriveHelper.GetDriveType(""));
        Assert.Equal(DriveType.NoRootDirectory, DriveHelper.GetDriveType("  "));
    }
}
