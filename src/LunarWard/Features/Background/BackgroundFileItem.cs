using System;
using System.IO;
using Avalonia.Media.Imaging;

namespace LunarWard.Features.Background;

/// <summary>
/// 背景文件条目（对应上游 <c>Starward/Features/Background/BackgroundFileItem.cs</c>）。
/// </summary>
public class BackgroundFileItem
{

    public string FilePath { get; set; }

    public string FileInfo { get; set; }

    public long FileSize { get; set; }

    public DateTime CreationTime { get; set; }

    public bool IsVideo { get; set; }


    private Avalonia.Media.IImage? _thumbnail;

    /// <summary>缩略图（对应上游 BackgroundFileItem.Thumbnail，图标即视频）。</summary>
    public Avalonia.Media.IImage? Thumbnail
    {
        get
        {
            if (_thumbnail is null && !IsVideo && File.Exists(FilePath))
            {
                try
                {
                    using var fs = File.OpenRead(FilePath);
                    _thumbnail = Bitmap.DecodeToWidth(fs, 320);
                }
                catch (Exception)
                {
                    return null;
                }
            }
            return _thumbnail;
        }
    }


    public BackgroundFileItem(string file)
    {
        FilePath = file;
        var info = new FileInfo(file);
        FileSize = info.Length;
        CreationTime = info.LastWriteTime;
        FileInfo = GetFileInfo(info);
        IsVideo = BackgroundService.FileIsSupportedVideo(file);
    }


    private static string GetFileInfo(FileInfo info)
    {
        const double KB = 1 << 10, MB = 1 << 20;
        string ext = info.Extension.Replace(".", "").ToUpper();
        string size = info.Length >= MB ? $"{info.Length / MB:F2} MB" : $"{info.Length / KB:F2} KB";
        return $"{ext}  {size}".Trim();
    }


}
