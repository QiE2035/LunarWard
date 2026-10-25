using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LunarWard.App.ViewModels.Starward;

namespace LunarWard.App.Views.Starward;

/// <summary>
/// 管理截图文件夹（复刻 Starward <c>Features/Screenshot/ScreenshotFolderManageDialog.xaml</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 列表第一行永远是游戏自己的截图目录（不可移除），其余是用户添加的额外目录：
/// 图库会把它们合并显示（<see cref="ViewModels.ScreenshotViewModel.FolderDirectories"/>）。
/// </para>
/// <para>
/// 与上游的一处差异：上游把游戏内目录的截图复制到 <c>Default</c>（游戏设置里指定的备份目录）；
/// LunarWard 没有这一设置，备份目标固定为 <c>~/Pictures/LunarWard</c>（与页面「导出」同一目录），
/// 只复制目标里还没有的文件。结果文案与上游同一句。
/// </para>
/// </remarks>
public sealed partial class ScreenshotFolderManageDialog : Window
{
    private readonly ScreenshotFolderManageDialogViewModel _viewModel;


    public ScreenshotFolderManageDialog()
    {
        InitializeComponent();
        _viewModel = new ScreenshotFolderManageDialogViewModel("", []);
        DataContext = _viewModel;
    }


    public ScreenshotFolderManageDialog(string inGameDirectory, IEnumerable<string> externalFolders)
    {
        InitializeComponent();
        _viewModel = new ScreenshotFolderManageDialogViewModel(inGameDirectory, externalFolders);
        DataContext = _viewModel;
    }


    /// <summary>点「保存」后返回额外目录列表；取消或直接关窗返回 null。</summary>
    public IReadOnlyList<string>? Result => _viewModel.Result;


    private async void OnAddFolderClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = _viewModel.Strings["Screenshot.AddFolder"],
            AllowMultiple = false,
        });

        string? path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            _viewModel.Add(path);
        }
    }


    private void OnOpenFolderClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ScreenshotFolderItem item }
            && !string.IsNullOrWhiteSpace(item.Folder))
        {
            OpenInFileManager(item.Folder);
        }
    }


    private void OnRemoveFolderClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ScreenshotFolderItem item })
        {
            _viewModel.Remove(item);
        }
    }


    private async void OnBackupFolderClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ScreenshotFolderItem item } button)
        {
            return;
        }

        button.IsEnabled = false;
        _viewModel.BackupResult = null;
        _viewModel.IsBackingUp = true;
        try
        {
            string target = Path.Join(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures", "LunarWard");
            int count = await Task.Run(() => CopyMissingFiles(item.Folder, target));
            if (count < 0)
            {
                _viewModel.BackupResult = _viewModel.Strings["Screenshot.BackupFailed"];
            }
            else
            {
                _viewModel.BackupResult = string.Format(
                    CultureInfo.CurrentCulture, _viewModel.Strings["Screenshot.BackedUpNew"], count);
            }
        }
        catch (Exception)
        {
            _viewModel.BackupResult = _viewModel.Strings["Screenshot.BackupFailed"];
        }
        finally
        {
            _viewModel.IsBackingUp = false;
            button.IsEnabled = true;
        }
    }


    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.Save();
        Close();
    }


    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();


    /// <summary>把 <paramref name="source"/> 里目标目录没有的文件复制过去；返回新复制数量，失败返回 -1。</summary>
    private static int CopyMissingFiles(string source, string target)
    {
        if (!Directory.Exists(source) || string.IsNullOrWhiteSpace(target))
        {
            return -1;
        }

        Directory.CreateDirectory(target);
        int count = 0;
        foreach (string file in Directory.GetFiles(source))
        {
            string destination = Path.Join(target, Path.GetFileName(file));
            if (!File.Exists(destination))
            {
                try
                {
                    File.Copy(file, destination);
                    count++;
                }
                catch (Exception)
                {
                    // 单文件失败不打断整批
                }
            }
        }
        return count;
    }


    private void OpenInFileManager(string path)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "xdg-open",
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(path);
            using var process = System.Diagnostics.Process.Start(startInfo);
        }
        catch (Exception)
        {
            // 没有 xdg-open 时静默忽略
        }
    }


    /// <summary>自检截图：渲染本窗口到 PNG 后退出（独立 Window，主窗口截不到）。</summary>
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
                if (Application.Current?.ApplicationLifetime
                    is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown(0);
                }
            }
        }, TimeSpan.FromMilliseconds(delayMs));
    }

}
