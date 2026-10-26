using System.IO;
using System.Runtime.InteropServices;

namespace LunarWard.Installer;

/// <summary>
/// 磁盘/卷信息。
///
/// <para>上游 <c>Starward.RPC.DriveHelper</c> 通过 <c>Vanara.PInvoke.Kernel32</c> 调用
/// <c>GetDriveType</c> / <c>GetDiskFreeSpaceEx</c> / <c>GetVolumeInformation</c>，
/// 并用 <c>IOCTL_STORAGE_QUERY_PROPERTY</c> 判断 USB。Linux 上这些 API 都不存在，
/// 这里改用 BCL 的 <see cref="DriveInfo"/>、<c>statx(2)</c> 与少量 <c>procfs</c>/<c>sysfs</c> 读取。</para>
///
/// <para><b>为什么不能用 <c>Path.GetPathRoot</c></b>：在 Unix 上它恒定返回 <c>"/"</c>
/// （<c>Path.GetPathRoot("/tmp") == "/"</c>），于是 <c>new DriveInfo(root)</c> 永远在查根卷，
/// <c>/tmp</c>（tmpfs）会被报成根卷的 <c>btrfs</c>。本类的所有 API 因此都直接对
/// <b>路径本身</b>（或其最近的已存在祖先）构造 <see cref="DriveInfo"/>，由 BCL 解析真实挂载点。</para>
///
/// <para><b>为什么同一卷判定要用 <c>statx(2)</c></b>：btrfs 子卷共用 <c>mountinfo</c> 里的同一个
/// <c>major:minor</c>（实测 <c>/</c> 与 <c>/home</c> 都是 <c>0:34</c>），但它们的
/// <c>st_dev</c> 不同，<c>link(2)</c> 跨子卷会返回 <c>EXDEV</c>。只看 mountinfo 会误判为同卷。</para>
/// </summary>
public static class DriveHelper
{


    /// <summary>
    /// 路径所在挂载点的根（即 cover 该路径的最深挂载点），总是以 <c>/</c> 结尾；
    /// 无法解析时返回 <c>null</c>。这与上游"补尾部斜杠"的语义一致，
    /// 但返回的是<b>真实挂载点</b>而不是恒定 <c>/</c>。
    /// </summary>
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

        var mountPoint = FindMountPoint(full)?.MountPoint;
        if (string.IsNullOrEmpty(mountPoint))
        {
            mountPoint = Path.GetPathRoot(full);
        }

        if (string.IsNullOrWhiteSpace(mountPoint))
        {
            return null;
        }

