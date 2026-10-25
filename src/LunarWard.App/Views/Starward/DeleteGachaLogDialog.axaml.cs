using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LunarWard.App.ViewModels.Starward;
using LunarWard.App.Views.Starward;
using LunarWard.Launch;

namespace LunarWard.App.Views.Starward;

/// <summary>
/// 删除指定时间段的抽卡记录（复刻 Starward <c>Features/Gacha/DeleteGachaLogDialog.xaml</c>）。
/// </summary>
/// <remarks>
/// 与上游一致的部分：UID 选择、记录数、开始/结束时间（日期 + 24 小时制时间）、
/// 6 个月提示、「删除」按钮里的「确认删除」Flyout、强调色的取消。
/// 与上游一致：确认后直接删 SQLite 里的记录行（<see cref="DeleteGachaLogDialogViewModel"/>），
/// 页面收到 <see cref="DeleteGachaLogDialog.Deleted"/> 后从库里重读。
/// </remarks>
public sealed partial class DeleteGachaLogDialog : Window
{
    private readonly DeleteGachaLogDialogViewModel _viewModel;


    public DeleteGachaLogDialog(
        Starward.PageContext context,
        IReadOnlyList<GachaRecord> records,
        long? defaultUid,
        string gameKind)
    {
        InitializeComponent();
        _viewModel = new DeleteGachaLogDialogViewModel(context, records, defaultUid, gameKind);
        DataContext = _viewModel;
        _viewModel.DeleteConfirmed += (uid, begin, end) => Deleted = (uid, begin, end);
        _viewModel.RequestClose += Close;
    }


    /// <summary>确认删除后的时间段；取消或直接关窗为 null。</summary>
    public (long Uid, DateTime Begin, DateTime End)? Deleted { get; private set; }


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
                if (Application.Current?.ApplicationLifetime
                    is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown(0);
                }
            }
        }, TimeSpan.FromMilliseconds(delayMs));
    }
}
