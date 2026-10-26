using System;
using System.IO;

namespace LunarWard.Helpers;

/// <summary>
/// 上游 Helpers/DriveHelper.cs 的 Linux 等价物。上游用 Vanara 的 <c>Kernel32</c> 查询
/// 盘符/卷信息；Linux 上 <see cref="DriveInfo"/> 已覆盖除「USB 总线识别」外的全部能力。
/// </summary>
internal abstract class DriveHelper
{


    /// <summary>
    /// 是否是可移动介质或 USB 设备。上游用 <c>IOCTL_STORAGE_QUERY_PROPERTY</c> 查总线类型；
    /// Linux 上读 <c>/sys/block/*/removable</c> 与挂载点路径判断（两者任一命中即算）。
    /// </summary>
    public static bool IsDeviceRemovableOrOnUSB(string path)
    {
        try
        {
            var drive = new DriveInfo(path);
            if (drive.DriveType is DriveType.Removable)
            {
                return true;
            }

            var full = Path.GetFullPath(path);
            // 常见可移动挂载点：/media/<user>/..., /run/media/<user>/..., /mnt/...
            if (full.StartsWith("/media/", StringComparison.Ordinal)
                || full.StartsWith("/run/media/", StringComparison.Ordinal)
                || full.StartsWith("/mnt/", StringComparison.Ordinal))
            {
                return true;
            }

            // /sys/block/<name>/removable == 1 时，该设备（及挂在它下面的分区）算可移动介质
            var deviceName = GetUnderlyingDeviceName(full);
            if (!string.IsNullOrWhiteSpace(deviceName))
            {
                var removableFile = $"/sys/block/{deviceName}/removable";
                if (File.Exists(removableFile) && File.ReadAllText(removableFile).Trim() == "1")
                {
                    return true;
                }
            }
        }
        catch { }
        return false;
    }


    /// <summary>把挂载点解析成 <c>/sys/block</c> 下的物理设备名（分区会回溯到父设备）。</summary>
    private static string? GetUnderlyingDeviceName(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var mountInfo = File.ReadAllLines("/proc/self/mountinfo");
            foreach (var line in mountInfo)
            {
                // 形如：36 24 8:2 / / rw,noatime shared:1 - ext4 /dev/sda2 rw
                var fields = line.Split(' ');
                if (fields.Length < 10)
                {
                    continue;
                }

                var mountPoint = Unescape(fields[4]);
                if (!full.StartsWith(mountPoint.TrimEnd('/') + "/", StringComparison.Ordinal) && full != mountPoint)
                {
                    continue;
                }

                for (var i = 6; i < fields.Length - 1; i++)
                {
                    if (fields[i] == "-")
                    {
                        var source = i + 1 < fields.Length ? fields[i + 1] : null;
                        if (source is null || !source.StartsWith("/dev/", StringComparison.Ordinal))
                        {
                            return null;
                        }

                        var name = Path.GetFileName(source);
                        // /dev/nvme0n1p2 -> nvme0n1；/dev/sdb1 -> sdb；/dev/mmcblk0p1 -> mmcblk0
                        if (name.StartsWith("nvme", StringComparison.Ordinal)
                            || name.StartsWith("mmcblk", StringComparison.Ordinal))
                        {
                            var p = name.LastIndexOf('p');
                            return p > 0 ? name[..p] : name;
                        }
                        return name.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
                    }
                }
            }
        }
        catch { }
        return null;
    }


    /// <summary>mountinfo 里的空格等字符用八进制转义，复原成真实路径。</summary>
    private static string Unescape(string value)
    {
        if (!value.Contains('\\'))
        {
            return value;
        }
        var bytes = new System.Collections.Generic.List<byte>();
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 3 < value.Length
                && char.IsDigit(value[i + 1]) && char.IsDigit(value[i + 2]) && char.IsDigit(value[i + 3]))
            {
                bytes.Add(System.Convert.ToByte(new string([value[i + 1], value[i + 2], value[i + 3]]), 8));
                i += 3;
            }
            else
            {
                bytes.Add((byte)value[i]);
            }
        }
        return System.Text.Encoding.UTF8.GetString(bytes.ToArray());
    }


    public static DriveType GetDriveType(string path)
    {
        try
        {
            return new DriveInfo(Path.GetFullPath(path)).DriveType;
        }
        catch
        {
            return DriveType.Unknown;
        }
    }


    public static long GetDriveAvailableSpace(string path)
    {
        return new DriveInfo(Path.GetFullPath(path)).AvailableFreeSpace;
    }


    public static string? GetDriveFormat(string path)
    {
        try
        {
            return new DriveInfo(Path.GetFullPath(path)).DriveFormat;
        }
        catch
        {
            return null;
        }
    }
}
