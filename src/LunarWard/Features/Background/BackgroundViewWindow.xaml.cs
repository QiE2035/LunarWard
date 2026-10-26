using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace LunarWard.Features.Background;

/// <summary>
/// 背景图库窗口（对应上游 <c>Starward/Features/Background/BackgroundViewWindow.xaml.cs</c>，172 行）。
/// </summary>
/// <remarks>
/// 平台替换：WinUI <c>AppWindow</c>/<c>DesktopAcrylicBackdrop</c>/<c>OverlappedPresenter</c> →
/// Avalonia <see cref="Window"/> 属性（尺寸/不可缩放/居中）；<c>ExtendsContentIntoTitleBar</c> →
/// <see cref="Window.ExtendClientAreaToDecorationsHint"/>；<c>RequestedTheme</c> 翻转刷新（WinUI 重绘 bug 的 workaround）
/// 在 Avalonia 无等价问题，删除并登记于 docs/09。
/// </remarks>
public partial class BackgroundViewWindow : Window
{


    public BackgroundViewWindow()
    {
        InitializeComponent();
        InitializeWindow();
        WeakReferenceMessenger.Default.Register<AccentColorChangedMessage>(this, OnAccentColorChanged);
    }



    private void InitializeWindow()
    {
        CanResize = false;
        KeyDown += Content_KeyDown;
        Closed += BackgroundViewWindow_Closed;
    }



    private void OnAccentColorChanged(object _, AccentColorChangedMessage __)
    {
        // WinUI 下强调色变化需要 RequestedTheme 翻转来强制重绘资源；Avalonia 资源是动态的，无需处理。
    }



    private void Content_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Escape)
        {
            Close();
        }
    }


    private void BackgroundViewWindow_Closed(object? sender, EventArgs args)
    {
        try
        {
            WeakReferenceMessenger.Default.UnregisterAll(this);
            KeyDown -= Content_KeyDown;
            Closed -= BackgroundViewWindow_Closed;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }


}
