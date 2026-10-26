using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using LunarWard.Core.HoYoPlay;
using LunarWard.Features.HoYoPlay;
using LunarWard.Features.ViewHost;
using LunarWard.Services;
using Microsoft.Extensions.Logging;

namespace LunarWard.Features.Background;

/// <summary>
/// 应用背景（对应上游 <c>Starward/Features/Background/AppBackground.xaml.cs</c>，637 行）。
/// </summary>
/// <remarks>
/// 平台替换：
/// <list type="bullet">
/// <item>WinUI Composition 的 <c>SpriteVisual</c>/<c>CompositionVideoSurface</c>/<c>MediaPlayer</c>
/// → <see cref="VideoBackgroundPlayer"/>（ffmpeg 输出到 Avalonia Image），视频无音轨（<c>-an</c>）。</item>
/// <item><c>BitmapDecoder</c>/<c>CanvasBitmap</c> → Avalonia <see cref="Bitmap"/> +
/// <see cref="RenderTargetBitmap"/> 缩放取样。</item>
/// <item><c>XamlRoot.Changed</c> → <see cref="TopLevel.ScalingChanged"/>；<c>Loaded/Unloaded</c> 保持同义。</item>
/// <item>VP9 解码器提示（InAppToast + Microsoft Store 链接）删除，登记于 docs/09。</item>
/// </list>
/// </remarks>
public partial class AppBackground : UserControl, INotifyPropertyChanged
{

    public static AppBackground? Current;


    private readonly ILogger<AppBackground> _logger;

    private readonly BackgroundService _backgroundService;


    private VideoBackgroundPlayer? _videoPlayer;


    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private readonly SemaphoreSlim _videoSemaphore = new(1, 1);

    private CancellationTokenSource? _updateBackgroundCancellationTokenSource;


    public new event PropertyChangedEventHandler? PropertyChanged;


    private void OnPropertyChanged(string? propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


    protected bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (!Equals(field, value))
        {
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        return false;
    }



    private GameId? _currentGameId;

    /// <summary>
    /// 当前背景对应的游戏 Id
    /// </summary>
    public GameId? CurrentGameId
    {
        get => _currentGameId;
        set
        {
            if (!Equals(value, _currentGameId))
            {
                bool first = _currentGameId is null;
                _currentGameId = value;
                OnPropertyChanged(nameof(CurrentGameId));
                Image_VideoOverlay.Source = null;
                foreach (var child in _children)
                {
                    child.CurrentGameId = value;
                }
                if (first)
                {
                    InitializeBackgroundImage();
                }
            }
            _ = UpdateBackgroundAsync();
        }
    }


    private readonly System.Collections.Generic.List<AppBackground> _children = [];




    public AppBackground()
    {
        InitializeComponent();
        _logger = AppConfig.GetLogger<AppBackground>();
        _backgroundService = CreateBackgroundService();
        PlacehoderImageSource = GetDefaultBackgroundImage();
        Current = this;

        WeakReferenceMessenger.Default.Register<BackgroundChangedMessage>(this, OnBackgroundChanged);
        WeakReferenceMessenger.Default.Register<MainWindowStateChangedMessage>(this, OnMainWindowStateChanged);
        WeakReferenceMessenger.Default.Register<VideoBgVolumeChangedMessage>(this, OnVideoBgVolumeChanged);

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            topLevel.ScalingChanged += OnScalingChanged;
        }
    }