        return mountPoint.EndsWith(Path.DirectorySeparatorChar)
            ? mountPoint
            : mountPoint + Path.DirectorySeparatorChar;
    }


    /// <summary>
    /// 驱动器类型。路径不存在且找不到任何已存在的祖先时返回 <see cref="DriveType.NoRootDirectory"/>
    /// （上游 <c>Kernel32.GetDriveType</c> 对非法路径返回 <c>DRIVE_NO_ROOT_DIR</c>）。
    /// </summary>
    public static DriveType GetDriveType(string path)
    {
        var probe = GetProbePath(path);
        if (probe is null)
        {
            return DriveType.NoRootDirectory;
        }

        try
        {
            return new DriveInfo(probe).DriveType;
        }
        catch (Exception)
        {
            return DriveType.Unknown;
        }
    }


    /// <summary>
    /// 卷上当前用户可用的空闲字节数。
    /// 路径无效或卷不可读时抛 <see cref="DriveNotFoundException"/>，与上游
    /// <c>GetDiskFreeSpaceEx</c> 失败时的行为一致。
    /// </summary>
    public static long GetDriveAvailableSpace(string path)
    {
        var probe = GetProbePath(path);
        if (probe is null)
        {
            throw new DriveNotFoundException($"The path '{path}' does not have a valid root directory.");
        }

        try
        {
            return new DriveInfo(probe).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is not DriveNotFoundException)
        {
            throw new DriveNotFoundException($"Failed to get disk free space information for path '{path}'.", ex);
        }
    }


    /// <summary>卷的总容量（字节）。</summary>
    public static long GetDriveTotalSpace(string path)
    {
        var probe = GetProbePath(path);
        if (probe is null)
        {
            throw new DriveNotFoundException($"The path '{path}' does not have a valid root directory.");
        }

        try
        {
            return new DriveInfo(probe).TotalSize;
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
        var probe = GetProbePath(path);
        if (probe is null)
        {
            return null;
        }

        try
        {
            var format = new DriveInfo(probe).DriveFormat;
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
    /// ext2/3/4、btrfs（含跨子卷 —— 那是 <c>EXDEV</c>，由 <see cref="IsSameVolume"/> 负责）、
    /// xfs、zfs、f2fs、tmpfs、overlayfs 都支持硬链接；
    /// FAT/exFAT/ISO9660/UDF/NFS 不支持。</para>
    /// </summary>
    public static bool SupportsHardLink(string path)
    {
        var format = GetDriveFormat(path);
        if (format is null)
        {
            // 判不出格式（如权限受限）时乐观放行：真正的失败由 link(2) 的返回值兜底，
            // 由 GameInstallHelper.HardLinkAsync 回退到重新下载。
            return true;
        }

        return format switch
        {
            // 无 inode 概念 / 明确不支持硬链接
            "vfat" or "msdos" or "msdosfs" or "fat" or "fat16" or "fat32" => false,
            "exfat" or "fuse.exfat" => false,
            "iso9660" or "udf" or "fuse.udf" or "iso9660fs" => false,
            "nfs" or "nfs2" or "nfs3" => false,
            "cifs" or "smbfs" or "smb3" => false,
            "proc" or "sysfs" or "devpts" or "cgroup" or "cgroup2" => false,
            // ntfs / ntfs3 / fuseblk 支持；ext4 / btrfs / xfs / zfs / f2fs / tmpfs 支持
            _ => true,
        };
    }


    /// <summary>
    /// 两个路径是否位于同一个卷上（可安全 <c>link(2)</c>）。
    ///
    /// <para>这是上游 <c>Path.GetPathRoot(a) == Path.GetPathRoot(b)</c> 在 Linux 上的正确版本。
    /// 在 Linux 上 <c>Path.GetPathRoot</c> 对任何路径都返回 <c>/</c>，直接比较会把
    /// <c>/home</c> 与 <c>/var</c> 误判为同卷。更隐蔽的是 btrfs 子卷：它们在
    /// <c>mountinfo</c> 里共享同一个 <c>major:minor</c>，但跨子卷 <c>link(2)</c> 仍返回
    /// <c>EXDEV</c>。因此这里优先用 <c>statx(2)</c> 比较 <c>stx_dev_major</c>/<c>stx_dev_minor</c>。</para>
    /// </summary>
    public static bool IsSameVolume(string pathA, string pathB)
    {
        try
        {
            var a = GetProbePath(pathA);
            var b = GetProbePath(pathB);
            if (a is null || b is null)
            {
                return false;
            }

            var devA = StatxDeviceId(a);
            var devB = StatxDeviceId(b);
            if (devA is not null && devB is not null)
            {
                return devA == devB;
            }

            // statx 不可用（老内核/非 Linux）时退化为挂载点比较
            return string.Equals(GetPathRoot(a), GetPathRoot(b), StringComparison.Ordinal);
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

            var probe = GetProbePath(path);
            if (probe is null)
            {
                return false;
            }

            var driveType = GetDriveType(probe);
            if (driveType is DriveType.Removable or DriveType.CDRom)
            {
                return true;
            }

            if (driveType is not DriveType.Fixed)
            {
                return false;
            }

            var device = FindMountPoint(Path.GetFullPath(path))?.Source;
            var blockName = GetBlockDeviceName(device);
            if (blockName is null)
            {
                return false;
            }

            if (ReadSysfsFlag(Path.Join("/sys/block", blockName, "removable")) is true)
            {
                return true;
            }

            return IsOnUsbBlockDevice(blockName);
        }
        catch (Exception)
        {
            return false;
        }
    }




    // ———————————————————————————— 内部实现 ————————————————————————————


    /// <summary>
    /// 返回可用于构造 <see cref="DriveInfo"/> 的路径：路径本身（或最近的已存在祖先）。
    /// <see cref="DriveInfo"/> 在 Unix 上对不存在的路径抛 <see cref="DriveNotFoundException"/>，
    /// 而"目标目录还没创建"是安装流程的正常场景（安装前做空间预检）。
    /// </summary>
    private static string? GetProbePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string current;
        try
        {
            current = Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return null;
        }

        while (!string.IsNullOrEmpty(current))
        {
            try
            {
                if (Directory.Exists(current) || File.Exists(current))
                {
                    return current;
                }
            }
            catch (Exception)
            {
                // 权限不足等，继续向上找
            }

            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || parent == current)
            {
                // 退到根了
                return Directory.Exists("/") ? "/" : null;
            }

            current = parent;
        }

        return null;
    }


    /// <summary>
    /// 用 <c>statx(2)</c> 取 <c>st_dev</c> 的 (major, minor)。
    /// 这是区分 btrfs 子卷的唯一可靠办法；失败返回 <c>null</c> 让调用方回退。
    /// </summary>
    private static (uint Major, uint Minor)? StatxDeviceId(string path)
    {
        try
        {
            if (SysStatx(AtFdcwd, path, AtSymlinkNofollow, StatxBasicStats, out var statx) != 0)
            {
                return null;
            }

            return (statx.DevMajor, statx.DevMinor);
        }
        catch (Exception)
        {
            // 非 Linux / 无 statx 符号
            return null;
        }
    }


    private const int AtFdcwd = -100;
    private const int AtSymlinkNofollow = 0x100;
    private const uint StatxBasicStats = 0x7ff;


    [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
    private static extern int SysStatx(int dirfd, [MarshalAs(UnmanagedType.LPUTF8Str)] string pathname,
                                       int flags, uint mask, out Statx buffer);


    /// <summary>
    /// <c>struct statx</c>（Linux 5.6+，布局跨架构稳定，总计 256 字节）。
    /// 只声明到 <c>stx_dev_minor</c> 为止真正用到的字段，其余用 spare 补齐以保证长度正确 ——
    /// 长度不对会让内核越界写。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct StatxTimestamp
    {
        public long TvSec;
        public uint TvNsec;
        public int Reserved;
    }


    [StructLayout(LayoutKind.Sequential)]
    private struct Statx
    {
        public uint Mask;
        public uint Blksize;
        public ulong Attributes;
        public uint Nlink;
        public uint Uid;
        public uint Gid;
        public ushort Mode;
        public ushort Spare0;
        public ulong Ino;
        public ulong Size;
        public ulong Blocks;
        public ulong AttributesMask;
        public StatxTimestamp Atime;
        public StatxTimestamp Btime;
        public StatxTimestamp Ctime;
        public StatxTimestamp Mtime;
        public uint RdevMajor;
        public uint RdevMinor;
        public uint DevMajor;
        public uint DevMinor;
        public ulong MntId;
        public uint DioMemAlign;
        public uint DioOffsetAlign;

        // __spare3[12]：12 * 8 = 96 字节，保证结构体总计 256 字节
        public ulong Spare1, Spare2, Spare3, Spare4, Spare5, Spare6,
                     Spare7, Spare8, Spare9, Spare10, Spare11, Spare12;
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


    private static bool IsOnUsbBlockDevice(string blockName)
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
            var resolved = new DirectoryInfo(Path.Join("/sys/block", blockName))
                .ResolveLinkTarget(returnFinalTarget: true);
            return resolved?.FullName.Contains("/usb", StringComparison.OrdinalIgnoreCase) is true;
        }
        catch (Exception)
        {
            return false;
        }
    }

}
