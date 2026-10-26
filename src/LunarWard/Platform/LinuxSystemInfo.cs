namespace LunarWard.Platform;

/// <summary>
/// LunarWard 追加（Starward 没有）：关于页展示的 Linux 运行环境摘要。
/// </summary>
/// <remarks>
/// <para>
/// 只读 <c>/proc</c> 与 <c>/sys</c> 里的现成文件，不调用 <c>lspci</c> / <c>vulkaninfo</c>
/// 之类的外部命令：关于页每次打开都会读一次，起进程既慢又需要额外依赖。
/// </para>
/// <para>
/// 显卡名称需要 <c>pci.ids</c> 才能把 <c>PCI_ID</c> 翻译成人话，这里不查表，只给
/// 驱动名 + PCI ID；排查驱动问题时这两项比型号更有用。
/// </para>
/// </remarks>
public sealed record LinuxSystemInfo(
    string Kernel,
    string Session,
    string GpuDriver,
    string GpuPciId)
{
    /// <summary>未知值统一显示成这个，避免界面出现空白行。</summary>
    public const string Unknown = "未知";


    /// <summary>
    /// 读取当前系统的信息；文件缺失时对应字段退化为 <see cref="Unknown"/>，不抛异常。
    /// </summary>
    /// <param name="procRoot">
    /// <c>/proc</c> 的替代根（单测用）。生产代码只传默认值。
    /// </param>
    /// <param name="sysRoot">
    /// <c>/sys</c> 的替代根（单测用）。生产代码只传默认值。
    /// </param>
    /// <param name="environment">
    /// 环境变量来源（单测用）。生产代码传 <see langword="null"/>，表示
    /// <see cref="Environment.GetEnvironmentVariable(string)"/>。
    /// </param>
    public static LinuxSystemInfo Read(
        string procRoot = "/proc",
        string sysRoot = "/sys",
        Func<string, string?>? environment = null)
    {
        environment ??= Environment.GetEnvironmentVariable;

        string kernel = ReadFirstLine(Path.Join(procRoot, "sys", "kernel", "osrelease"));

        // XDG_SESSION_TYPE：wayland / x11 / tty；桌面环境再补一层（KDE / GNOME…）。
        string sessionType = environment("XDG_SESSION_TYPE") ?? "";
        string desktop = environment("XDG_CURRENT_DESKTOP") ?? "";
        string session = (sessionType, desktop) switch
        {
            ("", "") => Unknown,
            ("", _) => desktop,
            (_, "") => sessionType,
            _ => $"{sessionType} · {desktop}",
        };

        (string gpuDriver, string gpuPciId) = ReadGpu(sysRoot);

        return new LinuxSystemInfo(
            string.IsNullOrWhiteSpace(kernel) ? Unknown : kernel.Trim(),
            session,
            gpuDriver,
            gpuPciId);
    }


    /// <summary>读显卡驱动与 PCI ID；取第一块有 <c>DRIVER</c> 的 DRM 卡。</summary>
    private static (string Driver, string PciId) ReadGpu(string sysRoot)
    {
        string drmRoot = Path.Join(sysRoot, "class", "drm");
        if (!Directory.Exists(drmRoot))
        {
            return (Unknown, Unknown);
        }

        // card0/card1… 的排序可能随驱动探测顺序变化，先按名字排序保证稳定。
        foreach (string card in Directory.GetDirectories(drmRoot, "card*").Order(StringComparer.Ordinal))
        {
            string? uevent = ReadAllTextOrNull(Path.Join(card, "device", "uevent"));
            if (uevent is null)
            {
                continue;
            }

            string driver = FindValue(uevent, "DRIVER=") ?? Unknown;
            string pciId = FindValue(uevent, "PCI_ID=") ?? Unknown;
            if (driver != Unknown || pciId != Unknown)
            {
                return (driver, pciId);
            }
        }

        return (Unknown, Unknown);
    }


    private static string? FindValue(string text, string prefix)
    {
        foreach (string line in text.Split('\n'))
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                return line[prefix.Length..].Trim();
            }
        }

        return null;
    }


    private static string ReadFirstLine(string path) => ReadFirstLineOrNull(path) ?? "";


    private static string? ReadAllTextOrNull(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }


    private static string? ReadFirstLineOrNull(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadLines(path).FirstOrDefault() : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
