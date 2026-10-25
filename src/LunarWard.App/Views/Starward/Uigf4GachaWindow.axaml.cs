using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LunarWard.App.ViewModels.Starward;
using LunarWard.Launch.Database;
using LunarWard.Launch.Uigf4;

namespace LunarWard.App.Views.Starward;

/// <summary>
/// UIGF v4.2 导出 / 导入（复刻 Starward <c>Features/Gacha/UIGF/UIGF4GachaWindow.xaml</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 上游从 SQLite（<c>StarwardDatabase.db</c>）读存档、用 <c>StorageProvider</c> 选文件；
/// 这里同样从 SQLite 读（<c>&lt;DataHome&gt;/LunarWardDatabase.db</c>，表结构一致），
/// 由抽卡页每次「更新记录」后写入。
/// </para>
/// <para>
/// 差异：上游窗口不设尺寸（WinUI 默认），这里沿用本仓库对话框的 800×600；
/// 千星奇域（<c>hk4e_ugc</c>）与上游一致：读 <c>GenshinBeyondGachaItem</c> 表、单独成行，
/// 导出/导入都走同一个 <c>GenshinBeyondGachaStore</c>。
/// </para>
/// </remarks>
public sealed partial class Uigf4GachaWindow : Window
{
    private readonly Uigf4GachaWindowViewModel _viewModel;


    public Uigf4GachaWindow(PageContext context, GachaLogStore? store = null)
    {
        InitializeComponent();
        if (CaptureMode.BeyondSeed)
        {
            // 自检：让导出列表里出现千星奇域行（样例写进同一个临时库）。
            Views.Starward.Pages.GenshinBeyondGachaPage.SeedSampleData();
        }
        store ??= GachaLogStore.OpenDefault(context.Services.Paths.DataHome);
        _viewModel = new Uigf4GachaWindowViewModel(new Uigf4Service(
            store,
            ReadVersion(),
            CaptureMode.Enabled
                ? CaptureMode.CreateBeyondStore()
                : Launch.Database.GenshinBeyondGachaStore.OpenDefault(context.Services.Paths.DataHome)));
        DataContext = _viewModel;

        _viewModel.ExportRequested += OnExportRequested;
        _viewModel.SelectFileRequested += OnSelectFileRequested;
    }


    /// <summary>导出：弹保存对话框后写 UTF-8 JSON（上游是 <c>File.WriteAllTextAsync</c>）。</summary>
    private async void OnExportRequested(string json, string suggestedFileName)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }
        try
        {
            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = _viewModel.Strings["Gacha.Uigf.ExportTitle"],
                SuggestedFileName = suggestedFileName,
                FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }],
            });
            if (file is null)
            {
                return;
            }

            await using (var stream = await file.OpenWriteAsync())
            await using (var writer = new StreamWriter(stream))
            {
                await writer.WriteAsync(json);
            }
            _viewModel.NotifyExported(file.Path.LocalPath);
        }
        catch (Exception ex)
        {
            _viewModel.ErrorText = ex.Message;
        }
    }


    /// <summary>导入：选文件后交给 VM 解析（错误由 VM 显示在底部）。</summary>
    private async void OnSelectFileRequested()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }
        try
        {
            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = _viewModel.Strings["Gacha.Uigf.ImportTitle"],
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }],
            });
            if (files.Count == 0)
            {
                return;
            }
            _viewModel.LoadImportFile(files[0].Path.LocalPath);
        }
        catch (Uigf4ImportException ex)
        {
            _viewModel.ErrorText = string.Format(_viewModel.Strings["Gacha.Uigf.Error.InvalidFile"], ex.Path);
        }
        catch (Exception ex)
        {
            _viewModel.ErrorText = ex.Message;
        }
    }


    /// <summary>自检（<c>--uigf-import</c>）：切到导入页并灌一份样例文件，便于截导入版式。</summary>
    public void ShowImportPageForCapture()
    {
        _viewModel.SelectedPage = 1;
        string json = _viewModel.BuildExportJson() ?? "{}";
        _viewModel.LoadImportJson(json, "LunarWard_UIGF_sample.json");
        foreach (var row in _viewModel.ImportItems)
        {
            row.IsChecked = true;
        }
        if (_viewModel.ImportItems.Count > 0)
        {
            // 样例存档是国服（timezone=8），显式写一下顺便验证 NumericUpDown ↔ int 绑定
            _viewModel.ImportItems[0].Timezone = 8;
        }
        _viewModel.AllImportsChecked = true;
    }


    /// <summary>自检：延迟后把自身渲染成 PNG 并退出进程（与其它对话框同构）。</summary>
    public void CaptureSelf(string path, int delayMs)
    {
        DispatcherTimer.RunOnce(() =>
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
                Dispatcher.UIThread.Post(() => Environment.Exit(0));
            }
        }, TimeSpan.FromMilliseconds(delayMs));
    }


    private static string ReadVersion()
    {
        var assembly = typeof(Uigf4GachaWindow).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            int plus = informational.IndexOf('+');
            return plus > 0 ? informational[..plus] : informational;
        }
        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
