using System.IO;

namespace LunarWard.Installer;

/// <summary>
/// 磁盘/卷信息。
///
/// <para>上游 <c>Starward.RPC.DriveHelper</c> 通过 <c>Vanara.PInvoke.Kernel32</c> 调用
/// <c>GetDriveType</c> / <c>GetDiskFreeSpaceEx</c> / <c>GetVolumeInformation</c>，
/// 并用 <c>IOCTL_STORAGE_QUERY_PROPERTY</c> 判断 USB。Linux 上这些 API 都不存在，
/// 这里改用 BCL 的 <see cref="DriveInfo"/> 加上少量 <c>procfs</c>/<c>sysfs</c> 读取。</para>
///
/// <para>映射关系：</para>
/// <list type="table">
/// <item><description><c>Kernel32.GetDriveType</c> → <see cref="GetDriveType"/>，
/// 内部用 <see cref="DriveInfo.DriveType"/>。Linux 的 .NET 实现由 <c>statfs(2)</c> 的
/// <c>f_type</c> 与挂载点推出：块设备 → <c>Fixed</c>，tmpfs/ramfs → <c>Ram</c>，
/// NFS/CIFS → <c>Network</c>，vfat/iso9660 等 → <c>Removable</c>。</description></item>
/// <item><description><c>Kernel32.GetDiskFreeSpaceEx</c> → <see cref="GetDriveAvailableSpace"/>，
/// 对应其实参 <c>freeBytesAvailable</c>（当前用户可用），而不是 <c>totalNumberOfFreeBytes</c>。</description></item>
/// <item><description><c>Kernel32.GetVolumeInformation</c> → <see cref="GetDriveFormat"/>，
/// 由 <c>statfs</c> 的 <c>f_type</c> 反查名字（<c>btrfs</c> / <c>ext4</c> / <c>ntfs</c> / <c>tmpfs</c> …）。</description></item>
/// </list>
///
/// <para><b>注意</b>：上游的 <c>is "NTFS"</c> 判断在 Linux 上不能直接照搬。
/// 上游用 NTFS 判断"能否硬链接"，而 Linux 上 ext4/btrfs/xfs 都支持硬链接，
/// 该判断的正确语义见 <see cref="SupportsHardLink"/>；同一卷判定见 <see cref="IsSameVolume"/>。</para>
/// </summary>
public static class DriveHelper
{


