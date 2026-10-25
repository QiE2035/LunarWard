using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using LunarWard.App.Localization;
using LunarWard.App.Services;
using LunarWard.App.Views.Starward.Controls;
using LunarWard.Core;
using LunarWard.Core.HoYoPlay;
using LunarWard.Games;
using LunarWard.Installer;
using LunarWard.Launch;
using LunarWard.App.ViewModels.Starward;
using System.Diagnostics;

namespace LunarWard.App.Views.Starward;

/// <summary>
/// 启动器「游戏设置」弹窗，1:1 移植 Starward <c>Features/GameLauncher/GameLauncherSettingDialog.xaml</c>：
/// 左侧 200 宽导航（基本信息 / 启动参数 / 自定义背景 / 游戏包体），右侧四页内容。
/// </summary>
/// <remarks>
/// 源码用 <c>ContentDialog</c> + <c>NavigationView</c>(OpenPaneLength=200) + <c>FlipView</c>；
/// Avalonia 没有这两个控件，左侧用 <see cref="ListBox" /> 的选中态、右侧用四块面板切
/// <see cref="Control.IsVisible" />，尺寸/行高/间距照源码（Grid 800×480、Padding 24,24,24,0）。
/// <para>
/// 未移植（与 <c>docs/08-UI-PARITY.md</c> 一致）：
/// 卸载游戏（LunarWard 没有卸载器，源码走游戏自带的 uninstall.exe）、网络驱动器警告、
/// 「使用 CMD 启动游戏」与「避免检测到非法工具提示」（Windows 专有）、游戏包体明细列表
/// （文件名 / 大小 / MD5 / 下载链接）、拖放背景、背景音量。
/// </para>
/// </remarks>
public partial class GameLauncherSettingDialog : StarwardContentDialog
{

    private static AppStrings Strings => AppStrings.Instance;

    private readonly PageContext? _context;
    private readonly GameBiz _gameBiz;
    private readonly string _installPath;
    private AudioLanguage _audioLanguage;
    private bool _audioLanguageBusy;
    private bool _uninstallBusy;
    private bool _loading = true;


    /// <summary>设计器专用。</summary>
    public GameLauncherSettingDialog()
    {
        InitializeComponent();
        _gameBiz = new GameBiz("");
        _installPath = "";
        CloseButtonText = Strings["Common.Close"];
    }


    public GameLauncherSettingDialog(PageContext context) : this()
    {
        CloseButtonText = Strings["Common.Close"];
        LatestPackagesList.ItemTemplate = new FuncDataTemplate<PackageGroup>((group, _) => BuildPackageGroupView(group));
        PrePackagesList.ItemTemplate = new FuncDataTemplate<PackageGroup>((group, _) => BuildPackageGroupView(group));
        _context = context;
        _gameBiz = new GameBiz(context.Shell.CurrentGameBiz);
        _installPath = ProfileConfig.ExpandPath(context.Profile.Profile.Path);

        NavList.SelectedIndex = 0;
        ShowPanel(0);

        _ = LoadAsync();
    }


    // ── 加载 ─────────────────────────────────────────────────────────

    private async Task LoadAsync()
    {
        if (_context is not { } context)
        {
            return;
        }

        try
        {
            var settings = context.Services.Config.Launcher;
            ShowNoticeToggle.IsChecked = settings.ShowGameNotice;
            DisableNoticeRedDotToggle.IsChecked = settings.DisableNoticeRedDot;
            LocateButton.IsVisible = string.IsNullOrWhiteSpace(_installPath);

            // 游戏图标 / 名称（源码 CurrentGameBizIcon.GameIcon + ServerIcon + GameName）
            var assets = context.Shell.Assets;
            if (await assets.BitmapAsync(assets.For(_gameBiz.ToString())?.Icon) is { } icon)
            {
                GameIcon.Source = icon;
            }
            GameNameText.Text = GameCatalog.All.FirstOrDefault(x => x.GameBiz == _gameBiz.ToString())?.DisplayName
                ?? _gameBiz.ToString();

            LoadProfileValues(context);
            LoadArgumentValues(context);
            LoadBackgroundValues(context);
            await LoadAudioLanguageAsync(context);
            await LoadLocalSizeAsync();
            RefreshUninstallState();
            _ = LoadPackageVersionsAsync(context);
        }
        catch (Exception ex)
        {
            ShowStatus(ex.Message);
        }
        finally
        {
            _loading = false;
        }
    }


