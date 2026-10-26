using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using LunarWard.Helpers;
using System.Diagnostics;
using System.Reflection;

namespace LunarWard.Controls;


/// <summary>
/// 上游 <c>Starward.Controls.CachedImage</c>（继承 Scighost.WinUI.ImageEx 的 <c>ImageEx</c>）的 Avalonia 移植。
/// <para>
/// 上游行为：给一个 Uri，远端图片先落到 <see cref="FileCache"/> 缓存再解码成 BitmapImage；
/// 本地 file:// 图片在 <see cref="IsThumbnail"/> 时走 <c>Features/Codec/ImageThumbnail</c> 缩略图；
/// ms-appx:// 资源直接解码。
/// </para>
/// <para>
/// Linux/Avalonia 差异（见 docs/09）：Avalonia 没有 <c>ImageEx</c> 与 BitmapImage.ImageOpened/ImageFailed，
/// 这里用 <see cref="Image"/> + 异步解码实现同样语义；XAML 里写的是字符串地址，因此另外提供
/// <see cref="UriSource"/>（string），<see cref="Image.Source"/> 仍可直接赋 IImage。
/// 缩略图分支等 Features/Codec（ImageThumbnail）移植后接回，见 <see cref="ProvideCachedResourceAsync"/> 注释。
/// </para>
/// </summary>
public class CachedImage : Image
{


    public static readonly StyledProperty<bool> IsThumbnailProperty =
        AvaloniaProperty.Register<CachedImage, bool>(nameof(IsThumbnail));

    /// <summary>上游 <c>ImageEx.IsThumbnail</c>：本地图片是否走缩略图解码。</summary>
    public bool IsThumbnail
    {
        get => GetValue(IsThumbnailProperty);
        set => SetValue(IsThumbnailProperty, value);
    }


    public static readonly StyledProperty<bool> PngThumbnailProperty =
        AvaloniaProperty.Register<CachedImage, bool>(nameof(PngThumbnail));

    /// <summary>上游 <c>ImageEx.PngThumbnail</c>：缩略图是否输出 png 缓存。</summary>
    public bool PngThumbnail
    {
        get => GetValue(PngThumbnailProperty);
        set => SetValue(PngThumbnailProperty, value);
    }


    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<CachedImage, CornerRadius>(nameof(CornerRadius));

    /// <summary>
    /// 上游 XAML 大量使用 <c>CornerRadius</c>（WinUI x:Bind 到 ImageEx 的圆角裁剪）。
    /// Avalonia 12 的 <see cref="Image.Render"/> 是 sealed，不能像 WinUI 那样在绘制里裁剪，
    /// 因此改为设置 <see cref="Visual.Clip"/> 的圆角矩形，视觉等价（docs/09「二十二」共性问题清单）。
    /// </summary>
    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }


    public static readonly StyledProperty<string?> UriSourceProperty =
        AvaloniaProperty.Register<CachedImage, string?>(nameof(UriSource));

    /// <summary>
    /// 图片地址（string，便于 XAML 直接写 <c>UriSource="https://…"</c> / <c>{Binding Url}</c>）；
    /// 赋值后异步走 <see cref="ProvideCachedResourceAsync"/> 加载到 <see cref="Image.Source"/>。
    /// </summary>
    public string? UriSource
    {
        get => GetValue(UriSourceProperty);
        set => SetValue(UriSourceProperty, value);
    }



    private CancellationTokenSource? _cancellationTokenSource;


    static CachedImage()
    {
        UriSourceProperty.Changed.AddClassHandler<CachedImage>((image, e) => image.OnUriSourceChanged(e));
    }



    private async void OnUriSourceChanged(AvaloniaPropertyChangedEventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        var cts = new CancellationTokenSource();
        _cancellationTokenSource = cts;
        Source = null;
        string? uri = e.NewValue as string;
        if (string.IsNullOrWhiteSpace(uri))
        {
            return;
        }
        try
        {
            if (Uri.TryCreate(uri, UriKind.Absolute, out var imageUri))
            {
                var resource = await ProvideCachedResourceAsync(imageUri, cts.Token);
                if (!cts.IsCancellationRequested)
                {
                    Source = resource;
                }
            }
            else if (File.Exists(uri))
            {
                var resource = await ProvideCachedResourceAsync(new Uri(Path.GetFullPath(uri)), cts.Token);
                if (!cts.IsCancellationRequested)
                {
                    Source = resource;
                }
            }
        }
        catch (TaskCanceledException)
        {
        }
        catch (FileNotFoundException)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            if (Uri.TryCreate(uri, UriKind.Absolute, out var failed))
            {
                FileCache.DeleteCacheFile(failed.ToString());
            }
        }
    }



    /// <summary>
    /// 对应上游 <c>ImageEx.ProvideCachedResourceAsync</c>：把 Uri 解成可直接显示的位图。
    /// </summary>
    protected virtual async Task<IImage?> ProvideCachedResourceAsync(Uri imageUri, CancellationToken token)
    {
        if (imageUri.Scheme is "avares" or "ms-appx")
        {
            // 上游：new BitmapImage(imageUri) 读取打包资源。
            await using var assetStream = AssetLoader.Open(ToAssetUri(imageUri));
            return new Bitmap(assetStream);
        }
        else if (imageUri.Scheme is "file")
        {
            if (IsThumbnail)
            {
                // 上游：视频走 ImageThumbnail.GetVideoThumbnailAsync，图片走 ImageThumbnail.GetImageThumbnailAsync。
                // 本仓库 Features/Codec（Win2D 解码）尚未移植（task-9），这里直接解码原图；
                // 待 ImageThumbnail 落地后接回缩略图分支。
            }
            await using var fileStream = File.OpenRead(imageUri.LocalPath);
            return new Bitmap(fileStream);
        }
        else
        {
            var file = await FileCache.GetFromCacheAsync(imageUri.ToString(), false, token);
            if (token.IsCancellationRequested)
            {
                throw new TaskCanceledException("Image source has changed.");
            }
            if (file is null)
            {
                throw new FileNotFoundException(imageUri.ToString());
            }
            await using var cacheStream = File.OpenRead(file);
            return new Bitmap(cacheStream);
        }
    }



    /// <summary>ms-appx:///Assets/a.png → avares://LunarWard/Assets/a.png（本仓库资源在应用程序集里）。</summary>
    private static Uri ToAssetUri(Uri uri)
    {
        if (uri.Scheme is "avares")
        {
            return uri;
        }
        string path = uri.AbsolutePath.TrimStart('/');
        Assembly assembly = typeof(CachedImage).Assembly;
        return new Uri($"avares://{assembly.GetName().Name}/{path}");
    }




    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        UpdateCornerClip(size);
        return size;
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CornerRadiusProperty)
        {
            UpdateCornerClip(Bounds.Size);
        }
    }


    private void UpdateCornerClip(Size size)
    {
        CornerRadius radius = CornerRadius;
        if (size.Width <= 0 || size.Height <= 0 || (radius.TopLeft <= 0 && radius.TopRight <= 0 && radius.BottomLeft <= 0 && radius.BottomRight <= 0))
        {
            Clip = null;
            return;
        }
        // Avalonia 的 RectangleGeometry 只支持统一圆角；取最大值近似（数量级都在 4-8px，肉眼无差）。
        double r = Math.Max(Math.Max(radius.TopLeft, radius.TopRight), Math.Max(radius.BottomLeft, radius.BottomRight));
        Clip = new RectangleGeometry(new Rect(size), r, r);
    }


}