    /// <summary>路径所在卷的根；无法解析时返回 <c>null</c>。总是以 <c>/</c> 结尾，与上游补尾部斜杠的语义一致。</summary>
    public static string? GetPathRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return null;
        }

        var root = Path.GetPathRoot(full);
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }

        return root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
    }


    /// <summary>
    /// 驱动器类型。路径无有效根时返回 <see cref="DriveType.NoRootDirectory"/>
    /// （上游 <c>Kernel32.GetDriveType</c> 对非法路径返回 <c>DRIVE_NO_ROOT_DIR</c>）。
    /// </summary>
    public static DriveType GetDriveType(string path)
    {
        var root = GetPathRoot(path);
        if (string.IsNullOrWhiteSpace(root))
        {
            return DriveType.NoRootDirectory;
        }

        try
        {
            return new DriveInfo(root).DriveType;
        }
        catch (Exception)
        {
            return DriveType.Unknown;
        }
    }


    /// <summary>
    /// 卷上当前用户可用的空闲字节数。
    /// 路径无有效根或卷不可读时抛 <see cref="DriveNotFoundException"/>，与上游 <c>GetDiskFreeSpaceEx</c> 失败时的行为一致。
    /// </summary>
    public static long GetDriveAvailableSpace(string path)
    {
        var root = GetPathRoot(path);
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new DriveNotFoundException($"The path '{path}' does not have a valid root directory.");
        }

        try
        {
            return new DriveInfo(root).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is not DriveNotFoundException)
        {
            throw new DriveNotFoundException($"Failed to get disk free space information for path '{path}'.", ex);
        }
    }


    /// <summary>卷的总容量（字节）。</summary>
    public static long GetDriveTotalSpace(string path)
    {
        var root = GetPathRoot(path);
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new DriveNotFoundException($"The path '{path}' does not have a valid root directory.");
        }

        try
        {
            return new DriveInfo(root).TotalSize;
        }
        catch (Exception ex) when (ex is not DriveNotFoundException)
        {
            throw new DriveNotFoundException($"Failed to get total size for path '{path}'.", ex);
        }
    }


    /// <summary>
    /// 卷的文件系统名，小写（<c>btrfs</c> / <c>ext4</c> / <c>ntfs</c> / <c>vfat</c> …）。
    /// 无法确定时返回 <c>null</c>。
    /// </summary>
    public static string? GetDriveFormat(string path)
    {
        var root = GetPathRoot(path);
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }

        try
        {
            var format = new DriveInfo(root).DriveFormat;
            return string.IsNullOrWhiteSpace(format) ? null : format.ToLowerInvariant();
        }
        catch (Exception)
        {
            return null;
        }
    }


    /// <summary>
    /// 该卷是否支持硬链接（即 <c>link(2)</c> 可用）。
    ///
    /// <para>这取代了上游 <c>GetDriveFormat(...) is "NTFS"</c> 的判断。Linux 上
    /// ext2/3/4、btrfs、xfs、zfs、f2fs、tmpfs、overlayfs 都支持硬链接；
    /// FAT/exFAT/ISO9660/UDF/NFSv2 不支持。</para>
    /// </summary>
    public static bool SupportsHardLink(string path)
    {
        var format = GetDriveFormat(path);
        if (format is null)
        {
            // 判不出来就乐观地试一次，失败由调用方的 link(2) 返回值兜底
            return true;
        }

        return format switch
        {
            "vfat" or "msdos" or "msdosfs" or "fat" or "fat16" or "fat32" => false,
            "exfat" or "fuse.exfat" or "iso9660" or "udf" or "fuse.udf" => false,
            "nfs" or "nfs2" or "nfs3" => false,
            _ => true,
        };
    }


    /// <summary>
    /// 两个路径是否位于同一个卷上。
    ///
    /// <para>这是上游 <c>Path.GetPathRoot(a) == Path.GetPathRoot(b)</c> 在 Linux 上的正确版本：
    /// 在 Linux 上 <c>/home</c>、<c>/var</c> 常与 <c>/</c> 是不同的挂载点，但
    /// <c>Path.GetPathRoot</c> 全都返回 <c>/</c>，直接比较会误判为"同卷"，
    /// 从而导致跨卷硬链接失败。</para>
    /// </summary>
    public static bool IsSameVolume(string pathA, string pathB)
    {
        try
        {
            var a = Path.GetFullPath(pathA);
            var b = Path.GetFullPath(pathB);
            var devA = GetDeviceId(a);
            var devB = GetDeviceId(b);
            if (devA is not null && devB is not null)
            {
                return devA == devB;
            }

            return string.Equals(Path.GetPathRoot(a), Path.GetPathRoot(b), StringComparison.Ordinal);
        }
        catch (Exception)
        {
            return false;
        }
    }


    /// <summary>
    /// 路径是否位于可移动介质或 USB 存储上。
    ///
    /// <para>Linux 没有 Windows <c>IOCTL_STORAGE_QUERY_PROPERTY</c> 的单一等价物，分三层判断：</para>
    /// <list type="number">
    /// <item><description><see cref="DriveType.Removable"/> / <see cref="DriveType.CDRom"/> 直接为真；</description></item>
    /// <item><description>挂载源在 <c>/sys/block/&lt;dev&gt;/removable</c> 上标记为 1；</description></item>
    /// <item><description><c>/sys/block/&lt;dev&gt;</c> 的物理路径里含 <c>usb</c>（USB 大容量存储）。</description></item>
    /// </list>
    /// 任何一步出错都按"不可移动"处理，与上游 <c>catch { return false; }</c> 一致。
    /// </summary>
    public static bool IsDeviceRemovableOrOnUSB(string path)
    {
        try
        {
            if (!Directory.Exists(path) && !File.Exists(path))
            {
                return false;
            }

            var root = GetPathRoot(path);
            if (string.IsNullOrWhiteSpace(root))
            {
                return false;
            }

            var driveType = GetDriveType(root);
            if (driveType is DriveType.Removable or DriveType.CDRom)
            {
                return true;
            }

            if (driveType is not DriveType.Fixed)
            {
                return false;
            }

            var device = GetMountSource(root);
            var blockName = GetBlockDeviceName(device);
            if (blockName is null)
            {
                return false;
            }

            if (ReadSysfsFlag(Path.Join("/sys/block", blockName, "removable")) is true)
            {
                return true;
            }

            return IsOnUsbBus(blockName);
        }
        catch (Exception)
        {
            return false;
        }
    }




    // ———————————————————————————— 内部实现 ————————————————————————————


    /// <summary>
    /// 用 <c>stat(2)</c> 拿 <c>st_dev</c>，这是判断"同卷"最准确的方式。
    /// 这里通过 <see cref="DriveInfo.Name"/> 的挂载点等价类近似：
    /// 先找最深挂载点，再用挂载点的 major:minor 比较。
    /// </summary>
    private static string? GetDeviceId(string fullPath)
    {
        var mount = FindMountPoint(fullPath);
        return mount?.DeviceId;
    }


    /// <summary>在 <c>/proc/self/mountinfo</c> 中找覆盖该路径的最深挂载点。</summary>
    private static (string MountPoint, string DeviceId, string Source)? FindMountPoint(string fullPath)
    {
        const string mountInfo = "/proc/self/mountinfo";
        if (!File.Exists(mountInfo))
        {
            return null;
        }

        (string MountPoint, string DeviceId, string Source)? best = null;
        var bestLength = -1;

        foreach (var line in File.ReadLines(mountInfo))
        {
            // 格式：<id> <parent> <major>:<minor> <root> <mount_point> <options> ... - <fstype> <source> <superopts>
            var separator = line.IndexOf(" - ", StringComparison.Ordinal);
            if (separator < 0)
            {
                continue;
            }

            var left = line[..separator].Split(' ');
            var right = line[(separator + 3)..].Split(' ');
            if (left.Length < 5 || right.Length < 2)
            {
                continue;
            }

            var mountPoint = UnescapeMountInfo(left[4]);
            if (!IsAncestorOrSelf(mountPoint, fullPath))
            {
                continue;
            }

            if (mountPoint.Length > bestLength)
            {
                bestLength = mountPoint.Length;
                best = (mountPoint, left[2], right[1]);
            }
        }

        return best;
    }


    private static string? GetMountSource(string path)
    {
        var full = Path.GetFullPath(path);
        return FindMountPoint(full)?.Source;
    }


    private static bool IsAncestorOrSelf(string mountPoint, string fullPath)
    {
        if (string.Equals(fullPath, mountPoint, StringComparison.Ordinal))
        {
            return true;
        }

        var prefix = mountPoint.EndsWith('/') ? mountPoint : mountPoint + "/";
        return fullPath.StartsWith(prefix, StringComparison.Ordinal);
    }


    /// <summary><c>mountinfo</c> 把空格等字符写成 <c>\040</c> 形式的八进制转义。</summary>
    private static string UnescapeMountInfo(string value)
    {
        if (!value.Contains('\\'))
        {
            return value;
        }

        var builder = new System.Text.StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 3 < value.Length
                && IsOctal(value[i + 1]) && IsOctal(value[i + 2]) && IsOctal(value[i + 3]))
            {
                builder.Append((char)((value[i + 1] - '0') * 64 + (value[i + 2] - '0') * 8 + (value[i + 3] - '0')));
                i += 3;
            }
            else
            {
                builder.Append(value[i]);
            }
        }

        return builder.ToString();
    }


    private static bool IsOctal(char c) => c is >= '0' and <= '7';


    /// <summary>
    /// 从挂载源（<c>/dev/sdb1</c>、<c>/dev/mapper/x</c>、<c>UUID=…</c>、<c>overlay</c> 等）
    /// 推出 <c>/sys/block</c> 下的设备名；推不出（如 overlay / tmpfs / 网络）时返回 <c>null</c>。
    /// </summary>
    private static string? GetBlockDeviceName(string? source)
    {
        if (string.IsNullOrWhiteSpace(source) || !source.StartsWith("/dev/", StringComparison.Ordinal))
        {
            return null;
        }

        // device-mapper / md / drbd 等虚拟设备不是 USB 盘
        if (source.StartsWith("/dev/mapper/", StringComparison.Ordinal)
            || source.StartsWith("/dev/md", StringComparison.Ordinal)
            || source.StartsWith("/dev/dm-", StringComparison.Ordinal))
        {
            return null;
        }

        return ResolvePartitionName(Path.GetFileName(source));
    }


    private static string ResolvePartitionName(string name)
    {
        // nvme0n1p1 / mmcblk0p1 / loop0p1 / nbd0p1
        var p = name.LastIndexOf('p');
        if (p > 0 && p < name.Length - 1
            && name[(p + 1)..].All(char.IsAsciiDigit)
            && (name.StartsWith("nvme", StringComparison.Ordinal)
                || name.StartsWith("mmcblk", StringComparison.Ordinal)
                || name.StartsWith("loop", StringComparison.Ordinal)
                || name.StartsWith("nbd", StringComparison.Ordinal)))
        {
            return name[..p];
        }

        // sdb1 / vdb2 / hda3 / xvda1 → 去掉尾部数字
        var i = name.Length;
        while (i > 0 && char.IsAsciiDigit(name[i - 1]))
        {
            i--;
        }

        if (i > 0 && i < name.Length)
        {
            var baseName = name[..i];
            if (baseName.StartsWith("sd", StringComparison.Ordinal)
                || baseName.StartsWith("vd", StringComparison.Ordinal)
                || baseName.StartsWith("hd", StringComparison.Ordinal)
                || baseName.StartsWith("xvd", StringComparison.Ordinal)
                || baseName.StartsWith("mmcblk", StringComparison.Ordinal))
            {
                return baseName;
            }
        }

        return name;
    }


    private static bool? ReadSysfsFlag(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() == "1" : null;
        }
        catch (Exception)
        {
            return null;
        }
    }


    private static bool IsOnUsbBus(string blockName)
    {
        try
        {
            // /sys/block/<dev> 是指向 /sys/devices/.../usbN/... 的相对软链
            var linkTarget = new DirectoryInfo(Path.Join("/sys/block", blockName)).LinkTarget;
            if (!string.IsNullOrEmpty(linkTarget))
            {
                return linkTarget.Contains("usb", StringComparison.OrdinalIgnoreCase);
            }

            // 某些内核把 block 目录做成真实目录，此时回溯物理设备路径
            var resolved = new DirectoryInfo(Path.Join("/sys/block", blockName)).ResolveLinkTarget(returnFinalTarget: true);
            return resolved?.FullName.Contains("/usb", StringComparison.OrdinalIgnoreCase) is true;
        }
        catch (Exception)
        {
            return false;
        }
    }

}
