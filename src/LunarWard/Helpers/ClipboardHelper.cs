using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using LunarWard.Core;

namespace LunarWard.Helpers;

/// <summary>
/// 上游 <c>Starward.Helpers.ClipboardHelper</c> 的 Avalonia 等价物。
/// <para>
/// 上游用 WinRT <c>Clipboard.SetContent(DataPackage)</c>；Avalonia 12 的 <see cref="IClipboard"/>
/// 走 <c>IAsyncDataTransfer</c>，文本/位图分别由 <c>ClipboardExtensions.SetTextAsync</c> /
/// <c>SetBitmapAsync</c> 提供，这里只是把它们包成上游同名的同步入口。
/// </para>
/// <para>
/// <see cref="Clipboard"/> 由 App 启动时注入（<c>ApplicationLifetimes.MainWindow.Clipboard</c>），
/// 因为上游的静态 <c>Clipboard.SetContent</c> 在 Avalonia 需要 TopLevel 上下文。
/// </para>
/// </summary>
public static class ClipboardHelper
{

    /// <summary>当前剪贴板；由 App 启动时设为主窗口的 <c>Clipboard</c>。</summary>
    public static IClipboard? Clipboard { get; set; }



    public static void SetText(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && Clipboard is { } clipboard)
        {
            _ = clipboard.SetTextAsync(value);
        }
    }



    public static void SetBitmap(string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            SetBitmap(File.OpenRead(filePath));
        }
    }



    public static void SetBitmap(Stream stream)
    {
        if (Clipboard is { } clipboard)
        {
            _ = SetBitmapCoreAsync(clipboard, new Bitmap(stream));
        }
    }



    public static void SetBitmap(Uri uri)
    {
        if (Clipboard is { } clipboard)
        {
            _ = Task.Run(async () =>
            {
                using var client = new HttpClient();
                await using var stream = await client.GetStreamAsync(uri);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                ms.Position = 0;
                await SetBitmapCoreAsync(clipboard, new Bitmap(ms));
            });
        }
    }



    private static async Task SetBitmapCoreAsync(IClipboard clipboard, Bitmap bitmap)
    {
        try
        {
            await clipboard.SetBitmapAsync(bitmap);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Set clipboard bitmap failed");
        }
    }



    public static async Task<string?> GetTextAsync()
    {
        if (Clipboard is { } clipboard)
        {
            return await clipboard.TryGetTextAsync();
        }
        return null;
    }

}