    private static BackgroundService CreateBackgroundService()
    {
        var logger = AppConfig.GetLogger<BackgroundService>();
        var hoYoPlayService = AppConfig.GetService<HoYoPlayService>();
        var factory = AppConfig.GetService<IHttpClientFactory>();
        return new BackgroundService(logger, hoYoPlayService, factory.CreateClient());
    }



    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            topLevel.ScalingChanged -= OnScalingChanged;
            topLevel.ScalingChanged += OnScalingChanged;
        }
    }


    private void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        StopVideo();
        WeakReferenceMessenger.Default.UnregisterAll(this);
        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            topLevel.ScalingChanged -= OnScalingChanged;
        }
    }


    private void OnScalingChanged(object? sender, EventArgs e)
    {
        double scale = GetUIScaleFactor();
        if (_lastScale != scale)
        {
            _ = UpdateBackgroundAsync();
        }
    }


    private double GetUIScaleFactor()
        => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;





    #region Image

    // 占位图，默认背景图
    private IImage? _placehoderImageSource;

    public IImage? PlacehoderImageSource
    {
        get => _placehoderImageSource;
        private set => SetProperty(ref _placehoderImageSource, value);
    }


    private IImage? _backgroundImageSource;

    public IImage? BackgroundImageSource
    {
        get => _backgroundImageSource;
        private set
        {
            // 对应上游：新背景为空而旧背景不为空时，把旧背景留作占位图
            if (value is null && _backgroundImageSource is not null)
            {
                PlacehoderImageSource = _backgroundImageSource;
            }
            SetProperty(ref _backgroundImageSource, value);
        }
    }


    private double _backgroundImageOpacity = 1;

    /// <summary>背景图透明度（上游用 Composition 隐式动画，这里绑定到 Image.Opacity + Transitions）。</summary>
    public double BackgroundImageOpacity
    {
        get => _backgroundImageOpacity;
        private set => SetProperty(ref _backgroundImageOpacity, value);
    }


    private double _placeholderOpacity = 1;

    /// <summary>占位图透明度（上游 ImageEx Template 的 PlaceholderImage.Opacity）。</summary>
    public double PlaceholderOpacity
    {
        get => _placeholderOpacity;
        private set => SetProperty(ref _placeholderOpacity, value);
    }


    /// <summary>上游 VisualState "Loaded"：Image 0→1（0.3s），PlaceholderImage 延迟 0.3s 后 1→0。</summary>
    private async void ApplyLoadedVisualState()
    {
        BackgroundImageOpacity = 1;
        try
        {
            await Task.Delay(300);
            PlaceholderOpacity = 0;
        }
        catch { }
    }


    /// <summary>上游 VisualState "Failed"：Image=0，PlaceholderImage=1。</summary>
    private void ApplyFailedVisualState()
    {
        BackgroundImageOpacity = 0;
        PlaceholderOpacity = 1;
    }


    /// <summary>上游 VisualState "Unloaded"：瞬时切换，不走过渡。</summary>
    private void ApplyUnloadedVisualState()
    {
        var background = this.FindControl<Image>("Image_Background");
        var placeholder = this.FindControl<Image>("Image_Placeholder");
        var bgTransitions = background?.Transitions;
        var phTransitions = placeholder?.Transitions;
        if (background is not null) background.Transitions = null;
        if (placeholder is not null) placeholder.Transitions = null;
        BackgroundImageOpacity = 0;
        PlaceholderOpacity = 1;
        if (background is not null) background.Transitions = bgTransitions;
        if (placeholder is not null) placeholder.Transitions = phTransitions;
    }


    private IImage? _videoOverlayImageSource;

    public IImage? VideoOverlayImageSource
    {
        get => _videoOverlayImageSource;
        private set => SetProperty(ref _videoOverlayImageSource, value);
    }


    private bool _isVideoPlaying;

    public bool IsVideoPlaying
    {
        get => _isVideoPlaying;
        private set => SetProperty(ref _isVideoPlaying, value);
    }



    public static IImage? GetDefaultBackgroundImage()
    {
        // 上游 Assets/Image/UI_CutScene_1130320101A.png；本仓库没有随包该图时返回 null，
        // 背景区显示为透明，由窗口背景色兜底。
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Assets", "Image", "UI_CutScene_1130320101A.png");
            if (File.Exists(path))
            {
                using var fs = File.OpenRead(path);
                return new Bitmap(fs);
            }
        }
        catch { }
        return null;
    }





    private bool _isUpdateBackgroundRunning;

    /// <summary>
    /// 是否正在更新背景图
    /// </summary>
    public bool IsUpdateBackgroundRunning
    {
        get => _isUpdateBackgroundRunning;
        private set => SetProperty(ref _isUpdateBackgroundRunning, value);
    }




    private string? _lastBackgroundFile;

    private double _lastScale = 1;


    /// <summary>当前生效的背景（上游 <c>CurrentGameBackground</c>）。</summary>
    public GameBackground? CurrentGameBackground { get; private set; }


    /// <summary>首次绑定时读缓存背景 + 恢复强调色（上游 <c>InitializeBackgroundImage</c>，117-152 行）。</summary>
    private void InitializeBackgroundImage()
    {
        try
        {
            if (CurrentGameId is null)
            {
                return;
            }
            string? file = BackgroundService.GetCachedBackgroundFile(CurrentGameId);
            if (file is not null)
            {
                if (!BackgroundService.FileIsSupportedVideo(file))
                {
                    using var fs = File.OpenRead(file);
                    BackgroundImageSource = new Bitmap(fs);
                    ApplyLoadedVisualState();
                }
            }
            else
            {
                PlacehoderImageSource = GetDefaultBackgroundImage() ?? PlacehoderImageSource;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initialize background image");
        }
    }


    /// <summary>按窗口尺寸与缩放比解码背景图（上游 <c>ChangeBackgroundImageAsync</c> 的 Win2D 缩放，285-350 行）。</summary>
    private async Task ChangeBackgroundImageAsync(string file, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        double scale = GetUIScaleFactor();
        int targetWidth = (int)Math.Max(1, Bounds.Width * scale);
        using var fs = File.OpenRead(file);
        Bitmap bitmap;
        try
        {
            bitmap = targetWidth > 0 ? Bitmap.DecodeToWidth(fs, targetWidth) : new Bitmap(fs);
        }
        catch
        {
            // 超出窗口尺寸的图交给解码器原生尺寸，Avalonia 会按 Stretch 缩放
            fs.Position = 0;
            try
            {
                bitmap = new Bitmap(fs);
            }
            catch (Exception ex)
            {
                // 上游 VisualState "Failed"：只显示占位图
                _logger.LogError(ex, "Cannot decode image: '{path}'", file);
                ApplyFailedVisualState();
                throw;
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        BackgroundImageSource = bitmap;
        ApplyLoadedVisualState();
        await Task.CompletedTask;
    }




    public async Task UpdateBackgroundAsync(GameBackground? gameBackground = null)
    {
        string? imageFilePath = null;
        try
        {
            IsUpdateBackgroundRunning = true;

            _updateBackgroundCancellationTokenSource?.Cancel();
            _updateBackgroundCancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = _updateBackgroundCancellationTokenSource.Token;

            if (CurrentGameId is null)
            {
                DisposeVideoResource();
                BackgroundImageSource = GetDefaultBackgroundImage();
                ApplyUnloadedVisualState();
                return;
            }

            for (int i = 0; i < 2; i++)
            {
                bool apiCancelled = false;
                string? filePath = null;
                GameBackground? current = null;
                try
                {
                    bool timeout = i == 0 && gameBackground is null;
                    CancellationToken apiCt = timeout ? new CancellationTokenSource(1000).Token : CancellationToken.None;
                    CancellationToken downloadCt = timeout ? new CancellationTokenSource(3000).Token : CancellationToken.None;
                    current = gameBackground ?? await _backgroundService.GetSuggestedGameBackgroundAsync(CurrentGameId, apiCt);
                    if (current is null)
                    {
                        filePath = BackgroundService.GetFallbackBackgroundImage(CurrentGameId);
                    }
                    else if (current.Type is GameBackground.BACKGROUND_TYPE_CUSTOM)
                    {
                        filePath = current.Background.Url;
                    }
                    else if (current.Type is GameBackground.BACKGROUND_TYPE_VIDEO && !current.StopVideo)
                    {
                        filePath = await _backgroundService.GetBackgroundFileAsync(current.Video.Url, downloadCt);
                    }
                    else
                    {
                        filePath = await _backgroundService.GetBackgroundFileAsync(current.Background.Url, downloadCt);
                    }
                }
                catch (OperationCanceledException)
                {
                    apiCancelled = true;
                    filePath = BackgroundService.GetFallbackBackgroundImage(CurrentGameId);
                }
                catch (Exception ex)
                {
                    filePath = BackgroundService.GetFallbackBackgroundImage(CurrentGameId);
                    _logger.LogError(ex, "Update background image");
                }
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                if (filePath is not null && filePath == _lastBackgroundFile)
                {
                    if (BackgroundService.FileIsSupportedVideo(filePath))
                    {
                        continue;
                    }
                    if (Math.Abs(_lastScale - GetUIScaleFactor()) < 0.001)
                    {
                        continue;
                    }
                }
                DisposeVideoResource();
                BackgroundImageSource = null;
                if (filePath is not null)
                {
                    if (current?.Type is GameBackground.BACKGROUND_TYPE_VIDEO)
                    {
                        await SetVideoBackgroundAsync(current, filePath, cancellationToken);
                    }
                    else if (BackgroundService.FileIsSupportedVideo(filePath))
                    {
                        // 非官方 video 类型但扩展名是视频：直接播放
                        await SetVideoBackgroundAsync(current ?? GameBackground.FromPosterUrl(filePath), filePath, cancellationToken);
                    }
                    else
                    {
                        imageFilePath = filePath;
                        await ChangeBackgroundImageAsync(filePath, cancellationToken);
                    }
                    _lastBackgroundFile = filePath;
                    _lastScale = GetUIScaleFactor();
                    CurrentGameBackground = current;
                    if (!apiCancelled && current?.Type is not GameBackground.BACKGROUND_TYPE_CUSTOM && CurrentGameId is { } gameId)
                    {
                        AppConfig.SetBg(gameId.GameBiz, Path.GetFileName(filePath));
                        var list = await _backgroundService.GetGameBackgroundsAsync(gameId);
                        AppConfig.SetGameBackgroundIds(gameId.GameBiz, string.Join(',', list.Select(x => x.Id)));
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cannot decode image: '{path}'", imageFilePath);
        }
        finally
        {
            IsUpdateBackgroundRunning = false;
        }
    }




    private async Task SetVideoBackgroundAsync(GameBackground gameBackground, string? preparedFile, CancellationToken cancellationToken)
    {
        try
        {
            string file;
            if (preparedFile is not null)
            {
                file = preparedFile;
            }
            else
            {
                var video = gameBackground.Video!;
                file = await _backgroundService.GetBackgroundFileAsync(video.Url, cancellationToken);
            }
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            await _videoSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (_videoPlayer is null)
                {
                    _videoPlayer = new VideoBackgroundPlayer();
                    ApplyLoadedVisualState();
                }
                _videoPlayer.View.Width = double.NaN;
                _videoPlayer.View.Height = double.NaN;
                VideoHost.Content = _videoPlayer.View;
                bool started = await _videoPlayer.StartAsync(file, AppConfig.GetService<LogService>());
                IsVideoPlaying = started;
                if (!started)
                {
                    // 平台替换：Linux 视频背景走 ffmpeg 子进程，失败时回退为静态图并明确提示，避免静默黑屏。
                    InAppToast.Show("视频背景不可用", "已回退为静态背景图（视频播放需要 ffmpeg）");
                }
                if (started)
                {
                    BackgroundImageOpacity = 0;
                    VideoOverlayImageSource = null;
                    await PrepareVideoOverlayImageAsync(gameBackground.Background?.Url, cancellationToken);
                }
            }
            finally
            {
                _videoSemaphore.Release();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Play video background");
        }
    }


    private async Task PrepareVideoOverlayImageAsync(string? url, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }
            string filePath = await _backgroundService.GetBackgroundFileAsync(url, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            using var fs = File.OpenRead(filePath);
            VideoOverlayImageSource = new Bitmap(fs);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Prepare video overlay image");
        }
    }


    private async Task ChangeAccentColorToImageFileAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            var color = await Task.Run(async () =>
            {
                string filePath = await _backgroundService.GetBackgroundFileAsync(url, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    return (Color?)null;
                }
                using var bitmap = new Bitmap(filePath);
                return GetAccentColorFromBitmap(bitmap);
            }, cancellationToken);
            if (color is not null)
            {
                await Dispatcher.UIThread.InvokeAsync(() => AccentColorHelper.ChangeAppAccentColor(color));
                AppConfig.AccentColor = $"#{color.Value.R:X2}{color.Value.G:X2}{color.Value.B:X2}";
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Change accent color to image file");
        }
    }


    /// <summary>
    /// 把图片等比缩到 ≤256×256 后再取平均色，等价上游直接对解码像素取样。
    /// </summary>
    private static Color? GetAccentColorFromBitmap(Bitmap bitmap)
    {
        const int MAX = 256;
        int width = bitmap.PixelSize.Width;
        int height = bitmap.PixelSize.Height;
        double scale = Math.Min(1, MAX / (double)Math.Max(width, height));
        int w = Math.Max(1, (int)(width * scale));
        int h = Math.Max(1, (int)(height * scale));

        using var rtb = new RenderTargetBitmap(new PixelSize(w, h));
        using (var ctx = rtb.CreateDrawingContext())
        {
            ctx.DrawImage(bitmap, new Rect(0, 0, w, h));
        }
        int stride = w * 4;
        var buffer = new byte[stride * h];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            rtb.CopyPixels(new PixelRect(0, 0, w, h), handle.AddrOfPinnedObject(), buffer.Length, stride);
        }
        finally
        {
            handle.Free();
        }
        return AccentColorHelper.GetAccentColor(buffer, w, h);
    }




    private void OnBackgroundChanged(object _, BackgroundChangedMessage message)
    {
        _ = UpdateBackgroundAsync(message.GameBackground);
    }


    private void OnMainWindowStateChanged(object _, MainWindowStateChangedMessage message)
    {
        try
        {
            if (_videoPlayer is not null)
            {
                if (message.Activate && !_videoPlayer.IsRunning)
                {
                    _videoPlayer.Resume();
                }
                else if (message.Hide || message.SessionLock)
                {
                    _videoPlayer.Pause();
                }
            }
        }
        catch { }
    }


    private void OnVideoBgVolumeChanged(object _, VideoBgVolumeChangedMessage message)
    {
        // ffmpeg 以 -an 输出，视频背景无音轨；保留消息响应以对齐上游 API，音量不生效（docs/09）。
    }



    #endregion




    #region Video

    /// <summary>
    /// 停止视频背景
    /// </summary>
    private async Task StopVideoAsync()
    {
        await _videoSemaphore.WaitAsync();
        try
        {
            if (_videoPlayer is not null)
            {
                await _videoPlayer.StopAsync();
                VideoHost.Content = null;
            }
            IsVideoPlaying = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stop video");
        }
        finally
        {
            _videoSemaphore.Release();
        }
    }


    private void StopVideo()
    {
        try
        {
            _videoPlayer?.Stop();
            VideoHost.Content = null;
            IsVideoPlaying = false;
        }
        catch { }
    }


    private void DisposeVideoResource()
    {
        _videoPlayer?.Dispose();
        _videoPlayer = null;
        VideoOverlayImageSource = null;
    }




    /// <summary>
    /// 添加子背景（上游用于多窗口/多屏背景），这里保持 API 对齐。
    /// </summary>
    public void AddChild(AppBackground child)
    {
        child.CurrentGameId = CurrentGameId;
        _children.Add(child);
    }


    public void RemoveChild(AppBackground child)
    {
        _children.Remove(child);
    }


    public void Dispose()
    {
        StopVideo();
        DisposeVideoResource();
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    #endregion

}
