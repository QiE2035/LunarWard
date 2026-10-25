using LunarWard.App.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LunarWard.App.ViewModels.Starward;
using LunarWard.Core;
using LunarWard.Core.HoYoPlay;
using LunarWard.Installer;

namespace LunarWard.App.Views.Starward.Pages;

public partial class GameLauncherPage : StarwardPage
{
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };

    private GameLauncherPageViewModel? _viewModel;


    public GameLauncherPage(PageContext context) : base(context)
    {
        InitializeComponent();
        _viewModel = new GameLauncherPageViewModel(context);
        DataContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.InstallDialogRequested += OnInstallDialogRequested;
        _viewModel.PredownloadDialogRequested += OnPredownloadDialogRequested;
        _viewModel.SettingDialogRequested += OnSettingDialogRequested;
        _viewModel.LocateGameRequested += OnLocateGameRequested;
        DragDrop.SetAllowDrop(RootGrid, true);
        RootGrid.AddHandler(DragDrop.DragEnterEvent, OnRootGridDragOver);
        RootGrid.AddHandler(DragDrop.DragOverEvent, OnRootGridDragOver);
        RootGrid.AddHandler(DragDrop.DragLeaveEvent, OnRootGridDragLeave);
        RootGrid.AddHandler(DragDrop.DropEvent, OnRootGridDrop);
        _viewModel.BackgroundImages.CollectionChanged += (_, _) => RebuildPips();
        _viewModel.RefreshBackgroundImages();
        if (CaptureMode.ShowBackgroundBar)
        {
            _viewModel.ShowBackgroundBar = true;
        }

        if (CaptureMode.ShowDragOverlay)
        {
            _viewModel.IsDragOverlayVisible = true;
        }

        if (CaptureMode.InstallProgress)
        {
            _viewModel.ShowInstallProgressForCapture(
                CaptureMode.InstallUpdate ? GameInstallOperation.Update : GameInstallOperation.Install);
        }
        else if (CaptureMode.InstallRealPath is { Length: > 0 } realInstallPath)
        {
            StartRealInstallForCapture(realInstallPath);
        }
        else if (CaptureMode.InstallUpdate)
        {
            _viewModel.ShowUpdateButtonForCapture();
        }

        if (CaptureMode.Predownload is { Length: > 0 } predownloadState)
        {
            _viewModel.ShowPredownloadForCapture(predownloadState);
        }

        if (CaptureMode.AuthLogin)
        {
            // 自检：摆出「已拿到通行证 ID + 一次认证失败」的 Flyout，并顺带把 Popup 单独存一张。
            _viewModel.AuthLogin.ShowForCapture("Response status code does not indicate success: 401 (Unauthorized).");
            DispatcherTimer.RunOnce(() => Button_GameAuthLogin.Flyout?.ShowAt(Button_GameAuthLogin), TimeSpan.FromMilliseconds(600));
        }

        if (CaptureMode.InstallDialog && CaptureMode.Path is not null)
        {
            _ = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await Task.Delay(1000);
                if (TopLevel.GetTopLevel(this) is not Window owner)
                {
                    return;
                }
                var dialog = new InstallGameDialog(Context.Services, _viewModel.GameBiz,
                    Context.Shell.CurrentLauncherId, Context.Profile.Profile.Path);
                _ = DialogService.ShowAsync(dialog, owner);
                await Task.Delay(2500);
                CaptureMode.Capture(owner);
            });
        }

        if (CaptureMode.PredownloadDialog && CaptureMode.Path is not null)
        {
            _ = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await Task.Delay(900);
                if (TopLevel.GetTopLevel(this) is not Window owner)
                {
                    return;
                }
                var dialog = new PreDownloadDialog(Context.Services, _viewModel.GameBiz,
                    Context.Shell.CurrentLauncherId, Context.Profile.Profile.Path, capture: true);
                dialog.PrepareCapture(CaptureMode.PredownloadDialogUnavailable);
                _ = DialogService.ShowAsync(dialog, owner);
                await Task.Delay(1200);
                CaptureMode.Capture(owner);
            });
        }

        if (CaptureMode.Dx12IntroDialog && CaptureMode.Path is not null)
        {
            _ = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await Task.Delay(700);
                if (TopLevel.GetTopLevel(this) is not Window owner)
                {
                    return;
                }
                var dialog = new DX12IntroDialog();
                _ = DialogService.ShowAsync(dialog, owner);
                await Task.Delay(900);
                CaptureMode.Capture(owner);
            });
        }


        if (CaptureMode.LauncherSettingDialog && CaptureMode.Path is not null)
        {
            int section = CaptureMode.SettingsSection switch
            {
                "arguments" => 1,
                "background" => 2,
                "packages" => 3,
                _ => 0,
            };
            _ = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await Task.Delay(900);
                if (TopLevel.GetTopLevel(this) is not Window owner)
                {
                    return;
                }
                var dialog = new GameLauncherSettingDialog(Context);
                dialog.SelectSection(section);
                if (section == 0 && CaptureMode.SettingsSection == "network")
                {
                    dialog.ShowNetworkWarningForCapture();
                }
                if (section == 0 && CaptureMode.UninstallWarning)
                {
                    dialog.ShowUninstallWarningForCapture();
                }
                _ = DialogService.ShowAsync(dialog, owner);
                await Task.Delay(1800);
                CaptureMode.Capture(owner);
            });
        }

        RebuildPips();
    }


    /// <summary>
    /// Starward <c>GameLauncherPage.xaml.cs:385 LocateGameAsync</c>：
    /// 选目录 → 网络驱动器就警告、否则写回配置并刷新按钮状态。
    /// </summary>
    private async void OnLocateGameRequested()
    {
        if (_viewModel is null || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = _viewModel.Strings["Launcher.LocateGame"],
            AllowMultiple = false,
        });

        if (folders.FirstOrDefault()?.TryGetLocalPath() is not { Length: > 0 } path)
        {
            return;
        }

        if (LunarWard.Installer.DriveHelper.GetDriveType(path) is DriveType.Network
            && !path.StartsWith("//", StringComparison.Ordinal))
        {
            _viewModel.ReportStatus(_viewModel.Strings["LauncherSetting.NetworkDriveWarning"]);
            return;
        }

        Context.Profile.Profile.Path = path;
        try
        {
            Context.Services.Config.Save(Context.Services.Paths.ConfigFile);
        }
        catch (Exception ex)
        {
            _viewModel.ReportStatus(ex.Message);
            return;
        }

        _viewModel.RefreshInstallPathState();
    }


    /// <summary>
    /// Starward <c>GameLauncherPage.xaml.cs:912</c>：齿轮弹「游戏设置」对话框。
    /// </summary>
    /// <summary>卸载完成后让启动器重新判断「已安装 / 未安装」（源码 UninstallGameAsync 末尾刷新同一个状态机）。</summary>
    /// <summary>DX12「详情」：弹 <see cref="DX12IntroDialog"/>（源码 <c>Button_Details_Click</c>）。</summary>
    private async void OnDx12DetailsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Avalonia.Controls.Window owner)
        {
            return;
        }

        var dialog = new DX12IntroDialog();
        await DialogService.ShowAsync(dialog, owner);
    }


    private void OnGameUninstalled()
    {
        _viewModel?.RefreshInstallPathState();
    }


    private async void OnSettingDialogRequested()
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var dialog = new GameLauncherSettingDialog(Context);
        dialog.SettingsChanged += () => _viewModel?.RefreshNoticeSettings();
        dialog.GameUninstalled += OnGameUninstalled;
        await DialogService.ShowAsync(dialog, owner);
    }


    /// <summary>
    /// 自检：用真实安装任务填「开始游戏」按钮的进度（<c>--install-real &lt;目录&gt;</c>）。
    /// 与安装对话框确认后走同一条 <see cref="GameLauncherPageViewModel.BeginInstallAsync"/>。
    /// </summary>
    private void StartRealInstallForCapture(string installPath)
    {
        if (_viewModel is not { } vm)
        {
            return;
        }

        GameId? gameId = GameId.FromGameBiz(new GameBiz(vm.GameBiz));
        if (gameId is null)
        {
            Console.Error.WriteLine($"INSTALL_REAL_FAIL 未知区服：{vm.GameBiz}");
            return;
        }

        Directory.CreateDirectory(installPath);
        _ = vm.BeginInstallAsync(new GameInstallRequest
        {
            GameBiz = vm.GameBiz,
            GameId = gameId.Id,
            InstallPath = installPath,
            Operation = GameInstallOperation.Install,
            AudioLanguage = AudioLanguage.None,
        });

        if (CaptureMode.InstallInfoPopup)
        {
            // 真实下载下把「游戏信息 / 下载进度」卡片也展开，一张图同时验证两者
            vm.ActionButtonPointerOver = true;
            vm.IsGameInfoPopupOpen = true;
        }
    }


    /// <summary>
    /// Starward 在页面代码里 `new InstallGameDialog(...).ShowAsync()`；
    /// 对话框确认后拿到 <c>GameInstallRequest</c>，交给 ViewModel 起安装任务。
    /// </summary>
    private async void OnInstallDialogRequested()
    {
        if (_viewModel is null || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var dialog = new InstallGameDialog(
            Context.Services,
            _viewModel.GameBiz,
            Context.Shell.CurrentLauncherId,
            Context.Profile.Profile.Path);
        await DialogService.ShowAsync(dialog, owner);
        if (dialog.Request is { } request)
        {
            await _viewModel.BeginInstallAsync(request);
        }
    }


    /// <summary>
    /// Starward <c>PredownloadAsync</c> 里的 <c>new PreDownloadDialog(...).ShowAsync()</c>：
    /// 确认框自己算体积，确认后把 <see cref="GameInstallRequest"/> 交回来起任务。
    /// </summary>
    private async void OnPredownloadDialogRequested(PredownloadDialogArgs args)
    {
        if (_viewModel is null || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var dialog = new PreDownloadDialog(Context.Services, args.GameBiz, args.LauncherId, args.InstallPath);
        await DialogService.ShowAsync(dialog, owner);
        if (dialog.Request is { } request)
        {
            await _viewModel.StartPredownloadAsync(request);
        }
    }


    /// <summary>
    /// Starward StartGameButton 的 PointerEntered：安装中把状态小字换成「暂停/继续/取消」，
    /// 并让上方进度卡保持展开。
    /// </summary>
    /// <summary>Starward <c>PreDownloadButton.Grid_Root_PointerEntered</c>：悬停换图标并弹下载信息。</summary>
    private void OnPredownloadPointerEntered(object? sender, PointerEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PredownloadPointerOver = true;
        }
    }


    private void OnPredownloadPointerExited(object? sender, PointerEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PredownloadPointerOver = false;
        }
    }


    private void OnStartButtonPointerEntered(object? sender, PointerEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.ActionButtonPointerOver = true;
            _viewModel.IsGameInfoPopupOpen = true;
        }
    }


    /// <summary>Starward StartGameButton 的 PointerExited。</summary>
    private void OnStartButtonPointerExited(object? sender, PointerEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.ActionButtonPointerOver = false;
            _viewModel.IsGameInfoPopupOpen = false;
        }
    }


    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GameLauncherPageViewModel.SelectedBackgroundIndex)
            or nameof(GameLauncherPageViewModel.ShowBackgroundBar))
        {
            RebuildPips();
        }
    }


    private void OnRootGridDragOver(object? sender, DragEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var hasFiles = e.DataTransfer?.TryGetFiles()?.Any() == true;
        e.DragEffects = hasFiles ? DragDropEffects.Copy : DragDropEffects.None;
        _viewModel.IsDragOverlayVisible = hasFiles;
        e.Handled = true;
    }


    private void OnRootGridDragLeave(object? sender, DragEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.IsDragOverlayVisible = false;
        }
    }


    private async void OnRootGridDrop(object? sender, DragEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.IsDragOverlayVisible = false;
        var path = e.DataTransfer?.TryGetFiles()?.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (await _viewModel.AdoptBackgroundFileAsync(path).ConfigureAwait(true) is { } error)
        {
            _viewModel.ReportError(error);
        }
    }


    /// <summary>
    /// Starward 的 <c>PipsPager_SwitchBackgroundImage</c>：每个背景一个可点圆点，
    /// 选中的圆点更大更亮（源码里选中/普通字形同号但字号 8/6）。
    /// </summary>
    private void RebuildPips()
    {
        if (_viewModel is null)
        {
            return;
        }

        PipsPanel.Children.Clear();
        for (var i = 0; i < _viewModel.BackgroundImages.Count; i++)
        {
            var index = i;
            var selected = i == _viewModel.SelectedBackgroundIndex;
            var dot = new Avalonia.Controls.Shapes.Ellipse
            {
                Width = selected ? 8 : 6,
                Height = selected ? 8 : 6,
                Fill = new SolidColorBrush(Color.Parse(selected ? "#FFFFFFFF" : "#88FFFFFF")),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            };
            var button = new Button
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(5, 4, 5, 4),
                MinWidth = 0,
                Content = dot,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            };
            button.Click += (_, _) => _viewModel.SelectBackgroundCommand.Execute(index);
            PipsPanel.Children.Add(button);
        }
    }


    private void OnBackgroundBarPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.CycleBackgroundCommand.Execute(e.Delta.Y > 0 ? -1 : 1);
        e.Handled = true;
    }


    private async void OnCopyBackgroundClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel?.CurrentBackground is not { } background)
        {
            return;
        }

        try
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
            {
                return;
            }

            if (await GetBackgroundBitmapAsync(background).ConfigureAwait(true) is { } bitmap)
            {
                await clipboard.SetBitmapAsync(bitmap).ConfigureAwait(true);
            }
            else
            {
                await clipboard.SetTextAsync(background.Value).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            _viewModel.ReportError(ex.Message);
        }
    }


    private async void OnSaveBackgroundClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel?.CurrentBackground is not { } background)
        {
            return;
        }

        try
        {
            if (TopLevel.GetTopLevel(this) is not { } topLevel)
            {
                return;
            }

            var fileName = File.Exists(background.Value)
                ? Path.GetFileName(background.Value)
                : "lunarward-background.png";
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = fileName,
            }).ConfigureAwait(true);
            if (file is null)
            {
                return;
            }

            await using var destination = await file.OpenWriteAsync().ConfigureAwait(true);
            if (File.Exists(background.Value))
            {
                await using var source = File.OpenRead(background.Value);
                await source.CopyToAsync(destination).ConfigureAwait(true);
            }
            else if (await GetBackgroundBitmapAsync(background).ConfigureAwait(true) is { } bitmap)
            {
                bitmap.Save(destination, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            }
        }
        catch (Exception ex)
        {
            _viewModel.ReportError(ex.Message);
        }
    }


    private async Task<Bitmap?> GetBackgroundBitmapAsync(BackgroundSwitchItem background)
    {
        if (background.IsVideo)
        {
            return null;
        }

        if (File.Exists(background.Value))
        {
            return new Bitmap(background.Value);
        }

        return _viewModel is null
            ? null
            : await _viewModel.GetBitmapAsync(background.Value).ConfigureAwait(true);
    }


    protected override void DisposeViewModel(PageContext context)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.InstallDialogRequested -= OnInstallDialogRequested;
            _viewModel.PredownloadDialogRequested -= OnPredownloadDialogRequested;
            _viewModel.Dispose();
        }

        _viewModel = null;
    }


    public override void OnNavigatedTo()
    {
        _clock.Tick += OnClockTick;
        _clock.Start();

        // 自检：--open-daily-note 时在页面挂好后展开便笺 Flyout。
        if (CaptureMode.OpenDailyNote)
        {
            DispatcherTimer.RunOnce(
                () => Button_DailyNote.Flyout?.ShowAt(Button_DailyNote),
                TimeSpan.FromMilliseconds(600));
        }
    }


    public override void OnNavigatedFrom()
    {
        _clock.Stop();
        _clock.Tick -= OnClockTick;
        base.OnNavigatedFrom();
    }


    private void OnClockTick(object? sender, EventArgs e) => _viewModel?.TickRunningTime();


    private void OnPlayTimeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dialog = new PlayTimeStatsDialog(Context.Services, GameLauncherPageViewModel.GameBizOfProfile(Context.Profile));
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            _ = DialogService.ShowAsync(dialog, owner);
        }
    }


    private void OnBannerPointerEntered(object? sender, PointerEventArgs e) => _viewModel?.OnBannerPointerEnter();


    private void OnBannerPointerExited(object? sender, PointerEventArgs e) => _viewModel?.OnBannerPointerExit();


    private void OnBannerTapped(object? sender, TappedEventArgs e) => _viewModel?.OpenBannerLinkCommand.Execute(null);


    /// <summary>
    /// 公告卡滚轮翻页（Starward 用 FlipView，鼠标滚轮即翻页；这里是单张 Image + 计时器，
    /// 需要显式接住滚轮，见用户反馈「左下角轮播的图片滚轮无翻页」）。
    /// </summary>
    private void OnBannerPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_viewModel is null || _viewModel.Banners.Count == 0)
        {
            return;
        }

        e.Handled = true;
        if (e.Delta.Y > 0)
        {
            _viewModel.PreviousBanner();
        }
        else if (e.Delta.Y < 0)
        {
            _viewModel.NextBanner();
        }
    }


    private void OnBackgroundBarPointerEntered(object? sender, PointerEventArgs e) => _viewModel?.OnBackgroundBarPointerEnter();


    private void OnBackgroundBarPointerExited(object? sender, PointerEventArgs e) => _viewModel?.OnBackgroundBarPointerExit();


    /// <summary>便笺 Flyout 打开：绑定 Flyout VM 并刷新一次（Starward 的按钮 Loaded 时拉取）。</summary>
    private void OnDailyNoteFlyoutOpened(object? sender, EventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        DailyNoteFlyoutRoot.DataContext = _viewModel.DailyNote;
        _viewModel.DailyNote.RefreshCommand.Execute(null);

        // Flyout 是独立 Popup，主窗口的 RenderTargetBitmap 拍不到；自检时单独把 PopupRoot 存一张。
        if (CaptureMode.Enabled)
        {
            DispatcherTimer.RunOnce(CaptureFlyout, TimeSpan.FromMilliseconds(800));
        }
    }


    private void OnGameAuthLoginFlyoutOpened(object? sender, EventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        GameAuthLoginFlyoutRoot.DataContext = _viewModel.AuthLogin;
        if (CaptureMode.AuthLogin)
        {
            DispatcherTimer.RunOnce(() => CaptureFlyoutFrom(GameAuthLoginFlyoutRoot), TimeSpan.FromMilliseconds(800));
        }
    }


    private void CaptureFlyout() => CaptureFlyoutFrom(DailyNoteFlyoutRoot);


    private void CaptureFlyoutFrom(Control anchor)
    {
        try
        {
            if (TopLevel.GetTopLevel(anchor) is not { } root)
            {
                Console.WriteLine("FLYOUT_SHOT_FAIL no popup root");
                return;
            }

            var size = new PixelSize(
                Math.Max(1, (int)root.Bounds.Width),
                Math.Max(1, (int)root.Bounds.Height));
            using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
            bitmap.Render(root);
            var path = CaptureMode.Path + ".flyout.png";
            using (var stream = File.Create(path))
            {
                bitmap.Save(stream, new PngBitmapEncoderOptions());
            }

            Console.WriteLine($"SCREENSHOT_OK {path} {size.Width}x{size.Height}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FLYOUT_SHOT_FAIL {ex.GetType().Name}: {ex.Message}");
        }
    }
}
