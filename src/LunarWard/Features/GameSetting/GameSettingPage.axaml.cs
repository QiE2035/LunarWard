using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LunarWard.Core;
using LunarWard.Core.HoYoPlay;
using LunarWard.Features.GameLauncher;
using LunarWard.Features.GameSelector;
using LunarWard.Language;
using LunarWard.Views.Starward;
using LunarWard.Services;
using Microsoft.Extensions.Logging;

namespace LunarWard.Features.GameSetting;

/// <summary>
/// 游戏设置页。<b>移植自</b> <c>Starward/src/Starward/Features/GameSetting/GameSettingPage.xaml(.cs)</c>。
/// 平台替换：WinRT <c>DisplayInformation</c>（HDR 探测）/ <c>DisplayArea</c>（屏幕分辨率）
/// → Avalonia <c>Screens</c>，HDR 探测在 Linux 下无对应 API，暂不置警示（见 docs/09）。
/// <para>
/// 宿主适配：上游继承 <c>Starward.Frameworks.PageBase</c>（WinUI Page，构造后由 Frame 调
/// <c>OnNavigatedTo(GameId)</c>）。本仓库宿主 <c>StarwardShell</c> 只接受
/// <c>StarwardPage</c>（ctor <c>PageContext</c> + 无参 <c>OnNavigatedTo</c>），故改为
/// <c>StarwardPage</c> 子类；<see cref="CurrentGameId"/> / <see cref="CurrentGameBiz"/>
/// 由 <see cref="PageContext.Profile"/> 经 <see cref="LauncherService.BuildGameInfo"/> 推导，
/// 语义与上游 PageBase 一致（见 docs/09-LINUX-DIVERGENCES.md §十一）。
/// </para>
/// </summary>
public sealed partial class GameSettingPage : StarwardPage, INotifyPropertyChanged
{

    private readonly ILogger<GameSettingPage> _logger = AppConfig.GetLogger<GameSettingPage>();

    private readonly GameLauncherService _gameLauncherService = AppConfig.GetService<GameLauncherService>();



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



    public GameSettingPage(PageContext context) : base(context)
    {
        this.InitializeComponent();
        DataContext = this;
    }




    private GameId _currentGameId = null!;

    private GameBiz _currentGameBiz;

    private bool _initialized;


    /// <summary>等价上游 <c>PageBase.CurrentGameId</c>：当前 profile 的游戏 ID。</summary>
    private GameId CurrentGameId => _currentGameId;


    /// <summary>等价上游 <c>PageBase.CurrentGameBiz</c>：当前 profile 的游戏区服。</summary>
    private GameBiz CurrentGameBiz
    {
        get => _currentGameBiz;
        set => _currentGameBiz = value;
    }




    public override void OnNavigatedTo()
    {
        if (_currentGameId is null)
        {
            var launchInfo = LauncherService.BuildGameInfo(Context.Profile.Profile);
            _currentGameBiz = new GameBiz(launchInfo.GameBiz);
            _currentGameId = new GameId { Id = Context.Profile.Id, GameBiz = _currentGameBiz };
        }

        Image_Emoji.Source = CurrentGameBiz.ToGame().Value switch
        {
            GameBiz.bh3 => LoadImage(AppConfig.EmojiAI),
            GameBiz.hk4e => LoadImage(AppConfig.EmojiPaimon),
            GameBiz.hkrpg => LoadImage(AppConfig.EmojiPom),
            GameBiz.nap => LoadImage(AppConfig.EmojiBangboo),
            _ => null,
        };
        if (CurrentGameId.GameBiz == GameBiz.bh3_global)
        {
            CurrentGameBiz = CurrentGameId.Id switch
            {
                "g0mMIvshDb" => GameBiz.bh3_jp,
                "uxB4MC7nzC" => GameBiz.bh3_kr,
                "bxPTXSET5t" => GameBiz.bh3_os,
                "wkE5P5WsIf" => GameBiz.bh3_asia,
                _ => GameBiz.bh3_global,
            };
        }

        if (!_initialized)
        {
            _initialized = true;
            _ = InitializeAsync();
        }
    }


    private async Task InitializeAsync()
    {
        InitializeResolutionItem();
        await InitializeGameSettingAsync();
    }


    public override void OnNavigatedFrom()
    {
        base.OnNavigatedFrom();
    }


