using System.Collections.Concurrent;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LunarWard.App.Services;

namespace LunarWard.App.Services;

/// <summary>
/// 抽卡物品图标的下载与缓存（对应上游 <c>CachedImage</c> 的磁盘+内存缓存行为）。
/// </summary>
/// <remarks>
/// <para>
/// Starward 用 <c>CommunityToolkit.WinUI.Controls.CachedImage</c> 显示
/// <c>GenshinGachaInfo.Icon</c> 这样的远程 URL（GachaStatsCard.xaml:184-187，28×28）。
/// Avalonia 的 <c>Image</c> 不会自己下载，这里补一个最小等价物：内存字典 + 磁盘缓存
/// （<c>&lt;CacheHome&gt;/gacha-icons/&lt;sha1&gt;.png</c>），失败时静默返回 null，
/// 调用方回退到原来的 28×28 占位块 —— 不伪造素材。
/// </para>
/// <para>
/// 缓存目录来自 <see cref="AppServices.Current"/> 的 <c>Paths.CacheHome</c>；在自检
/// （截图）里同样是缓存目录，不影响用户数据。
/// </para>
/// </remarks>
public static class GachaIconCache
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(20),
    };

    private static readonly ConcurrentDictionary<string, Bitmap> Memory = new(StringComparer.Ordinal);

    private static readonly ConcurrentDictionary<string, Task<Bitmap?>> Pending = new(StringComparer.Ordinal);


    /// <summary>同步取已缓存的内存位图；没有就返回 <c>null</c>（调用方随后可 <see cref="LoadAsync"/>）。</summary>
    public static Bitmap? TryGet(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        return Memory.TryGetValue(url, out Bitmap? bitmap) ? bitmap : null;
    }


    /// <summary>
    /// 下载并解码图标（同一个 URL 并发调用会合并成一个请求），结果写入内存与磁盘缓存。
    /// </summary>
    public static Task<Bitmap?> LoadAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Task.FromResult<Bitmap?>(null);
        }

        if (Memory.TryGetValue(url, out Bitmap? cached))
        {
            return Task.FromResult<Bitmap?>(cached);
        }

        return Pending.GetOrAdd(url, static u => LoadCoreAsync(u));
    }


    private static async Task<Bitmap?> LoadCoreAsync(string url)
    {
        try
        {
            string? file = CacheFile(url);
            byte[] bytes;
            if (file is not null && File.Exists(file))
            {
                bytes = await File.ReadAllBytesAsync(file).ConfigureAwait(false);
            }
            else
            {
                bytes = await Http.GetByteArrayAsync(url).ConfigureAwait(false);
                if (file is not null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                    await File.WriteAllBytesAsync(file, bytes).ConfigureAwait(false);
                }
            }

            using var stream = new MemoryStream(bytes);
            var bitmap = new Bitmap(stream);
            Memory[url] = bitmap;
            return bitmap;
        }
        catch (Exception)
        {
            // 网络不可用 / URL 失效 / 解码失败：保持占位块，不抛给 UI。
            return null;
        }
        finally
        {
            Pending.TryRemove(url, out _);
        }
    }


    private static string? CacheFile(string url)
    {
        string cacheHome;
        try
        {
            cacheHome = AppServices.Current.Paths.CacheHome;
        }
        catch (InvalidOperationException)
        {
            // 自检早期（AppServices 未就绪）不落盘，只走内存缓存。
            return null;
        }

        byte[] hash = SHA1.HashData(Encoding.UTF8.GetBytes(url));
        string name = Convert.ToHexString(hash).ToLowerInvariant() + ".png";
        return Path.Join(cacheHome, "gacha-icons", name);
    }


    /// <summary>后台加载完成后切回 UI 线程回调（Avalonia 绑定不接受后台线程改属性）。</summary>
    public static void Post(Action action) => Dispatcher.UIThread.Post(action);
}
