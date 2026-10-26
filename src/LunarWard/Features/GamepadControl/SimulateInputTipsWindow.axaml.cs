using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using LunarWard.Frameworks;
using LunarWard.Helpers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LunarWard.Features.GamepadControl;

/// <summary>
/// 模拟输入提示窗口：整屏压暗、光标处留一个圆孔，并在左上角显示按键提示。
/// <b>移植自</b> <c>Starward/src/Starward/Features/GamepadControl/SimulateInputTipsWindow.xaml(.cs)</c>。
/// </summary>
/// <remarks>
/// 平台替换（逐条登记于 <c>docs/09-LINUX-DIVERGENCES.md</c> 的「九、手柄增强」）：
/// <list type="bullet">
/// <item>上游用 <c>User32.GetCursorPos</c> + <c>DisplayArea</c> 把窗口铺到光标所在显示器，并用
/// <c>ScreenToClient</c> + <c>GetDpiForWindow</c> 换算客户区坐标 → 本移植用
/// <see cref="X11Input.TryGetCursorPosition"/>（XQueryPointer）取全局坐标；原生 Wayland 下取不到，
/// 退化为窗口中心（日志 Warning，不静默）。窗口尺寸按 <see cref="Window.Screens"/> 的主屏工作区铺满。</item>
/// <item>上游「矩形挖圆孔」是 WinUI Composition 的 <c>CompositionGeometricClip</c>（CanvasGeometry 组合），
/// 本移植用 Avalonia <see cref="CombinedGeometry"/>(<see cref="GeometryCombineMode.Exclude"/>) 每帧重算。</item>
/// <item>上游 LT 扳机触发圆孔放大/缩小的 KeyFrame 动画 → 本移植在 16ms 定时器里做指数缓动插值
/// （<c>RadiusTarget</c> 与 <c>Radius</c> 之间，0.85 的收敛系数 ≈ 上游 600ms 的观感）。</item>
/// <item>上游 <c>User32.ShowWindow(SW_SHOWNOACTIVATE)</c>（显示但不抢焦点）→ Avalonia 没有等价的“不激活显示”，
/// 用 <see cref="Window.Show"/> + 保持 Topmost 近似；不改变前台窗口的诉求在 X11 下无法完全表达（已登记）。</item>
/// <item>上游 <c>WS_EX_TRANSPARENT</c> 点击穿透 → Avalonia 未暴露该窗口样式位，本移植不设点击穿透（已登记）。</item>
/// <item>图标字体：上游 <c>XboxFluent2.ttf</c>（E9CD/E992/E991/E98F/E990/E9A0/E9A3/E9AA/E9A8/E9A4/E9C8）
/// 不在本项目的 AvaloniaResource 里，AXAML 里统一用项目已有 <c>TextBlock Classes="icon"</c>（Segoe Fluent Icons）。</item>
/// </list>
/// </remarks>
public sealed partial class SimulateInputTipsWindow : WindowEx
{

    private const double NormalRadius = 50;

    private const double ExpandedRadius = 260;

