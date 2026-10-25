using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using LunarWard.App.Localization;
using LunarWard.App.Services;
using LunarWard.App.Views.Starward.Controls;
using LunarWard.Core;
using LunarWard.Core.HoYoPlay;
using LunarWard.Installer;
using System.IO;

namespace LunarWard.App.Views.Starward;

/// <summary>
/// 安装游戏对话框，1:1 移植 Starward <c>Features/GameInstall/InstallGameDialog</c>：
/// 选择安装路径（可改）、音频语言分段（安装包带音频包时出现）、解压所需/可用空间、
/// 自动创建子文件夹、开始安装、硬链接说明、右上角关闭。
/// </summary>
/// <remarks>
/// Starward 的 <c>ComputePackageSize</c> 走 <c>HoYoPlayService.GetGamePackageAsync</c> 的
/// <c>main.major</c> 各包体积之和；这里沿用同一口径（压缩包 + 音频包的解压体积）。
/// 成功点「开始安装」后由调用方（启动器页）启动 <see cref="GameInstallController"/>。
/// </remarks>
public partial class InstallGameDialog : StarwardContentDialog
{
    private static AppStrings Strings => AppStrings.Instance;

    private readonly GameInstallController _installer;
    private readonly string _gameBiz;
    private readonly string _launcherId;
    private readonly string _language;
    private readonly GameId _gameId;
    private readonly string _baseFolder;

    private GamePackage? _package;
    private string _installationPath = "";
    private long _unzipSpaceBytes;
    private bool _updatingPath;
    private AudioLanguage _audioLanguage = AudioLanguage.None;
    private readonly List<AudioLanguage> _audioLanguages = [];

    /// <summary>点过「开始安装」后的请求；关闭对话框前由调用方读取。</summary>
    public GameInstallRequest? Request { get; private set; }


    /// <summary>设计器专用。</summary>
    public InstallGameDialog()
    {
        InitializeComponent();
        _installer = null!;
        _gameBiz = "";
        _launcherId = "";
        _language = "zh-cn";
        _gameId = new GameId();
        _baseFolder = "";
        CloseButtonText = Strings["Common.Close"];
    }


    public InstallGameDialog(AppServices services, string gameBiz, string launcherId, string? initialPath)
    {
        InitializeComponent();
        _installer = services.Installer;
        _gameBiz = gameBiz;
        _launcherId = launcherId;
        _language = string.IsNullOrWhiteSpace(Strings.Language) ? "zh-cn" : Strings.Language.ToLowerInvariant();
        _gameId = GameId.FromGameBiz(new LunarWard.Core.GameBiz(gameBiz)) ?? new GameId { GameBiz = gameBiz };
        CloseButtonText = Strings["Common.Close"];
        _baseFolder = string.IsNullOrWhiteSpace(initialPath)
            ? Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Games")
            : Path.GetDirectoryName(initialPath.TrimEnd('/')) ?? initialPath;

        AutoSubfolderCheck.IsChecked = true;
        SetInstallationPath(string.IsNullOrWhiteSpace(initialPath)
            ? Path.Join(_baseFolder, _gameBiz)
            : initialPath);

        BuildLanguageBar();
        _ = LoadPackageAsync();
    }


    // ── 路径 ─────────────────────────────────────────────────────────

    private void SetInstallationPath(string path)
    {
        _installationPath = path;
        InstallationPathText.Text = path;
        UpdatePathState();
    }


    private void UpdatePathState()
    {
        if (_updatingPath)
        {
            return;
        }

        var error = ValidatePath(_installationPath);
        ShowError(error);

        long available = 0;
        try
        {
            if (!string.IsNullOrWhiteSpace(_installationPath))
            {
                available = DriveHelper.GetDriveAvailableSpace(Path.GetDirectoryName(_installationPath) ?? _installationPath);
            }
        }
        catch
        {
            // 路径还不存在时忽略
        }

        AvailableSpaceRun.Text = available > 0 ? FormatGigabytes(available) : "...";
        UnzipSpaceRun.Text = _unzipSpaceBytes > 0 ? FormatGigabytes(_unzipSpaceBytes) : "...";

        StartInstallButton.IsEnabled = error is null && _package is not null;
    }


