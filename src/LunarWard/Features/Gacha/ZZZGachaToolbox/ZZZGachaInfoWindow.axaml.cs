using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using LunarWard.Core;
using LunarWard.Core.Gacha.ZZZ;
using LunarWard.Features.Database;
using LunarWard.Frameworks;
using LunarWard.Frameworks.WebView;
using LunarWard.Helpers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LunarWard.Features.Gacha.ZZZGachaToolbox;

/// <summary>
/// 绝区零抽卡图鉴工具箱窗口，对齐上游 <c>Starward/Features/Gacha/ZZZGachaToolbox/ZZZGachaInfoWindow.xaml(.xaml.cs)</c>。
/// </summary>
/// <remarks>
/// 平台替换点（见 docs/09 的「七、抽卡与云游戏」）：
/// <c>WebView2</c> → <see cref="Frameworks.WebView.WebView"/>（WebKitGTK）；
/// <c>CoreWebView2WebResourceResponseReceivedEventArgs.Request.Uri/Headers</c> → 本移植版的
/// <see cref="CoreWebView2WebResourceResponseReceivedEventArgs"/> 只暴露 <c>Uri</c> 与 <c>Response</c>，
/// 因此请求头改由 <see cref="CoreWebView2.CookieManager"/> 重新拼装（只有 Cookie 一项）；
/// <c>GridView</c> → <see cref="ListBox"/>（<c>SelectionMode="Multiple"</c>，多选面板用 WrapPanel）；
/// <c>Hyperlink</c> → <see cref="HyperlinkButton"/>；<c>Launcher.LaunchUriAsync(folder)</c> → xdg-open；
/// <c>FileDialogHelper.PickFolderAsync(XamlRoot)</c> → <see cref="FileDialogHelper.PickFolderAsync(Visual)"/>；
/// <c>[INotifyPropertyChanged]</c>/<c>[RelayCommand]</c> → 手写属性与 <see cref="IRelayCommand"/>。
/// </remarks>
public sealed partial class ZZZGachaInfoWindow : WindowEx, INotifyPropertyChanged
{

    private readonly ILogger<ZZZGachaInfoWindow> _logger = AppConfig.GetLogger<ZZZGachaInfoWindow>();


    private readonly HttpClient _httpClient = AppConfig.GetService<HttpClient>();


    private const string Source_cn = "https://act.mihoyo.com/zzz/gt/character-builder-h/index.html";
    private const string Source_global = "https://act.hoyolab.com/zzz/gt/character-builder-h/index.html";


    public ZZZGachaInfoWindow()
    {
        this.InitializeComponent();
        DataContext = this;
        NavigateToMiyousheCommand = new RelayCommand(NavigateToMiyoushe);
        NavigateToHoYoLABCommand = new RelayCommand(NavigateToHoYoLAB);
        GetAllLanguagesInfoCommand = new AsyncRelayCommand(GetAllLanguagesInfoAsync);
        SaveToDatabaseCommand = new RelayCommand(SaveToDatabase);
        ExportToFolderCommand = new AsyncRelayCommand(ExportToFolderAsync);
        InitializeWindow();

        RootGrid.Loaded += RootGrid_Loaded;
        RootGrid.Unloaded += RootGrid_Unloaded;
    }


    public ICommand NavigateToMiyousheCommand { get; }

    public ICommand NavigateToHoYoLABCommand { get; }

    public ICommand GetAllLanguagesInfoCommand { get; }

    public ICommand SaveToDatabaseCommand { get; }

    public ICommand ExportToFolderCommand { get; }



    private void InitializeWindow()
    {
        Title = Lang.ToolboxSetting_ZZZGachaItemImages;
        RootGrid.RequestedThemeVariant = ShouldAppsUseDarkMode() ? ThemeVariant.Dark : ThemeVariant.Light;
        AdaptTitleBarButtonColorToActuallTheme();
        SetIcon();
    }


