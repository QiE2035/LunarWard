using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LunarWard.App.ViewModels.Starward;

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
        DragDrop.SetAllowDrop(RootGrid, true);
        RootGrid.AddHandler(DragDrop.DragEnterEvent, OnRootGridDragOver);
        RootGrid.AddHandler(DragDrop.DragOverEvent, OnRootGridDragOver);
        RootGrid.AddHandler(DragDrop.DragLeaveEvent, OnRootGridDragLeave);
        RootGrid.AddHandler(DragDrop.DropEvent, OnRootGridDrop);
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
            _viewModel.ShowInstallProgressForCapture();
        }

        if (CaptureMode.InstallDialog && CaptureMode.Path is { } dialogPath)
        {
            _ = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await Task.Delay(1000);
                var dialog = new InstallGameDialog(Context.Services, _viewModel.GameBiz,
                    Context.Shell.CurrentLauncherId, Context.Profile.Profile.Path);
                dialog.ShowDialog(TopLevel.GetTopLevel(this) as Window ?? throw new InvalidOperationException());
                dialog.CaptureSelf(dialogPath + ".dialog.png", 2500);
            });
        }

        RebuildPips();
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

        var dialog = new InstallGameDialog(Context.Services, _viewModel.GameBiz, Context.Shell.CurrentLauncherId, Context.Profile.Profile.Path);
        await dialog.ShowDialog(owner);
        if (dialog.Request is { } request)
        {
            await _viewModel.BeginInstallAsync(request);
        }
    }


    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GameLauncherPageViewModel.SelectedBackgroundIndex))
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
        var dialog = new PlayTimeStatsDialog(Context.Services, Context.Profile.Id);
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            dialog.Show(owner);
        }
        else
        {
            dialog.Show();
        }
    }


    private void OnBannerPointerEntered(object? sender, PointerEventArgs e) => _viewModel?.OnBannerPointerEnter();


    private void OnBannerPointerExited(object? sender, PointerEventArgs e) => _viewModel?.OnBannerPointerExit();


    private void OnBannerTapped(object? sender, TappedEventArgs e) => _viewModel?.OpenBannerLinkCommand.Execute(null);


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


    private void CaptureFlyout()
    {
        try
        {
            if (TopLevel.GetTopLevel(DailyNoteFlyoutRoot) is not { } root)
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
