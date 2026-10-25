using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using LunarWard.App.Localization;
using LunarWard.App.Services;
using LunarWard.Core;
using LunarWard.Core.HoYoPlay;
using LunarWard.Installer;
using System.IO;

namespace LunarWard.App.Views.Starward;

/// <summary>
/// 预下载确认对话框，1:1 移植 Starward <c>Features/GameInstall/PreDownloadDialog</c>：
/// 标题、三行体积（安装包 / 解压所需 / 可用空间）、两条 Caution 警告、右下「开始预下载」、右上关闭。
/// </summary>
/// <remarks>
/// 体积口径与源码一致：有匹配本地版本的增量包就用增量包，否则用 <c>pre_download.major</c>；
/// 两条警告分别是「有补丁包但都不匹配本地版本」与「预下载不可用」。
/// 源码在 Chord 下载模式下还会额外解一遍 manifest 计算真实体积，这里直接读 package 的 Size 字段
/// （与 <see cref="InstallGameDialog"/> 同一取舍，已记进 parity 文档）。
/// </remarks>
public partial class PreDownloadDialog : Window
{
    private static AppStrings Strings => AppStrings.Instance;

    private readonly GameInstallController _installer;
    private readonly string _gameBiz;
    private readonly string _launcherId;
    private readonly string _language;
    private readonly GameId _gameId;
    private readonly string _installPath;

    private GamePackage? _package;
    private string? _localVersion;
    private AudioLanguage _audioLanguage = AudioLanguage.None;


    /// <summary>点过「开始预下载」后要发出去的请求；关闭后由调用方读取。</summary>
    public GameInstallRequest? Request { get; private set; }


    /// <summary>点过「开始预下载」时触发（调用方也可以读 <see cref="Request"/>）。</summary>
    public event Action<GameInstallRequest>? Confirmed;


    /// <summary>设计器专用。</summary>
    public PreDownloadDialog()
    {
        InitializeComponent();
        _installer = null!;
        _gameBiz = "";
        _launcherId = "";
        _language = "zh-cn";
        _gameId = new GameId();
        _installPath = "";
    }


    public PreDownloadDialog(AppServices services, string gameBiz, string launcherId, string installPath, bool capture = false)
    {
        InitializeComponent();
        _installer = services.Installer;
        _gameBiz = gameBiz;
        _launcherId = launcherId;
        _language = string.IsNullOrWhiteSpace(Strings.Language) ? "zh-cn" : Strings.Language.ToLowerInvariant();
        _gameId = GameId.FromGameBiz(new GameBiz(gameBiz)) ?? new GameId { GameBiz = gameBiz };
        _installPath = installPath;

        NoPatchesText.Text = Strings["PreDownloadDialog.NoPatches"];
        UnavailableText.Text = Strings["PreDownloadDialog.Unavailable"];

        // 自检截图时不去碰真实安装目录 / 网络，由 PrepareCapture 填假数据
        if (!capture)
        {
            _ = LoadAsync();
        }
    }