    private static Bitmap? LoadImage(Uri uri)
    {
        try
        {
            return System.IO.File.Exists(uri.LocalPath) ? new Bitmap(uri.LocalPath) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }


    public bool IsBaseSettingEnable { get; set => SetProperty(ref field, value); }

    public bool IsLanguageSettingEnable { get; set => SetProperty(ref field, value); }

    public bool IsGraphicsSettingEnable { get; set => SetProperty(ref field, value); }

    public bool IsApplyButtonEnable { get; set => SetProperty(ref field, value); }

    public string ErrorMessage { get; set => SetProperty(ref field, value); } = Lang.GameSettingPage_SettingNotEffect; // 游戏运行时应用的设置无法生效




    public string? StartArgument
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.SetStartArgument(CurrentGameBiz, value);
            }
        }
    }


    public bool EnableFullScreen
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                IsApplyButtonEnable = true;
            }
        }
    }


    public bool UsePopupWindow
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                IsApplyButtonEnable = true;
            }
        }
    }



    public bool EnableCustomResolution { get; set => SetProperty(ref field, value); }


    public int ResolutionWidth
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                IsApplyButtonEnable = true;
            }
        }
    }


    public int ResolutionHeight
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                IsApplyButtonEnable = true;
            }
        }
    }


    public int LanguageIndex
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                IsApplyButtonEnable = true;
            }
        }
    }


    public int StarRailFpsIndex
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                IsApplyButtonEnable = true;
            }
        }
    }


    public bool EnableGenshinHDR
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                IsApplyButtonEnable = true;
            }
        }
    }


    public bool HDRNotSupported { get; set => SetProperty(ref field, value); }

    public bool HDRNotEnabled { get; set => SetProperty(ref field, value); }


    private async Task InitializeGameSettingAsync()
    {
        try
        {
            var localVersion = await _gameLauncherService.GetLocalGameVersionAsync(CurrentGameId);
            if (localVersion is null)
            {
                StackPanel_Emoji.IsVisible = true;
                return;
            }
            IsBaseSettingEnable = true;
            if (CurrentGameBiz.ToGame().Value is GameBiz.hk4e or GameBiz.hkrpg)
            {
                IsLanguageSettingEnable = true;
            }
            if (CurrentGameBiz.Game is GameBiz.hkrpg)
            {
                IsGraphicsSettingEnable = true;
                StackPanel_StarRailFPS.IsVisible = true;
                StarRailFpsIndex = await GameSettingService.GetStarRailFPSIndexAsync(CurrentGameBiz);
            }
            if (CurrentGameBiz.Game is GameBiz.hk4e)
            {
                IsGraphicsSettingEnable = true;
                StackPanel_GenshinHDR.IsVisible = true;
                EnableGenshinHDR = AppConfig.EnableGenshinHDR;
                // 平台替换：上游用 WinRT DisplayInformation 探测显示器 HDR，Linux 无对应 API
                UpdateHdrState();
            }
            StartArgument = AppConfig.GetStartArgument(CurrentGameBiz);
            UsePopupWindow = AppConfig.GetUsePopupWindow(CurrentGameBiz);
            var resolutionSetting = await GameSettingService.GetGameResolutionSettingAsync(CurrentGameBiz);
            if (resolutionSetting != null)
            {
                EnableFullScreen = resolutionSetting.IsFullScreen;
                if (resolutionSetting.Width * resolutionSetting.Height > 0)
                {
                    ResolutionWidth = resolutionSetting.Width;
                    ResolutionHeight = resolutionSetting.Height;
                    EnableCustomResolution = !UpdateResolutionComboBoxSelection(ResolutionWidth, ResolutionHeight);
                }
                else
                {
                    ComboBox_Resolution.SelectedIndex = 0;
                }
            }
            else
            {
                ComboBox_Resolution.SelectedIndex = 0;
            }
            if (IsLanguageSettingEnable)
            {
                var langSetting = await GameSettingService.GetGameVoiceLanguageSettingAsync(CurrentGameBiz);
                if (langSetting != null)
                {
                    LanguageIndex = langSetting.Value;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initialize Game Setting");
        }
        finally
        {
            IsApplyButtonEnable = false;
        }
    }



    private void InitializeResolutionItem()
    {
        var screen = TopLevel.GetTopLevel(this)?.Screens.Primary;
        var width = (int)(screen?.Bounds.Width ?? 1920);
        var height = (int)(screen?.Bounds.Height ?? 1080);
        var list = Resolutions.Where(x => x.Width <= width && x.Height <= height).ToList();
        if (list.Count == 0)
        {
            list.Add((width, height));
        }
        else
        {
            if (list[0].Width != width || list[0].Height != height)
            {
                list.Add((width, height));
            }
        }
        foreach (var item in list)
        {
            ComboBox_Resolution.Items.Add(new ComboBoxItem
            {
                Content = $"{item.Width} × {item.Height}",
            });
        }
    }




    private static readonly List<(int Width, int Height)> Resolutions = new List<(int Width, int Height)>()
    {
        (3840 , 2160),
        (2560 , 1600),
        (2560 , 1440),
        (2048 , 1536),
        (1920 , 1440),
        (1920 , 1200),
        (1920 , 1080),
        (1680 , 1050),
        (1600 , 1200),
        (1440 , 900 ),
        (1280 , 960 ),
        (1280 , 800 ),
        (1280 , 720 ),
        (1152 , 864 ),
        (1024 , 768 ),
        (800  , 600 ),
        (640  , 480 ),
    };



    private void ComboBox_Resolution_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Cast<object>().FirstOrDefault() is ComboBoxItem item)
        {
            if (item.Content is string str)
            {
                var split = str.Split('×', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (split.Length == 2)
                {
                    if (int.TryParse(split[0], out int width) && int.TryParse(split[1], out int height))
                    {
                        ResolutionWidth = width;
                        ResolutionHeight = height;
                        IsApplyButtonEnable = true;
                    }
                }
            }
        }
    }



    private bool UpdateResolutionComboBoxSelection(int width, int height)
    {
        foreach (ComboBoxItem item in ComboBox_Resolution.Items)
        {
            if (item.Content is string str)
            {
                var split = str.Split('×', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (split.Length == 2)
                {
                    if (int.TryParse(split[0], out int _width) && int.TryParse(split[1], out int _height))
                    {
                        if (width == _width && height == _height)
                        {
                            ComboBox_Resolution.SelectedItem = item;
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }


    [RelayCommand]
    private async Task OpenGenshinHDRLumianceSettingWindow()
    {
        try
        {
            WeakReferenceMessenger.Default.Send(new MainWindowDragRectAdaptToGameIconMessage(true));
            var dialog = new GenshinHDRLuminanceSettingDialog { CurrentGameBiz = this.CurrentGameBiz };
            await DialogService.ShowAsync(dialog, this);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
        finally
        {
            WeakReferenceMessenger.Default.Send(new MainWindowDragRectAdaptToGameIconMessage());
        }
    }



    [RelayCommand]
    private async Task ApplySetting()
    {
        try
        {
            if (IsBaseSettingEnable)
            {
                if (ResolutionWidth <= 0 || ResolutionHeight <= 0)
                {
                    // 分辨率必须大于0
                    ErrorMessage = Lang.GameSettingPage_ResolutionMustBeGreaterThan0;
                    return;
                }
                var model = new GraphicsSettings_PCResolution_h431323223
                {
                    IsFullScreen = EnableFullScreen,
                    Width = ResolutionWidth,
                    Height = ResolutionHeight,
                };
                await GameSettingService.SetGameResolutionSettingAsync(CurrentGameBiz, model);
                AppConfig.SetUsePopupWindow(CurrentGameBiz, UsePopupWindow);
            }
            if (IsLanguageSettingEnable)
            {
                await GameSettingService.SetGameVoiceLanguageSettingAsync(CurrentGameBiz, LanguageIndex);
            }
            if (IsGraphicsSettingEnable)
            {
                if (CurrentGameBiz.Game is GameBiz.hkrpg)
                {
                    await GameSettingService.SetStarRailFPSIndexAsync(CurrentGameBiz, StarRailFpsIndex);
                }
                if (CurrentGameBiz.Game is GameBiz.hk4e)
                {
                    AppConfig.EnableGenshinHDR = EnableGenshinHDR;
                    await GameSettingService.SetGenshinEnableHDRAsync(CurrentGameBiz, EnableGenshinHDR);
                }
            }
            // 游戏运行时应用的设置无法生效
            ErrorMessage = Lang.GameSettingPage_SettingNotEffect;
            IsApplyButtonEnable = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _logger.LogError(ex, "Apply Setting");
        }
    }




    private void UpdateHdrState()
    {
        try
        {
            // 平台替换：Linux 无 WinRT 高级颜色（HDR）探测 API，暂不显示警示条。
            HDRNotEnabled = false;
            HDRNotSupported = false;
        }
        catch { }
    }



}