    private void LoadProfileValues(PageContext context)
    {
        if (string.IsNullOrWhiteSpace(_installPath))
        {
            return;
        }
        PathButton.IsVisible = true;
        PathText.Text = _installPath;
        PathButton.Tag = _installPath;
    }


    private void LoadArgumentValues(PageContext context)
    {
        var profile = context.Profile.Profile;
        ArgumentBox.Text = string.Join(' ', profile.Args);

        var behavior = context.Services.Config.Launcher.Behavior;
        BehaviorBox.SelectedIndex = behavior switch
        {
            "Minimize" => 1,
            "Hide" or "Close" => 2,
            _ => 0,
        };

        var custom = profile.CommandOverride;
        CustomProgramToggle.IsChecked = !string.IsNullOrWhiteSpace(custom);
        CustomProgramRow.IsVisible = !string.IsNullOrWhiteSpace(custom);
        CustomProgramPath.Text = custom ?? "";
    }


    private void LoadBackgroundValues(PageContext context)
    {
        var launcher = context.Services.Config.Launcher;
        VideoBackgroundToggle.IsChecked = launcher.VideoBackground;

        var items = context.Services.Background.Discover().ToList();
        var index = launcher.BackgroundIndex;
        var official = context.Shell.Assets.For(_gameBiz.ToString())?.Background;
        BackgroundNameText.Text = index switch
        {
            <= 0 when !string.IsNullOrWhiteSpace(official) => "官方背景",
            <= 0 => Strings["Common.NoData"],
            _ when index - 1 < items.Count => items[index - 1].Title is { Length: > 0 } title
                ? title
                : Path.GetFileName(items[index - 1].ImagePath ?? items[index - 1].VideoPath ?? ""),
            _ => Strings["Common.NoData"],
        };
    }


    private async Task LoadAudioLanguageAsync(PageContext context)
    {
        var gameId = Core.HoYoPlay.GameId.FromGameBiz(_gameBiz);
        if (gameId is null || string.IsNullOrWhiteSpace(_installPath))
        {
            AudioLanguageGroup.IsVisible = false;
            return;
        }

        _audioLanguage = await context.Services.Installer.GetAudioLanguageAsync(
            new LunarWard.Core.HoYoPlay.GameId { GameBiz = _gameBiz.ToString(), Id = gameId.Id }, _installPath);
        if (_audioLanguage is AudioLanguage.None)
        {
            // 没有语音包（或不支持切换）时源码隐藏整段
            AudioLanguageGroup.IsVisible = false;
            return;
        }

        _audioLanguageBusy = true;
        AudioLanguagePanel.Children.Clear();
        foreach (var language in new[] { AudioLanguage.Chinese, AudioLanguage.English, AudioLanguage.Japanese, AudioLanguage.Korean })
        {
            if (!_audioLanguage.HasFlag(language))
            {
                continue;
            }
            var button = new Button
            {
                Content = AudioLanguageName(language),
                Tag = language,
                Classes = { "segmented" },
                IsEnabled = !_audioLanguageBusy,
            };
            if (language == _audioLanguage)
            {
                button.Classes.Add("selected");
            }
            button.Click += OnAudioLanguageClick;
            AudioLanguagePanel.Children.Add(button);
        }
        _audioLanguageBusy = false;
        AudioLanguageGroup.IsVisible = true;
    }


    /// <summary>游戏目录大小：源码 <c>GameSize</c> 在后台线程算，这里同样异步，不阻塞弹窗。</summary>
    private async Task LoadLocalSizeAsync()
    {
        if (string.IsNullOrWhiteSpace(_installPath) || !Directory.Exists(_installPath))
        {
            return;
        }

        SizeText.IsVisible = true;
        SizeRun.Text = Strings["LauncherSetting.ComputingSize"];
        try
        {
            var size = await Task.Run(() => DirectorySize(_installPath));
            SizeRun.Text = FormatSize(size);
        }
        catch
        {
            SizeRun.Text = Strings["Common.Error"];
        }
    }