    /// <summary>Starward <c>CheckCanStartInstallation</c>：根目录/网络盘不允许。</summary>
    private string? ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception ex)
        {
            return ex.Message;
        }

        if (DriveHelper.GetDriveType(full) is DriveType.Network && !full.StartsWith("//", StringComparison.Ordinal))
        {
            return Strings["InstallGameDialog.NetworkDrive"];
        }

        if (Path.GetPathRoot(full) == full)
        {
            return Strings["InstallGameDialog.RootDirectory"];
        }

        return null;
    }


    private void ShowError(string? message)
    {
        ErrorMessageText.Text = message ?? "";
        ErrorMessageText.IsVisible = message is not null;
        FreeSpacePanel.IsVisible = message is null;
    }


    private static string FormatGigabytes(long bytes) => $"{bytes / 1024d / 1024d / 1024d:F2} GB";


    // ── 安装包 ───────────────────────────────────────────────────────

    private async Task LoadPackageAsync()
    {
        try
        {
            _package = await _installer.GetGamePackageAsync(_launcherId, _language, _gameId);
            if (_package?.Main?.Major is null)
            {
                ShowError(Strings["InstallGameDialog.PackageFailed"]);
                return;
            }

            ComputePackageSize();
            BuildLanguageBar();
            UpdatePathState();
        }
        catch (Exception ex)
        {
            ShowError(string.Format(Strings["InstallGameDialog.PackageFailedDetail"], ex.Message));
        }
    }


    /// <summary>Starward <c>ComputePackageSize</c>：主包 + 选中音频包的解压体积。</summary>
    private void ComputePackageSize()
    {
        long size = 0;
        var major = _package?.Main?.Major;
        if (major is null)
        {
            _unzipSpaceBytes = 0;
            return;
        }

        foreach (var file in major.GamePackages)
        {
            size += file.DecompressedSize;
        }

        if (_audioLanguages.Count > 0)
        {
            var lang = AudioLanguageToString(_audioLanguage);
            if (!string.IsNullOrEmpty(lang)
                && major.AudioPackages.FirstOrDefault(x => string.Equals(x.Language, lang, StringComparison.OrdinalIgnoreCase)) is { } audio)
            {
                size += audio.DecompressedSize;
            }
        }

        _unzipSpaceBytes = size;
    }


    private void BuildLanguageBar()
    {
        var major = _package?.Main?.Major;
        if (major is null || major.AudioPackages.Count == 0)
        {
            AudioLanguageBar.IsVisible = false;
            return;
        }

        // 语言集合变化时才重建，避免刷新时丢掉选择
        // Starward Segmented_SelectLanguage 的固定顺序：汉 / 英 / 日 / 韩
        var present = major.AudioPackages
            .Select(x => StringToAudioLanguage(x.Language))
            .Where(x => x is not AudioLanguage.None)
            .ToHashSet();
        var languages = new[] { AudioLanguage.Chinese, AudioLanguage.English, AudioLanguage.Japanese, AudioLanguage.Korean }
            .Where(present.Contains)
            .ToList();
        if (languages.Count == 0)
        {
            AudioLanguageBar.IsVisible = false;
            return;
        }

        if (!_audioLanguages.SequenceEqual(languages))
        {
            _audioLanguages.Clear();
            _audioLanguages.AddRange(languages);
            AudioLanguagePanel.Children.Clear();
            foreach (var language in languages)
            {
                var button = new Button
                {
                    Content = AudioLanguageName(language),
                    Tag = language,
                    FontSize = 14,
                    Padding = new Avalonia.Thickness(12, 4),
                    MinHeight = 30,
                    Background = Brushes.Transparent,
                    BorderThickness = default,
                    CornerRadius = new Avalonia.CornerRadius(4),
                };
                button.Click += OnAudioLanguageClick;
                AudioLanguagePanel.Children.Add(button);
            }

            _audioLanguage = languages[0];
        }

        UpdateAudioLanguageButtons();
        AudioLanguageBar.IsVisible = true;
    }


    private void UpdateAudioLanguageButtons()
    {
        foreach (var child in AudioLanguagePanel.Children.OfType<Button>())
        {
            bool selected = child.Tag is AudioLanguage language && language == _audioLanguage;
            child.Background = selected
                ? new SolidColorBrush(Color.Parse("#14FFFFFF"))
                : Brushes.Transparent;
        }
    }


    private void OnAudioLanguageClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AudioLanguage language })
        {
            _audioLanguage = language;
            UpdateAudioLanguageButtons();
            ComputePackageSize();
            UpdatePathState();
        }
    }


    private static AudioLanguage StringToAudioLanguage(string? language) => language?.ToLowerInvariant() switch
    {
        "zh-cn" or "zh" => AudioLanguage.Chinese,
        "en-us" or "en" => AudioLanguage.English,
        "ja-jp" or "ja" => AudioLanguage.Japanese,
        "ko-kr" or "ko" => AudioLanguage.Korean,
        _ => AudioLanguage.None,
    };


    private static string? AudioLanguageToString(AudioLanguage language) => language switch
    {
        AudioLanguage.Chinese => "zh-cn",
        AudioLanguage.English => "en-us",
        AudioLanguage.Japanese => "ja-jp",
        AudioLanguage.Korean => "ko-kr",
        _ => null,
    };


    private static string AudioLanguageName(AudioLanguage language) => language switch
    {
        AudioLanguage.Chinese => Strings["GameSetting.LanguageChinese"],
        AudioLanguage.English => Strings["GameSetting.LanguageEnglish"],
        AudioLanguage.Japanese => Strings["GameSetting.LanguageJapanese"],
        AudioLanguage.Korean => Strings["GameSetting.LanguageKorean"],
        _ => "",
    };


    // ── 交互 ─────────────────────────────────────────────────────────

    private async void OnChangePathClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Strings["InstallGameDialog.SelectPath"],
            AllowMultiple = false,
            SuggestedStartLocation = null,
        });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            SetInstallationPath(AutoSubfolderCheck.IsChecked == true
                ? Path.Join(path, _gameBiz)
                : path);
        }
    }


    private void OnAutoSubfolderClick(object? sender, RoutedEventArgs e)
    {
        if (_updatingPath)
        {
            return;
        }

        _updatingPath = true;
        try
        {
            var parent = Path.GetDirectoryName(_installationPath.TrimEnd('/')) ?? _installationPath;
            SetInstallationPath(AutoSubfolderCheck.IsChecked == true
                ? Path.Join(parent, _gameBiz)
                : parent);
        }
        finally
        {
            _updatingPath = false;
        }

        UpdatePathState();
    }


    private void OnStartInstallClick(object? sender, RoutedEventArgs e)
    {
        if (_package is null || ValidatePath(_installationPath) is not null)
        {
            return;
        }

        Request = new GameInstallRequest
        {
            GameBiz = _gameBiz,
            GameId = _gameId.Id,
            InstallPath = Path.GetFullPath(_installationPath),
            Operation = GameInstallOperation.Install,
            AudioLanguage = _audioLanguages.Count > 0 ? _audioLanguage : AudioLanguage.None,
        };
        Close();
    }
}
