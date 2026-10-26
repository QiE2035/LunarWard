using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using LunarWard.Core;
using LunarWard.Language;
using LunarWard.Views.Starward.Controls;
using Microsoft.Extensions.Logging;

namespace LunarWard.Features.GameSetting;

/// <summary>
/// 原神 HDR 亮度设置。<b>移植自</b>
/// <c>Starward/src/Starward/Features/GameSetting/GenshinHDRLuminanceSettingDialog.xaml(.cs)</c>。
/// 平台替换：Win2D <c>CanvasSwapChainPanel</c> HDR 线性增益预览 → 普通 Image + 透明度近似；
/// WinRT <c>DisplayInformation</c>（HDR 能力/峰值亮度）→ 无对应 API，仅显示分辨率/刷新率。
/// </summary>
public sealed partial class GenshinHDRLuminanceSettingDialog : StarwardContentDialog, INotifyPropertyChanged
{

    private readonly ILogger<GenshinHDRLuminanceSettingDialog> _logger = AppConfig.GetLogger<GenshinHDRLuminanceSettingDialog>();


    public GameBiz CurrentGameBiz { get; set; }


    public GenshinHDRLuminanceSettingDialog()
    {
        this.InitializeComponent();
        this.Loaded += GenshinHDRLuminanceSettingDialog_Loaded;
    }




    public new event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        OnPropertyChanged(name);
        return true;
    }



    private async void GenshinHDRLuminanceSettingDialog_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            // 平台替换：Linux 无 DisplayInformation，HDR 状态一律按「不可用」处理
            UpdateDisplayInfomation();
            (MaxLuminance, SceneLuminance, UILuminance) = await GameSettingService.GetGenshinHDRLuminanceAsync(CurrentGameBiz);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load Genshin HDR luminance");
        }
    }




    public bool HDRNotSupported { get; set => SetProperty(ref field, value); }

    public bool HDRNotEnabled { get; set => SetProperty(ref field, value); }

    public bool HDREnabled { get; set => SetProperty(ref field, value); }

    public string DisplayInfomation { get; set => SetProperty(ref field, value); } = "";

    public bool MoraVisibility { get; set => SetProperty(ref field, value); } = true;



    public int MaxLuminance
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(MoraPreviewOpacity));
            }
        }
    } = 1000;


    public int SceneLuminance
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(ScenePreviewOpacity));
            }
        }
    } = 300;


    public int UILuminance
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(ScenePreviewOpacity));
            }
        }
    } = 350;


    /// <summary>平台替换：Win2D 线性增益预览 → 透明度近似（0.2 ~ 1.0）。</summary>
    public double MoraPreviewOpacity => Math.Clamp(MaxLuminance / 2000d, 0.2d, 1d);

    /// <summary>平台替换：Win2D 线性增益预览 → 透明度近似（0.2 ~ 1.0）。</summary>
    public double ScenePreviewOpacity => Math.Clamp((SceneLuminance + UILuminance) / 1400d, 0.2d, 1d);



    private void UpdateDisplayInfomation()
    {
        try
        {
            HDRNotEnabled = false;
            HDRNotSupported = false;
            HDREnabled = false;
            // 平台替换：Linux 下没有 WinRT 高级颜色信息，只显示屏幕基本信息
            var screen = TopLevel.GetTopLevel(this)?.Screens.Primary;
            if (screen is not null)
            {
                DisplayInfomation = $"{screen.Bounds.Width} × {screen.Bounds.Height} @ {screen.Scaling:0.##}x";
            }
            else
            {
                DisplayInfomation = "N/A";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update display information");
        }
    }




    [RelayCommand]
    private async Task Save()
    {
        try
        {
            await GameSettingService.SetGenshinHDRLuminanceAsync(CurrentGameBiz, MaxLuminance, SceneLuminance, UILuminance);
            this.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Save Genshin HDR luminance");
        }
    }



    [RelayCommand]
    private void Reset()
    {
        MaxLuminance = 1000;
        SceneLuminance = 300;
        UILuminance = 350;
    }



    [RelayCommand]
    private void AutoAdjust()
    {
        // 平台替换：WinRT DisplayInformation 提供峰值亮度/SDR 白点，Linux 无对应 API。
        // 按钮在 HDREnabled=false 时本就禁用，这里保留上游默认值逻辑。
        MaxLuminance = Math.Clamp(1000, 300, 2000);
        SceneLuminance = Math.Clamp(300, 100, 500);
        UILuminance = Math.Clamp(350, 150, 550);
    }



    [RelayCommand]
    private void CloseDialog()
    {
        this.Close();
    }


}