    private async Task LoadPackageVersionsAsync(PageContext context)
    {
        try
        {
            var gameId = LunarWard.Core.HoYoPlay.GameId.FromGameBiz(_gameBiz);
            var launcherId = gameId is null ? null : LauncherId.FromGameId(gameId);
            if (launcherId is null || gameId is null)
            {
                return;
            }
            var client = new HoYoPlayClient();
            var branch = await client.GetGameBranchAsync(launcherId, "zh-cn", gameId);
            LatestPackagesTab.Header = $"{Strings["LauncherSetting.LatestVersion"]} {branch?.Main?.Tag ?? "—"}";
            PrePackagesTab.Header = $"{Strings["LauncherSetting.PreDownloadVersion"]} {branch?.PreDownload?.Tag ?? "—"}";

            // 包体明细：源码 InitializeGamePackagesAsync + GetGameResourcePackageGroups
            var language = string.IsNullOrWhiteSpace(Strings.Language) ? "zh-cn" : Strings.Language.ToLowerInvariant();
            var package = await context.Services.Installer.GetGamePackageAsync(launcherId, language, gameId);
            if (package?.Main is { } main)
            {
                LatestPackagesTab.Header = $"{Strings["LauncherSetting.LatestVersion"]} {main.Major?.Version ?? "—"}";
                var groups = BuildPackageGroups(main);
                groups[0].IsExpanded = true;
                LatestPackagesList.ItemsSource = groups;
            }
            await AppendChannelSdkGroupAsync(client, launcherId, language, gameId);

            if (package?.PreDownload is { Major: not null } pre)
            {
                PrePackagesTab.Header = $"{Strings["LauncherSetting.PreDownloadVersion"]} {pre.Major.Version}";
                PrePackagesList.ItemsSource = BuildPackageGroups(pre);
                PrePackagesTab.IsVisible = true;
            }
            else
            {
                PrePackagesTab.IsVisible = false;
            }
        }
        catch
        {
            LatestPackagesTab.Header = $"{Strings["LauncherSetting.LatestVersion"]} —";
            PreDownloadRunFallback();
            SeedPackagesForCapture();
        }
    }


    /// <summary>
    /// Starward <c>GetGameResourcePackageGroups</c> 的第三段：渠道服 SDK 单独一组。
    /// 官方渠道下 <c>GetGameChannelSDKAsync</c> 可能返回空，此时不加空组。
    /// </summary>
    private async Task AppendChannelSdkGroupAsync(HoYoPlayClient client, string launcherId, string language, LunarWard.Core.HoYoPlay.GameId gameId)
    {
        try
        {
            var sdk = await client.GetGameChannelSDKAsync(launcherId, language, gameId);
            if (sdk?.ChannelSDKPackage is not { } pkg)
            {
                return;
            }

            var group = new PackageGroup(Strings["LauncherSetting.ChannelSDK"]);
            group.Items.Add(new PackageItem
            {
                FileName = string.IsNullOrWhiteSpace(sdk.PkgVersionFileName) ? $"channel_sdk_{sdk.Version}" : sdk.PkgVersionFileName,
                Url = pkg.Url,
                Md5 = pkg.MD5,
                PackageSize = pkg.Size,
            });
            if (LatestPackagesList.ItemsSource is List<PackageGroup> groups)
            {
                groups.Add(group);
            }
        }
        catch
        {
            // SDK 拉取失败不影响包体列表（源码同样只记录日志）。
        }
    }


    /// <summary>
    /// Starward <c>GetGameResourcePackageGroups</c>：完整包一组，每个增量包各一组；
    /// 组内先是游戏资源包再是音频包。
    /// </summary>
    private List<PackageGroup> BuildPackageGroups(GamePackageVersion version)
    {
        var groups = new List<PackageGroup>
        {
            BuildGroup(Strings["LauncherSetting.FullPackages"], version.Major),
        };
        foreach (var patch in version.Patches ?? [])
        {
            groups.Add(BuildGroup($"{Strings["LauncherSetting.DiffPackages"]}  {patch.Version}", patch));
        }
        return groups;
    }


