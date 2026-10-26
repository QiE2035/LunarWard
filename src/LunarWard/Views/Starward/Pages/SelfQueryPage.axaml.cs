using LunarWard.Services;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LunarWard.ViewModels.Starward;
using LunarWard.Core.SelfQuery;

namespace LunarWard.Views.Starward.Pages;

/// <summary>
/// 账号记录页（复刻 Starward <c>Features/SelfQuery/SelfQueryPage</c>）。
/// </summary>
/// <remarks>
/// 视图里只有源码同样放在 code-behind 的三件事：「输入 URL」对话框（Starward 用 ContentDialog）、
/// 「删除本月数据」确认框（同上），以及首次挂载后触发 <see cref="SelfQueryPageViewModel.ActivateAsync"/>。
/// 其余全部是 MVVM 绑定。
/// </remarks>
public partial class SelfQueryPage : StarwardPage
{
    private readonly SelfQueryPageViewModel _viewModel;


    public SelfQueryPage(PageContext context) : base(context)
    {
        InitializeComponent();
        _viewModel = new SelfQueryPageViewModel(context);
        DataContext = _viewModel;
        _viewModel.InputURLRequested += OnInputUrlRequested;
        _viewModel.DeleteCurrentMonthRequested += OnDeleteCurrentMonthRequested;
    }


    public override void OnNavigatedTo()
    {
        base.OnNavigatedTo();
        _ = _viewModel.ActivateAsync();
        if (CaptureMode.SelfQueryInputUrl && CaptureMode.Path is { } path)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                await Task.Delay(900);
                if (TopLevel.GetTopLevel(this) is Window owner)
                {
                    var dialog = BuildInputUrlDialog();
                    _ = dialog.ShowDialog(owner);
                    dialog.CaptureSelf(path + ".dialog.png", 1800);
                }
            });
        }
    }


    protected override void DisposeViewModel(PageContext context)
    {
        _viewModel.InputURLRequested -= OnInputUrlRequested;
        _viewModel.DeleteCurrentMonthRequested -= OnDeleteCurrentMonthRequested;
        _viewModel.Dispose();
    }


    // ── 输入 URL（源码 InputURLAsync 里的 ContentDialog）────────────────

    private async void OnInputUrlRequested()
    {
        try
        {
            if (TopLevel.GetTopLevel(this) is not Window owner)
            {
                return;
            }
            var dialog = BuildInputUrlDialog();
            await dialog.ShowDialog(owner);
            if (dialog.Confirmed && !string.IsNullOrWhiteSpace(dialog.Url))
            {
                await _viewModel.InitializeByUrlAsync(dialog.Url);
            }
        }
        catch (Exception ex)
        {
            _viewModel.Log.Error("Input url dialog", ex);
        }
    }


    /// <summary>
    /// 版式对齐源码：标题「输入 URL」+ 步骤说明（次要色）+ 三行步骤 + 注意（警示色）+ 输入框。
    /// </summary>
    private SelfQueryInputUrlWindow BuildInputUrlDialog()
    {
        return new SelfQueryInputUrlWindow(
            _viewModel.Strings["SelfQuery.InputURL"],
            _viewModel.Strings["SelfQuery.StepsToGetURL"],
            _viewModel.Strings["SelfQuery.InputURLSteps"],
            _viewModel.Strings["SelfQuery.InputURLNote"],
            _viewModel.Strings["SelfQuery.Confirm"],
            _viewModel.Strings["Common.Cancel"]);
    }


    // ── 删除本月数据（源码 DeleteCurrentMonthData 里的 ContentDialog）────

    private async void OnDeleteCurrentMonthRequested()
    {
        try
        {
            if (TopLevel.GetTopLevel(this) is not Window owner)
            {
                return;
            }
            var dialog = new SelfQueryConfirmWindow(
                _viewModel.Strings["SelfQuery.DeleteThisMonth"],
                _viewModel.DeleteConfirmText,
                _viewModel.Strings["Common.Delete"],
                _viewModel.Strings["Common.Cancel"],
                destructive: true);
            await dialog.ShowDialog(owner);
            if (dialog.Confirmed)
            {
                _viewModel.DeleteCurrentMonthAsync();
            }
        }
        catch (Exception ex)
        {
            _viewModel.Log.Error("Delete records dialog", ex);
        }
    }

}