    private const double AnimationConvergence = 0.85;


    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };

    private EllipseGeometry? _holeGeometry;

    private RectangleGeometry? _rectangleGeometry;

    private CombinedGeometry? _maskGeometry;

    private CancellationTokenSource? _hideWindowCancellationTokenSource;

    private float _lastLeftTrigger;

    private GamepadButtons _lastViewButton;

    private double _radius = NormalRadius;

    private double _radiusTarget = NormalRadius;


    public SimulateInputTipsWindow()
    {
        this.InitializeComponent();
        InitializeWindow();
        InitializeGeometry();
        _timer.Tick += Timer_Tick;
        this.Closed += SimulateInputTipsWindow_Closed;
    }


    private void InitializeWindow()
    {
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        TransparentBackdrop backdrop = new();
        backdrop.ApplyTo(this);
        CanMaximize = false;
        CanResize = false;
        ShowInTaskbar = false;
        SystemDecorations = global::Avalonia.Controls.WindowDecorations.None;
        Topmost = true;
        if (Screens.Primary is { } screen)
        {
            Position = screen.WorkingArea.Position;
            Width = screen.WorkingArea.Width / screen.Scaling;
            Height = screen.WorkingArea.Height / screen.Scaling;
        }
    }


    private void InitializeGeometry()
    {
        _rectangleGeometry = new RectangleGeometry(new Avalonia.Rect(0, 0, Width, Height));
        _holeGeometry = new EllipseGeometry(new Avalonia.Rect(NormalRadius, NormalRadius, NormalRadius * 2, NormalRadius * 2));
        _maskGeometry = new CombinedGeometry(GeometryCombineMode.Exclude, _rectangleGeometry, _holeGeometry);
        Border_CursorMask.Data = _maskGeometry;
    }


    private void SimulateInputTipsWindow_Closed(object? sender, EventArgs args)
    {
        _timer.Stop();
        this.Closed -= SimulateInputTipsWindow_Closed;
    }


    private void Timer_Tick(object? sender, EventArgs e)
    {
        try
        {
            if (!IsVisible)
            {
                return;
            }
            MoveCursorMaskPosition();
            CursorMaskAnimation();
        }
        catch { }
    }


    private void MoveCursorMaskPosition()
    {
        if (GamepadController.Input is null || !GamepadController.Input.TryGetCursorPosition(out int x, out int y))
        {
            // 原生 Wayland 取不到全局光标位置：把孔固定在窗口中心，避免每帧抖动（docs/09）。
            x = (int)(Position.X + Width / 2);
            y = (int)(Position.Y + Height / 2);
        }
        double scale = RenderScaling > 0 ? RenderScaling : 1;
        double localX = (x - Position.X) / scale;
        double localY = (y - Position.Y) / scale;
        if (_holeGeometry is not null)
        {
            _holeGeometry.Rect = new Avalonia.Rect(localX - _radius, localY - _radius, _radius * 2, _radius * 2);
        }
        if (_rectangleGeometry is not null)
        {
            _rectangleGeometry.Rect = new Avalonia.Rect(0, 0, Width, Height);
        }
    }


    private void CursorMaskAnimation()
    {
        if (!GamepadController.TryGetLatestState(out GamepadState state))
        {
            return;
        }
        const float threshold = 0.2f;
        float trigger = state.LeftTrigger;
        if (trigger >= threshold && _lastLeftTrigger < threshold)
        {
            _radiusTarget = ExpandedRadius;
        }
        else if (trigger < threshold && _lastLeftTrigger >= threshold)
        {
            _radiusTarget = NormalRadius;
        }
        _lastLeftTrigger = trigger;

        GamepadButtons viewButton = state.Buttons & GamepadButtons.View;
        if (viewButton != 0 && (_lastViewButton ^ viewButton).HasFlag(GamepadButtons.View))
        {
            Expander_ButtonHints.IsExpanded = !Expander_ButtonHints.IsExpanded;
        }
        _lastViewButton = viewButton;

        // 上游是 600ms 的 KeyFrame 动画；这里每帧向目标值收敛（16ms 一帧、0.85 系数 → 约 0.3s 到位）。
        if (Math.Abs(_radius - _radiusTarget) > 0.5)
        {
            _radius = _radiusTarget + (_radius - _radiusTarget) * AnimationConvergence;
            MoveCursorMaskPosition();
        }
    }



    public new void Activate()
    {
        try
        {
            _hideWindowCancellationTokenSource?.Cancel();
            if (Screens.Primary is { } screen)
            {
                Position = screen.WorkingArea.Position;
                Width = screen.WorkingArea.Width / screen.Scaling;
                Height = screen.WorkingArea.Height / screen.Scaling;
            }
            _radius = NormalRadius;
            _radiusTarget = NormalRadius;
            MoveCursorMaskPosition();
            base.Show();
            Expander_ButtonHints.Opacity = 1;
            _timer.Start();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "SimulateInputTipsWindow: Activate 失败");
        }
    }



    public new async void Hide()
    {
        try
        {
            _hideWindowCancellationTokenSource?.Cancel();
            _hideWindowCancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = _hideWindowCancellationTokenSource.Token;
            _timer.Stop();
            Expander_ButtonHints.Opacity = 0;
            await Task.Delay(300);
            if (token.IsCancellationRequested)
            {
                return;
            }
            base.Hide();
        }
        catch { }
    }



}