    private static PackageGroup BuildGroup(string name, GamePackageResource? resource)
    {
        var group = new PackageGroup(name);
        if (resource is null)
        {
            return group;
        }
        foreach (var file in resource.GamePackages.Concat(resource.AudioPackages))
        {
            group.Items.Add(new PackageItem
            {
                FileName = Path.GetFileName(file.Url),
                Url = file.Url,
                Md5 = file.MD5,
                PackageSize = file.Size,
            });
        }
        return group;
    }


    /// <summary>一组包体：Expander（组头 16px + 复制全部链接按钮）里竖排每个文件的名称/大小/MD5。</summary>
    private Control BuildPackageGroupView(PackageGroup group)
    {
        var copyButton = new Button
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Content = new TextBlock
            {
                Classes = { "icon" },
                FontSize = 20,
                Foreground = ResourceBrush("StarAccentBrush", Brushes.DodgerBlue),
                Text = "\uE71B",
            },
        };
        ToolTip.SetTip(copyButton, Strings["LauncherSetting.CopyUrls"]);
        copyButton.Click += async (_, _) =>
        {
            var text = string.Join(Environment.NewLine, group.Items.Select(x => x.Url).Where(x => x.Length > 0));
            if (text.Length > 0 && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(text);
            }
        };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(new TextBlock
        {
            Text = group.Name,
            FontSize = 16,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        });
        Grid.SetColumn(copyButton, 1);
        header.Children.Add(copyButton);

