using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LunarWard.App.ViewModels.Starward;

namespace LunarWard.App.Views.Starward.Pages;

/// <summary>
/// 游戏设置页（Starward <c>GameSettingPage</c> 的复刻 + LunarWard 追加区）。
/// </summary>
/// <remarks>
/// 视图层只做三件 <see cref="PageContext"/> 覆盖不到的事：文件选择器、剪贴板、单行「浏览…」。
/// 其余逻辑都在 <see cref="GameSettingPageViewModel"/>。
/// </remarks>
public partial class GameSettingPage : StarwardPage
{
    private GameSettingPageViewModel? _viewModel;


    public GameSettingPage(PageContext context) : base(context)
    {
        InitializeComponent();
        _viewModel = new GameSettingPageViewModel(context);
        DataContext = _viewModel;
    }


    public override void OnNavigatedTo()
    {
        // Shell 在切页时会对上一个页面调 OnNavigatedFrom（本页 VM 因此被释放），
        // 但页面实例仍留在它的缓存里——回到本页时重建一次 VM，避免绑定到已释放的对象。
        if (_viewModel is null)
        {
            _viewModel = new GameSettingPageViewModel(Context);
            DataContext = _viewModel;
        }

        _viewModel.OnNavigatedTo();
    }


    protected override void DisposeViewModel(PageContext context)
    {
        _viewModel?.Dispose();
        _viewModel = null;
    }


    // ── 「+ 从文件添加」 ──────────────────────────────────────────────

    private async void OnAddProgramClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }

        try
        {
            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = _viewModel.Strings["GameSetting.AddFromFile"],
                AllowMultiple = false,
                FileTypeFilter = ProgramFileTypes,
            });

            if (files.Count == 0)
            {
                return;
            }

            if (files[0].TryGetLocalPath() is { Length: > 0 } path)
            {
                _viewModel.AddProgramFromPath(path);
            }
        }
        catch (Exception ex)
        {
            _viewModel.Log.Warn($"选择外部程序失败：{ex.Message}");
        }
    }


    // ── 行内「浏览…」 ────────────────────────────────────────────────

    private async void OnBrowseProgramClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null
            || sender is not Control { DataContext: GameSettingProgramViewModel program }
            || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }

        try
        {
            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = _viewModel.Strings["GameSetting.ProgramBrowse"],
                AllowMultiple = false,
                FileTypeFilter = ProgramFileTypes,
            });

            if (files.Count == 0)
            {
                return;
            }

            if (files[0].TryGetLocalPath() is { Length: > 0 } path)
            {
                program.Path = path;
            }
        }
        catch (Exception ex)
        {
            _viewModel.Log.Warn($"浏览外部程序失败：{ex.Message}");
        }
    }


    private static IReadOnlyList<FilePickerFileType> ProgramFileTypes { get; } =
    [
        new FilePickerFileType("Windows executable") { Patterns = ["*.exe", "*.dll"] },
        new FilePickerFileType("Executable") { Patterns = ["*"] },
    ];


    // ── 「粘贴 launch-report 分析」 ───────────────────────────────────

    private async void OnPasteReportClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        try
        {
            var text = await clipboard.TryGetTextAsync();
            _viewModel.AnalyzePastedReport(text);
        }
        catch (Exception ex)
        {
            _viewModel.Log.Warn($"读取剪贴板失败：{ex.Message}");
        }
    }
}
