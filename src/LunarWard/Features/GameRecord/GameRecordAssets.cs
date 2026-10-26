using Avalonia.Media.Imaging;

namespace LunarWard.Features.GameRecord;

/// <summary>
/// 上游 <c>ms-appx:///Assets/...</c> 包资源的等价加载器。
/// </summary>
/// <remarks>
/// Linux: 上游用 <c>new BitmapImage(new Uri("ms-appx:///Assets/Image/x.png"))</c> 读取 WinUI 包内资源；
/// 本仓库同名的品牌素材由 NuGet 内容包 <c>Starward.Assets</c> 复制到输出目录 <c>Assets/Image/</c>
/// （见 <see cref="LunarWard.Services.GameAssets"/>），所以这里从磁盘解码。
/// 找不到文件时返回 null，与上游「素材缺失只影响装饰、不抛异常」的行为一致。
/// </remarks>
internal static class GameRecordAssets
{
    // Linux: 上游 ms-appx:/// 包 URI；这里是输出目录里的实际文件。
    public static Bitmap? FromMsAppx(string relativePath)
    {
        string path = Path.Combine(AppContext.BaseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            return new Bitmap(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