    private async Task LoadAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_installPath))
            {
                ShowUnavailable();
                return;
            }

            if (await _installer.GetLocalGameVersionAsync(_installPath).ConfigureAwait(true) is not { } local)
            {
                ShowUnavailable();
                return;
            }
            _localVersion = local.ToString();

            _package = await _installer.GetGamePackageAsync(_launcherId, _language, _gameId).ConfigureAwait(true);
            var pre = _package?.PreDownload;
            if (pre is null)
            {
                ShowUnavailable();
                return;
            }

            _audioLanguage = await _installer.GetAudioLanguageAsync(_gameId, _installPath).ConfigureAwait(true);

            var patch = pre.Patches?.FirstOrDefault(x => string.Equals(x.Version, _localVersion, StringComparison.OrdinalIgnoreCase));
            var hasPatch = pre.Patches is { Count: > 0 };
            // 有补丁包但都不匹配本地版本 → 提醒先更新（源码 _hasPatch && !_canPatch）
            NoPatchesText.IsVisible = hasPatch && patch is null;

            if (patch is null && pre.Major is null)
            {
                ShowUnavailable();
                return;
            }

            ComputeSizes(patch ?? pre.Major!);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"PreDownloadDialog load failed: {ex.Message}");
            ShowUnavailable();
        }
    }


    /// <summary>Starward <c>ComputePackageSizeAsync</c>：资源包 + 已装音频包的体积；装不下可用空间时标警示色。</summary>
    private void ComputeSizes(GamePackageResource resource)
    {
        long size = resource.GamePackages.Sum(x => x.Size);
        long unzip = resource.GamePackages.Sum(x => x.DecompressedSize);

        foreach (var language in Enum.GetValues<AudioLanguage>())
        {
            if (language is AudioLanguage.None or AudioLanguage.All || !_audioLanguage.HasFlag(language))
            {
                continue;
            }
            if (resource.AudioPackages.FirstOrDefault(x => string.Equals(x.Language, AudioDescription(language), StringComparison.OrdinalIgnoreCase)) is { } audio)
            {
                size += audio.Size;
                unzip += audio.DecompressedSize;
            }
        }

        var available = DriveHelper.GetDriveAvailableSpace(_installPath);

        PackageSizeValue.Text = FormatBytes(size);
        UnzipSpaceValue.Text = FormatBytes(unzip);
        AvailableSpaceValue.Text = FormatBytes(available);
        AvailableSpaceValue.Foreground = available > 0 && unzip > available
            ? (IBrush?)Application.Current?.FindResource("StarCautionBrush") ?? Brushes.OrangeRed
            : (IBrush?)Application.Current?.FindResource("StarTextSecondaryBrush") ?? Brushes.Gray;

        StartButton.IsEnabled = size > 0 && unzip > 0 && !string.IsNullOrWhiteSpace(_localVersion);
    }


    private void ShowUnavailable()
    {
        UnavailableText.IsVisible = true;
        StartButton.IsEnabled = false;
    }


    private static string AudioDescription(AudioLanguage language) => language switch
    {
        AudioLanguage.Chinese => "zh-cn",
        AudioLanguage.English => "en-us",
        AudioLanguage.Japanese => "ja-jp",
        AudioLanguage.Korean => "ko-kr",
        _ => "",
    };


    private static string FormatBytes(long bytes) => bytes <= 0 ? "..." : $"{bytes / 1024d / 1024 / 1024:F2} GB";


    private void OnStartClick(object? sender, RoutedEventArgs e)
    {
        StartButton.IsEnabled = false;
        Request = new GameInstallRequest
        {
            GameBiz = _gameBiz,
            GameId = _gameId.Id,
            InstallPath = _installPath,
            Operation = GameInstallOperation.Predownload,
            AudioLanguage = _audioLanguage,
        };
        Confirmed?.Invoke(Request);
        Close();
    }


    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();


    // ── 自检截图（--predownload-dialog）────────────────────────────────

    /// <summary>
    /// 自检专用：没有真实预下载包时把界面填成「可开始」或「不可用」的样子。
    /// </summary>
    /// <param name="unavailable">true 时展示「预下载不可用」与无补丁提醒。</param>
    public void PrepareCapture(bool unavailable)
    {
        if (unavailable)
        {
            PackageSizeValue.Text = "...";
            UnzipSpaceValue.Text = "...";
            AvailableSpaceValue.Text = "...";
            UnavailableText.IsVisible = true;
            NoPatchesText.IsVisible = true;
            StartButton.IsEnabled = false;
            return;
        }

        PackageSizeValue.Text = "18.63 GB";
        UnzipSpaceValue.Text = "24.71 GB";
        AvailableSpaceValue.Text = "455.68 GB";
        StartButton.IsEnabled = true;
    }


    /// <summary>把窗口渲染成 PNG 并退出进程（与其它自检窗口同构，必须在 UI 线程渲染）。</summary>
    public void CaptureSelf(string path, int delayMs = 400)
    {
        Avalonia.Threading.DispatcherTimer.RunOnce(() =>
        {
            try
            {
                var size = new PixelSize(Math.Max(1, (int)Bounds.Width), Math.Max(1, (int)Bounds.Height));
                using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
                bitmap.Render(this);
                using (var stream = File.Create(path))
                {
                    bitmap.Save(stream, new PngBitmapEncoderOptions());
                }
                Console.WriteLine($"SCREENSHOT_OK {path} {size.Width}x{size.Height}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SCREENSHOT_FAIL {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (Application.Current?.ApplicationLifetime
                    is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown(0);
                }
            }
        }, TimeSpan.FromMilliseconds(delayMs));
    }
}
