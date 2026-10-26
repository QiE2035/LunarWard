using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace LunarWard;

public static partial class AppConfig
{

    public static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// 沿用上游成员名（API 对齐，见 docs/10 §3）；Linux 上是当前进程可执行路径。
    /// </summary>
    public static string StarwardExecutePath => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "LunarWard");

    public static Guid DeviceId { get; private set; }

    public static Guid SessionId { get; private set; }


    static AppConfig()
    {
        // 上游用 BIOS 版本 + MachineGuid + 机器名 做 MD5 种子的设备指纹。
        // Linux 等价：/sys/class/dmi/id/bios_version + /etc/machine-id + 主机名。
        string biosVersion = ReadFirstLine("/sys/class/dmi/id/bios_version") ?? "";
        string machineId = ReadFirstLine("/etc/machine-id") ?? ReadFirstLine("/var/lib/dbus/machine-id") ?? "";
        DeviceId = new(MD5.HashData(Encoding.UTF8.GetBytes($"{biosVersion}{machineId}{Environment.MachineName}")));
        SessionId = Guid.CreateVersion7();
    }


    private static string? ReadFirstLine(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadLines(path).FirstOrDefault()?.Trim() : null;
        }
        catch
        {
            return null;
        }
    }



    #region Emoji


    // 上游为 ms-appx:///Assets/Image/...；Linux 上这些图片由 Starward.Assets 包的 contentFiles 拷到输出目录，
    // 因此用输出目录的绝对 file:// URI（Avalonia 的 Bitmap / 图片源都能直接吃）。见 docs/09。
    public static Uri EmojiPaimon = new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "Image", "UI_EmotionIcon5.png"));

    public static Uri EmojiPom = new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "Image", "20008.png"));

    public static Uri EmojiAI = new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "Image", "bdfd19c3bdad27a395890755bb60b162.png"));

    public static Uri EmojiBangboo = new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "Image", "pamu.db6c2c7b.png"));


    #endregion



}