    private async void RootGrid_Loaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            await webview2.EnsureCoreWebView2Async();
            coreWebView2 = webview2.CoreWebView2!;
            coreWebView2.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Dark;
            coreWebView2.WebResourceResponseReceived += CoreWebView2_WebResourceResponseReceived;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ZZZGachaInfoWindow: EnsureCoreWebView2");
        }
    }


    private void RootGrid_Unloaded(object? sender, RoutedEventArgs e)
    {
        RootGrid.Loaded -= RootGrid_Loaded;
        RootGrid.Unloaded -= RootGrid_Unloaded;
        ListView_Languages.SelectionChanged -= ListView_Languages_SelectionChanged;
        if (coreWebView2 is not null)
        {
            coreWebView2.WebResourceResponseReceived -= CoreWebView2_WebResourceResponseReceived;
        }
        cts.Cancel();
        GachaInfoResult.Clear();
        gachaInfoDict = null!;
        iconInfoDict = null!;
        itemListDict = null!;
        headers = null!;
    }



    private CancellationTokenSource cts = new();


    private CoreWebView2 coreWebView2;


    private string? url;

    private List<KeyValuePair<string, string>>? headers;



    private ObservableCollection<string> _gachaInfoResult = new();

    public ObservableCollection<string> GachaInfoResult
    {
        get => _gachaInfoResult;
        set => SetProperty(ref _gachaInfoResult, value);
    }


    private Dictionary<string, List<ZZZGachaInfo>> gachaInfoDict = new();

    private Dictionary<string, IconInfo> iconInfoDict = new();

    private Dictionary<string, ItemList> itemListDict = new();



    private void NavigateToMiyoushe()
    {
        try
        {
            webview2.IsVisible = true;
            webview2.CoreWebView2!.Navigate(Source_cn);
            Button_GetAllLanguages.IsEnabled = false;
        }
        catch { }
    }



    private void NavigateToHoYoLAB()
    {
        try
        {
            webview2.IsVisible = true;
            webview2.CoreWebView2!.Navigate(Source_global);
            Button_GetAllLanguages.IsEnabled = false;
        }
        catch { }
    }



    private async void CoreWebView2_WebResourceResponseReceived(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs args)
    {
        try
        {
            if (Uri.TryCreate(args.Uri, UriKind.Absolute, out Uri? uri))
            {
                if (uri.AbsolutePath.Contains("/icon_info") && args.Response.StatusCode == 200)
                {
                    url = uri.OriginalString;
                    headers = await GetRequestHeadersAsync(uri);

                    string biz = "";
                    string lang = "";
                    if (uri.OriginalString.Contains("mihoyo.com"))
                    {
                        biz = GameBiz.nap_cn;
                        lang = "zh-cn";
                        Button_GetAllLanguages.IsEnabled = false;
                    }
                    if (uri.OriginalString.Contains("hoyolab.com"))
                    {
                        biz = GameBiz.nap_global;
                        string cookie = headers?.FirstOrDefault(x => x.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase)).Value ?? "";
                        lang = Regex.Match(cookie, @"mi18nLang=([^;]+);?").Groups[1].Value;
                        Button_GetAllLanguages.IsEnabled = true;
                    }
                    if (string.IsNullOrWhiteSpace(biz) || string.IsNullOrWhiteSpace(lang))
                    {
                        return;
                    }
                    string key = $"{biz}.{lang}";
                    await AddIconInfoAndGetItemListAsync(key, args);
                    UpdateGachaInfo(key);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ZZZGachaInfoWindow: CoreWebView2_WebResourceResponseReceived");
        }
    }



    /// <summary>
    /// 上游直接取 <c>args.Request.Headers</c>；本移植版的 WebView 事件参数不暴露请求头，
    /// 因此在同源 Cookie 容器里重新拼装（等价于 WebView2 会带上的 Cookie 头）。
    /// </summary>
    private async Task<List<KeyValuePair<string, string>>> GetRequestHeadersAsync(Uri uri)
    {
        var list = new List<KeyValuePair<string, string>>();
        try
        {
            string origin = uri.GetLeftPart(UriPartial.Authority);
            var cookies = await coreWebView2.CookieManager.GetCookiesAsync(origin);
            if (cookies.Count > 0)
            {
                string cookie = string.Join("; ", cookies.Select(x => $"{x.Name}={x.Value}"));
                list.Add(new("Cookie", cookie));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Get request headers");
        }
        return list;
    }



    private async Task AddIconInfoAndGetItemListAsync(string key, CoreWebView2WebResourceResponseReceivedEventArgs args)
    {
        try
        {

            var stream = await args.Response.GetContentAsync();
            if (stream is not null)
            {
                var wrapper = await JsonSerializer.DeserializeAsync<miHoYoApiWrapper<IconInfo>>(stream, cancellationToken: cts.Token);
                if (wrapper is not null && wrapper.Data is not null)
                {
                    iconInfoDict.TryAdd(key, wrapper.Data);
                }
            }
            {
                string url = args.Uri.Replace("/icon_info", "/item_list") + "&avatar_id=1011";
                var headers = this.headers ?? [];
                var request = new HttpRequestMessage(HttpMethod.Get, url) { VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher };
                foreach (var header in headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
                var response = await _httpClient.SendAsync(request, cts.Token);
                response.EnsureSuccessStatusCode();
                var wrapper = await response.Content.ReadFromJsonAsync<miHoYoApiWrapper<ItemList>>(cts.Token);
                if (wrapper is not null && wrapper.Data is not null)
                {
                    itemListDict.TryAdd(key, wrapper.Data);
                }
            }

        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get ZZZGachaInfo for specific langugae");
        }
    }



    private async Task GetAllLanguagesInfoAsync()
    {
        try
        {
            if (url?.Contains("hoyolab.com") ?? false && headers is not null)
            {
                foreach (var lang in LanguageUtil.GetAllLanguages())
                {
                    string key = $"nap_global.{lang}";
                    if (gachaInfoDict.ContainsKey(key))
                    {
                        continue;
                    }
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, url) { VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher };
                        foreach (var header in headers)
                        {
                            string value = header.Value;
                            if (header.Key.Equals("cookie", StringComparison.OrdinalIgnoreCase))
                            {
                                value = Regex.Replace(value, @"mi18nLang=[^;]+", $"mi18nLang={lang}");
                            }
                            if (header.Key.Equals("x-rpc-lang", StringComparison.OrdinalIgnoreCase))
                            {
                                value = lang;
                            }
                            request.Headers.Add(header.Key, value);
                        }
                        var response = await _httpClient.SendAsync(request, cts.Token);
                        response.EnsureSuccessStatusCode();
                        var wrapper = await response.Content.ReadFromJsonAsync<miHoYoApiWrapper<IconInfo>>(cts.Token);
                        if (wrapper is not null && wrapper.Data is not null)
                        {
                            iconInfoDict.TryAdd(key, wrapper.Data);
                        }
                    }
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, url.Replace("/icon_info", "/item_list") + "&avatar_id=1011") { VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher };
                        foreach (var header in headers)
                        {
                            string value = header.Value;
                            if (header.Key.Equals("cookie", StringComparison.OrdinalIgnoreCase))
                            {
                                value = Regex.Replace(value, @"mi18nLang=[^;]+", $"mi18nLang={lang}");
                            }
                            if (header.Key.Equals("x-rpc-lang", StringComparison.OrdinalIgnoreCase))
                            {
                                value = lang;
                            }
                            request.Headers.Add(header.Key, value);
                        }
                        var response = await _httpClient.SendAsync(request, cts.Token);
                        response.EnsureSuccessStatusCode();
                        var wrapper = await response.Content.ReadFromJsonAsync<miHoYoApiWrapper<ItemList>>(cts.Token);
                        if (wrapper is not null && wrapper.Data is not null)
                        {
                            itemListDict.TryAdd(key, wrapper.Data);
                        }
                    }
                    UpdateGachaInfo(key);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get ZZZGachaInfo for all languages");
        }
    }



    private void UpdateGachaInfo(string key)
    {
        try
        {
            var iconInfo = iconInfoDict.GetValueOrDefault(key);
            var itemList = itemListDict.GetValueOrDefault(key);
            if (iconInfo is not null && itemList is not null)
            {
                var list = new List<ZZZGachaInfo>();
                foreach (var item in itemList.Avatars)
                {
                    var info = new ZZZGachaInfo
                    {
                        Id = item.Id,
                        Name = item.NameMi18n,
                        Rarity = item.Rarity switch
                        {
                            "S" or "s" => 4,
                            "A" or "a" => 3,
                            "B" or "b" => 2,
                            _ => 0,
                        },
                        ElementType = item.ElementType,
                        Profession = item.AvatarProfession,
                    };
                    info.Icon = iconInfo.AvatarIcons.GetValueOrDefault(item.Id.ToString())?.SquareAvatar ?? "";
                    list.Add(info);
                }
                foreach (var item in itemList.Weapons)
                {
                    var info = new ZZZGachaInfo
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Icon = item.Icon,
                        Rarity = item.Rarity switch
                        {
                            "S" or "s" => 4,
                            "A" or "a" => 3,
                            "B" or "b" => 2,
                            _ => 0,
                        },
                        Profession = item.Profession,
                    };
                    list.Add(info);
                }
                foreach (var item in itemList.Buddies)
                {
                    var info = new ZZZGachaInfo
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Rarity = item.Rarity switch
                        {
                            "S" or "s" => 4,
                            "A" or "a" => 3,
                            "B" or "b" => 2,
                            _ => 0,
                        },
                    };
                    info.Icon = iconInfo.BuddyIcons.GetValueOrDefault(item.Id.ToString())?.SquareAvatar ?? "";
                    list.Add(info);
                }

                gachaInfoDict.TryAdd(key, list);
                GachaInfoResult.Remove(key);
                GachaInfoResult.Add(key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ZZZGachaWindows: Update GachaInfo");
        }
    }



    private void ListView_Languages_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (sender is ListBox listBox)
            {
                int count = listBox.SelectedItems?.Count ?? 0;
                Button_SaveToDatabase.IsEnabled = count == 1;
                Button_ExportFiles.IsEnabled = count > 0;
            }
        }
        catch { }
    }



    private void SaveToDatabase()
    {
        try
        {
            if (ListView_Languages.SelectedItems is not { Count: 1 } selected)
            {
                return;
            }
            if (selected[0] is string key)
            {
                if (gachaInfoDict.TryGetValue(key, out var list))
                {
                    using var dapper = DatabaseService.CreateConnection();
                    using var t = dapper.BeginTransaction();
                    dapper.Execute("""
                        INSERT OR REPLACE INTO ZZZGachaInfo (Id, Name, Icon, Rarity, ElementType, Profession)
                        VALUES (@Id, @Name, @Icon, @Rarity, @ElementType, @Profession);
                        """, list, t);
                    t.Commit();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Save ZZZGachaInfo to database");
        }
    }



    private async Task ExportToFolderAsync()
    {
        try
        {
            if (ListView_Languages.SelectedItems is { Count: > 0 } selected)
            {
                string? folder = await FileDialogHelper.PickFolderAsync(this);
                if (Directory.Exists(folder))
                {
                    foreach (string key in selected.Cast<string>())
                    {
                        if (gachaInfoDict.TryGetValue(key, out var list))
                        {
                            var obj = new miHoYoApiWrapper<ZZZGachaWiki>
                            {
                                Retcode = 0,
                                Message = "",
                                Data = new ZZZGachaWiki
                                {
                                    Game = GameBiz.nap,
                                    Language = key[^5..],
                                    List = list.OrderBy(x => x.Id).ToList(),
                                },
                            };
                            string json = JsonSerializer.Serialize(obj, AppConfig.JsonSerializerOptions);
                            string path = Path.Combine(folder, $"ZZZGachaInfo.{key}.json");
                            await File.WriteAllTextAsync(path, json, cts.Token);
                        }
                    }
                    // 上游 Launcher.LaunchUriAsync(new Uri(folder))：在文件管理器中打开导出目录
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "xdg-open",
                        ArgumentList = { folder },
                        UseShellExecute = false,
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export ZZZGachaInfo to folder");
        }
    }




    #region INotifyPropertyChanged

    /// <summary>
    /// Avalonia 的 <see cref="AvaloniaObject"/> 已实现 <see cref="INotifyPropertyChanged"/>，但只对其
    /// AvaloniaProperty 发通知；本窗口的 CLR 属性走这里。用 <c>new</c> 隐藏基类事件，并把接口分发显式重映射过来。
    /// </summary>
    public new event PropertyChangedEventHandler? PropertyChanged;

    event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
    {
        add => PropertyChanged += value;
        remove => PropertyChanged -= value;
    }


    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

}