/// <summary>「输入 URL」对话框（源码是一个 ContentDialog + StackPanel + TextBox）。</summary>
internal sealed class SelfQueryInputUrlWindow : Window
{

    private readonly TextBox _textBox = new() { Margin = new Thickness(0, 8, 0, 0) };


    public SelfQueryInputUrlWindow(string title, string stepsTitle, string steps, string note, string confirm, string cancel)
    {
        Title = title;
        Width = 460;
        Height = 340;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush.Parse("#FF2C2C2C");

        var stack = new StackPanel { Spacing = 4, Margin = new Thickness(20) };

        // ContentDialog 的 Title → 对话框顶部标题
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 20,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 8),
        });
        stack.Children.Add(new TextBlock
        {
            Text = stepsTitle,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush.Parse("#C5FFFFFF"),
        });
        stack.Children.Add(new TextBlock
        {
            Text = steps,
            TextWrapping = TextWrapping.Wrap,
        });
        stack.Children.Add(new TextBlock
        {
            Text = note,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush.Parse("#FFFFB900"),
        });
        stack.Children.Add(_textBox);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 12, 0, 0),
        };
        var primary = new Button { Content = confirm, IsDefault = true };
        var secondary = new Button { Content = cancel, IsCancel = true };
        primary.Click += (_, _) => Close(true);
        secondary.Click += (_, _) => Close(false);
        buttons.Children.Add(primary);
        buttons.Children.Add(secondary);
        stack.Children.Add(buttons);

        Content = stack;
        _textBox.AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => _textBox.Focus());
    }


    public bool Confirmed { get; private set; }

    public string? Url => _textBox.Text;


    /// <summary>自检：把本对话框渲染成 PNG（与 InstallGameDialog.CaptureSelf 同构）。</summary>
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





/// <summary>确认对话框（删除本月数据用；源码是带 destructive 默认按钮的 ContentDialog）。</summary>
internal sealed class SelfQueryConfirmWindow : Window
{

    public SelfQueryConfirmWindow(string title, string message, string confirm, string cancel, bool destructive = false)
    {
        Title = title;
        Width = 400;
        Height = 200;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush.Parse("#FF2C2C2C");

        var stack = new StackPanel { Spacing = 12, Margin = new Thickness(20) };
        stack.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        var primary = new Button { Content = confirm, IsDefault = !destructive };
        if (destructive)
        {
            primary.Background = Brush.Parse("#FFC42B1C");
        }
        var secondary = new Button { Content = cancel, IsCancel = true };
        primary.Click += (_, _) => Close(true);
        secondary.Click += (_, _) => Close(false);
        buttons.Children.Add(primary);
        buttons.Children.Add(secondary);
        stack.Children.Add(buttons);

        Content = stack;
    }


    public bool Confirmed { get; private set; }

}





/// <summary>源码 <c>SelfQueryStatsNumberConverter</c>：正数 +N、负数 -N、0 显示 "-"。</summary>
public sealed class SelfQueryStatsNumberConverter : IValueConverter
{

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long num)
        {
            return "-";
        }
        if (num > 0)
        {
            return "+" + num.ToString(culture);
        }
        if (num < 0)
        {
            return num.ToString(culture);
        }
        return "-";
    }


    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

}



/// <summary>源码 <c>SelfQueryStatsNumberBrushConverter</c>：正数成功色、负数错误色、0 次要色。</summary>
public sealed class SelfQueryStatsNumberBrushConverter : IValueConverter
{

    private static readonly IBrush Green = Brush.Parse("#FF6CCB5F");

    private static readonly IBrush Red = Brush.Parse("#FFFF99A4");

    private static readonly IBrush Default = Brush.Parse("#C5FFFFFF");


    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long num)
        {
            return Default;
        }
        if (num > 0)
        {
            return Green;
        }
        if (num < 0)
        {
            return Red;
        }
        return Default;
    }


    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

}



/// <summary>源码 <c>WhiteSpaceStringToVisibilityConverter</c>：字符串非空才显示。</summary>
public sealed class SelfQueryStringToVisibilityConverter : IValueConverter
{

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrWhiteSpace(s);


    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

}