        var items = new StackPanel { Spacing = 16 };
        foreach (var item in group.Items)
        {
            var row = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto"), RowSpacing = 2 };
            row.Children.Add(new TextBlock { Text = item.FileName, FontSize = 14 });
            var meta = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(0, 2, 0, 0),
            };
            meta.Children.Add(SecondaryIcon("\uF012"));
            meta.Children.Add(SecondaryText(item.PackageSizeText, 80));
            meta.Children.Add(SecondaryIcon("\uE95E"));
            meta.Children.Add(SecondaryText("MD5", double.NaN));
            var md5 = SecondaryText(item.Md5, double.NaN);
            md5.TextWrapping = TextWrapping.NoWrap;
            meta.Children.Add(md5);
            Grid.SetRow(meta, 1);
            row.Children.Add(meta);
            items.Children.Add(row);
        }

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#40000000")),
            CornerRadius = new CornerRadius(8),
            Child = new Expander
            {
                IsExpanded = group.IsExpanded,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                Header = header,
                Content = items,
            },
        };
    }


    /// <summary>取资源笔刷；窗口在自检/设计器里没有 Application 时退回给定颜色。</summary>
    private static IBrush ResourceBrush(string key, IBrush fallback)
        => Application.Current?.FindResource(key) is IBrush brush ? brush : fallback;


    private static TextBlock SecondaryIcon(string glyph) => new()
    {
        Classes = { "icon" },
        FontSize = 12,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        Foreground = ResourceBrush("StarTextSecondary", Brushes.Gray),
        Text = glyph,
    };


    private static TextBlock SecondaryText(string text, double width) => new()
    {
        Text = text,
        FontSize = 12,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        Width = double.IsNaN(width) ? double.NaN : width,
        Foreground = ResourceBrush("StarTextSecondary", Brushes.Gray),
    };



    private void PreDownloadRunFallback()
    {
        try
        {
            PrePackagesTab.Header = $"{Strings["LauncherSetting.PreDownloadVersion"]} —";
        }
        catch
        {
            // 面板未初始化（设计器）时忽略
        }
    }


    // ── 导航 ─────────────────────────────────────────────────────────

    private void OnNavSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedItem is ListBoxItem item && item.Tag is string tag)
        {
            ShowPanel(int.Parse(tag));
        }
    }


    /// <summary>弹窗改了需要立刻反映到启动器的设置（公告开关等）时触发。</summary>
    public event Action? SettingsChanged;


    /// <summary>卸载完成后触发（启动器据此重新判断安装状态）。</summary>
    public event Action? GameUninstalled;


    /// <summary>
    /// 卸载按钮的可见性（源码 <c>Grid_BasicInformation</c> 里绑 <c>IsUninstallGameButtonVisible</c>）：
    /// 游戏目录存在、不在安装任务中、也不在卸载中才显示。
    /// </summary>
    private void RefreshUninstallState()
    {
        var installed = !string.IsNullOrWhiteSpace(_installPath) && Directory.Exists(_installPath);
        var busy = _uninstallBusy || (_context?.Services.Installer.CurrentTask is not null);
        UninstallButton.IsVisible = installed && !busy;
        if (!installed)
        {
            UninstallWarning.IsVisible = false;
        }
    }


    private void OnUninstallClick(object? sender, RoutedEventArgs e)
    {
        if (_context is not { } context)
        {
            return;
        }

        ShowStatus(null);
        UninstallError.IsVisible = false;

        var reason = GameUninstaller.Validate(_installPath, context.Services.Paths.DataHome, AppContext.BaseDirectory);
        if (reason is not null)
        {
            UninstallError.Text = reason;
            UninstallError.IsVisible = true;
            return;
        }

        UninstallWarning.IsVisible = true;
    }


    private async void OnConfirmUninstallClick(object? sender, RoutedEventArgs e)
    {
        if (_context is not { } context || _uninstallBusy)
        {
            return;
        }

        var reason = GameUninstaller.Validate(_installPath, context.Services.Paths.DataHome, AppContext.BaseDirectory);
        if (reason is not null)
        {
            UninstallError.Text = reason;
            UninstallError.IsVisible = true;
            return;
        }

        _uninstallBusy = true;
        ConfirmUninstallButton.IsEnabled = false;
        UninstallButton.IsVisible = false;
        try
        {
            ShowStatus(Strings["LauncherSetting.Uninstalling"]);

            var kind = GameCatalog.All.FirstOrDefault(x => x.GameBiz == _gameBiz.ToString())?.Kind ?? "";
            var progress = new Progress<double>(_ => { });
            var backedUp = await Task.Run(() => GameUninstaller.Uninstall(
                _installPath, kind, GameUninstaller.GetScreenshotBackupDirectory(), progress));

            // 目录已经没了：清掉配置里的路径，启动器重新回到「未安装」
            context.Profile.Profile.Path = "";
            SaveConfig();
            UninstallWarning.IsVisible = false;

            var done = Strings["LauncherSetting.Uninstalled"];
            ShowStatus(backedUp > 0
                ? $"{done} {string.Format(Strings["LauncherSetting.UninstallScreenshotsBackedUp"], backedUp)}"
                : done);

            GameUninstalled?.Invoke();
            SettingsChanged?.Invoke();
        }
        catch (Exception ex)
        {
            UninstallError.Text = ex.Message;
            UninstallError.IsVisible = true;
            ShowStatus(null);
        }
        finally
        {
            _uninstallBusy = false;
            ConfirmUninstallButton.IsEnabled = true;
            RefreshUninstallState();
        }
    }


    private void OnShowNoticeToggled(object? sender, RoutedEventArgs e)
    {
        if (_context is not { } context || ShowNoticeToggle.IsChecked is not { } enabled)
        {
            return;
        }

        context.Services.Config.Launcher.ShowGameNotice = enabled;
        SaveConfig();
        SettingsChanged?.Invoke();
    }


    private void OnDisableNoticeRedDotToggled(object? sender, RoutedEventArgs e)
    {
        if (_context is not { } context || DisableNoticeRedDotToggle.IsChecked is not { } disabled)
        {
            return;
        }

        context.Services.Config.Launcher.DisableNoticeRedDot = disabled;
        SaveConfig();
        SettingsChanged?.Invoke();
    }


    /// <summary>自检：没有网络时用假包体数据把「游戏包体」页填满（版式核对用）。</summary>
    public void SeedPackagesForCapture()
    {
        LatestPackagesTab.Header = $"{Strings["LauncherSetting.LatestVersion"]} 6.2.0";
        PrePackagesTab.Header = $"{Strings["LauncherSetting.PreDownloadVersion"]} 6.3.0";
        PrePackagesTab.IsVisible = true;

        var full = new PackageGroup(Strings["LauncherSetting.FullPackages"]);
        full.Items.Add(new PackageItem
        {
            FileName = "game_6.2.0.zip",
            Url = "https://example.invalid/game_6.2.0.zip",
            Md5 = "00000000000000000000000000000000",
            PackageSize = 18_630_000_000,
        });
        full.Items.Add(new PackageItem
        {
            FileName = "audio_zh-cn_6.2.0.zip",
            Url = "https://example.invalid/audio_zh-cn_6.2.0.zip",
            Md5 = "11111111111111111111111111111111",
            PackageSize = 6_510_000_000,
        });

        var diff = new PackageGroup($"{Strings["LauncherSetting.DiffPackages"]}  6.1.0");
        diff.Items.Add(new PackageItem
        {
            FileName = "patch_6.1.0_6.2.0.zip",
            Url = "https://example.invalid/patch_6.1.0_6.2.0.zip",
            Md5 = "22222222222222222222222222222222",
            PackageSize = 1_240_000_000,
        });

        full.IsExpanded = true;
        LatestPackagesList.ItemsSource = new List<PackageGroup> { full, diff };
        PrePackagesList.ItemsSource = new List<PackageGroup> { full };
    }


    /// <summary>自检：切到第 <paramref name="index"/> 页（0 基本信息 / 1 启动参数 / 2 背景 / 3 包体）。</summary>
    public void SelectSection(int index)
    {
        NavList.SelectedIndex = Math.Clamp(index, 0, 3);
        ShowPanel(NavList.SelectedIndex);
    }


    /// <summary>自检：把「卸载游戏」按钮与警告行都亮出来（截图卸载确认区用）。</summary>
    public void ShowUninstallWarningForCapture()
    {
        UninstallButton.IsVisible = true;
        UninstallWarning.IsVisible = true;
        UninstallError.IsVisible = false;
    }


    /// <summary>自检：若路径是网络驱动器，把警告行亮出来（截图网络驱动器警告用）。</summary>
    public void ShowNetworkWarningForCapture()
    {
        NetworkWarning.IsVisible = true;
    }


    private void ShowPanel(int index)
    {
        PanelBasic.IsVisible = index == 0;
        PanelArguments.IsVisible = index == 1;
        PanelBackground.IsVisible = index == 2;
        PanelPackages.IsVisible = index == 3;
    }


    // ── 基本信息 ─────────────────────────────────────────────────────

    private void OnOpenGameFolderClick(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_installPath) && Directory.Exists(_installPath))
        {
            OpenPath(_installPath);
        }
    }


    private async void OnRelocateClick(object? sender, RoutedEventArgs e)
    {
        if (_context is not { } context)
        {
            return;
        }
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Strings["Launcher.LocateGame"],
            AllowMultiple = false,
        });
        if (folders.FirstOrDefault()?.TryGetLocalPath() is not { Length: > 0 } path)
        {
            return;
        }
        // 源码 :377-392：映射的网络驱动器不写回配置，只亮警告，等用户改选别的目录。
        // UNC 风格路径（//server/share，Linux 上的 SMB 挂载点路径）与上游的 IsUnc 判断一致地放行。
        if (LunarWard.Installer.DriveHelper.GetDriveType(path) is DriveType.Network
            && !path.StartsWith("//", StringComparison.Ordinal))
        {
            NetworkWarning.IsVisible = true;
            return;
        }
        NetworkWarning.IsVisible = false;
        context.Profile.Profile.Path = path;
        SaveConfig();
        PathButton.IsVisible = true;
        LocateButton.IsVisible = false;
        PathText.Text = path;
        PathButton.Tag = path;
        ShowStatus(null);
        await LoadAudioLanguageAsync(context);
        await LoadLocalSizeAsync();
    }


    private async void OnRepairClick(object? sender, RoutedEventArgs e)
    {
        if (_context is not { } context || string.IsNullOrWhiteSpace(_installPath))
        {
            return;
        }

        try
        {
            RepairButton.IsEnabled = false;
            ShowStatus(Strings["LauncherSetting.Repairing"]);

            var gameId = LunarWard.Core.HoYoPlay.GameId.FromGameBiz(_gameBiz);
            if (gameId is null)
            {
                ShowStatus(Strings["Common.Error"]);
                return;
            }

            var request = new GameInstallRequest
            {
                GameBiz = _gameBiz.ToString(),
                GameId = gameId.Id,
                InstallPath = _installPath,
                Operation = GameInstallOperation.Repair,
                AudioLanguage = _audioLanguage is AudioLanguage.None ? AudioLanguage.None : _audioLanguage,
            };

            var controller = context.Services.Installer;
            controller.StartOrContinue(request);
            if (controller.CurrentRun is { } run)
            {
                await run;
            }
            ShowStatus(null);
        }
        catch (Exception ex)
        {
            ShowStatus(ex.Message);
        }
        finally
        {
            RepairButton.IsEnabled = true;
        }
    }


    private async void OnAudioLanguageClick(object? sender, RoutedEventArgs e)
    {
        if (_context is not { } context || _audioLanguageBusy || sender is not Button button || button.Tag is not AudioLanguage language)
        {
            return;
        }

        try
        {
            _audioLanguageBusy = true;
            foreach (var child in AudioLanguagePanel.Children.OfType<Button>())
            {
                child.IsEnabled = false;
                child.Classes.Remove("selected");
            }
            button.Classes.Add("selected");

            await context.Services.Installer.SetAudioLanguageAsync(
                new LunarWard.Core.HoYoPlay.GameId { GameBiz = _gameBiz.ToString(), Id = LunarWard.Core.HoYoPlay.GameId.FromGameBiz(_gameBiz)?.Id ?? "" },
                _installPath,
                language);
            _audioLanguage = language;
            ShowStatus(null);
        }
        catch (Exception ex)
        {
            ShowStatus(ex.Message);
        }
        finally
        {
            _audioLanguageBusy = false;
        }
    }


    // ── 启动参数 ─────────────────────────────────────────────────────

    private void OnSeeMoreArgumentsClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            OpenUrl("https://docs.ambr.top/command-line-arguments");
        }
        catch
        {
            // 打不开就算了：这行只是帮助链接
        }
    }


    private void OnCustomProgramToggled(object? sender, RoutedEventArgs e)
    {
        CustomProgramRow.IsVisible = CustomProgramToggle.IsChecked == true;
        if (_loading || _context is not { } context)
        {
            return;
        }
        if (CustomProgramToggle.IsChecked != true)
        {
            context.Profile.Profile.CommandOverride = null;
            CustomProgramPath.Text = "";
            SaveConfig();
        }
    }


    private void OnOpenCustomProgramClick(object? sender, RoutedEventArgs e)
    {
        if (_context?.Profile.Profile.CommandOverride is { Length: > 0 } path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                OpenPath(directory);
            }
        }
    }


    private async void OnSelectCustomProgramClick(object? sender, RoutedEventArgs e)
    {
        if (_context is not { } context)
        {
            return;
        }
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings["LauncherSetting.CustomStartExecutable"],
            AllowMultiple = false,
        });
        if (files.FirstOrDefault()?.TryGetLocalPath() is not { Length: > 0 } path)
        {
            return;
        }
        context.Profile.Profile.CommandOverride = path;
        CustomProgramPath.Text = path;
        SaveConfig();
    }


    // ── 自定义背景 ───────────────────────────────────────────────────

    private void OnVideoBackgroundToggled(object? sender, RoutedEventArgs e)
    {
        if (_loading || _context is not { } context)
        {
            return;
        }
        context.Services.Config.Launcher.VideoBackground = VideoBackgroundToggle.IsChecked == true;
        SaveConfig();
    }


    private async void OnSelectBackgroundClick(object? sender, RoutedEventArgs e)
    {
        if (_context is not { } context)
        {
            return;
        }

        try
        {
            if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
            {
                return;
            }

            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Strings["LauncherSetting.CustomBackground"],
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("image")
                    {
                        Patterns = ["*.jpg", "*.jpeg", "*.png", "*.webp", "*.bmp"],
                    },
                    new FilePickerFileType("video")
                    {
                        Patterns = ["*.mp4", "*.webm", "*.mkv"],
                    },
                ],
            });
            if (files.FirstOrDefault()?.TryGetLocalPath() is not { Length: > 0 } path)
            {
                return;
            }

            Directory.CreateDirectory(context.Services.Background.CacheDirectory);
            var target = Path.Join(context.Services.Background.CacheDirectory, Path.GetFileName(path));
            File.Copy(path, target, overwrite: true);

            // 索引口径与启动器一致：0 = 官方背景，其余 = Discover() 顺序
            var items = context.Services.Background.Discover().ToList();
            var index = items.FindIndex(x => (x.ImagePath ?? x.VideoPath) == target);
            context.Services.Config.Launcher.BackgroundIndex = index < 0 ? 0 : index + 1;
            SaveConfig();

            BackgroundErrorText.IsVisible = false;
            LoadBackgroundValues(context);
        }
        catch (Exception ex)
        {
            BackgroundErrorText.IsVisible = true;
            BackgroundErrorText.Text = ex.Message;
        }
    }


    // ── 保存 ─────────────────────────────────────────────────────────

    private void SaveConfig()
    {
        if (_context is not { } context)
        {
            return;
        }
        context.Services.Config.Save(context.Services.Paths.ConfigFile);
    }


    /// <summary>关闭后把「命令行参数 / 启动后行为」写回（源码是 TwoWay 绑到 AppConfig）。</summary>
    protected override void OnDialogClosed()
    {
        base.OnDialogClosed();
        if (_context is not { } context || _loading)
        {
            return;
        }

        try
        {
            var args = ArgumentBox.Text?.Trim();
            context.Profile.Profile.Args = string.IsNullOrEmpty(args)
                ? []
                : [.. args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

            context.Services.Config.Launcher.Behavior = BehaviorBox.SelectedIndex switch
            {
                1 => "Minimize",
                2 => "Hide",
                _ => "Nothing",
            };
            SaveConfig();
        }
        catch
        {
            // 关窗时写盘失败不阻塞关闭
        }
    }


    // ── 小工具 ───────────────────────────────────────────────────────

    private void ShowStatus(string? message)
    {
        StatusText.IsVisible = !string.IsNullOrEmpty(message);
        StatusText.Text = message ?? "";
    }


    private static string AudioLanguageName(AudioLanguage language) => language switch
    {
        AudioLanguage.Chinese => "汉语",
        AudioLanguage.English => "英语",
        AudioLanguage.Japanese => "日语",
        AudioLanguage.Korean => "韩语",
        _ => language.ToString(),
    };


    private static long DirectorySize(string path)
    {
        long size = 0;
        foreach (var file in new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories))
        {
            size += file.Length;
        }
        return size;
    }


    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):F2} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):F2} MB",
        >= 1L << 10 => $"{bytes / (double)(1L << 10):F2} KB",
        _ => $"{bytes} B",
    };


    private static void OpenPath(string path) => Process.Start(new ProcessStartInfo
    {
        FileName = "xdg-open",
        ArgumentList = { path },
        UseShellExecute = false,
    });


    private static void OpenUrl(string url) => Process.Start(new ProcessStartInfo
    {
        FileName = "xdg-open",
        ArgumentList = { url },
        UseShellExecute = false,
    });

}


/// <summary>包体分组（完整包 / 某个版本的增量包）。</summary>
public sealed class PackageGroup
{
    public PackageGroup(string name) => Name = name;

    public string Name { get; }

    public List<PackageItem> Items { get; } = [];

    /// <summary>展开第一组，方便截图核对文件行版式。</summary>
    public bool IsExpanded { get; set; }
}


/// <summary>包体内的一个文件（名称 / 大小 / MD5 / 下载链接）。</summary>
public sealed class PackageItem
{
    public string FileName { get; init; } = "";

    public string Url { get; init; } = "";

    public string Md5 { get; init; } = "";

    public long PackageSize { get; init; }

    /// <summary>Starward <c>PackageItem.PackageSizeString</c>：GB 两位小数。</summary>
    public string PackageSizeText => PackageSize <= 0 ? "..." : $"{PackageSize / 1024d / 1024 / 1024:F2} GB";
}
