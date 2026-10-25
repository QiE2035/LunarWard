using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Views.Starward;
using LunarWard.Core.Account;
using LunarWard.Launch;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 右上角「通行证授权登录」按钮，对应 Starward
/// <c>Features/GameLauncher/GameAuthLoginButton.xaml(.xaml.cs)</c>。
/// <para>
/// 源码的可见条件是 <c>GameBiz.Server == "cn"</c> 且 <c>AppConfig.stoken/mid</c> 非空；
/// LunarWard 的登录态是每日便笺/工具箱共用的 cookie（<c>daily-note-credentials.json</c>），
/// 所以这里从 cookie 里解析 <c>stoken</c> 与 <c>mid</c>，两者都在才显示按钮。
/// </para>
/// <para>
/// 通行证 ID 来自 passport 的 <c>/account/ma-cn-session/app/verify</c>
/// （<see cref="GameAuthLoginClient"/>）。源码把失败信息直接显示在 Flyout 里，
/// 这里保持一致：不弹对话框，只把异常消息放下方并露出「重新认证」。
/// </para>
/// </summary>
public sealed partial class GameAuthLoginViewModel : ObservableObject
{

    private readonly PageContext _context;

    private readonly string? _stoken;

    private readonly string? _mid;

    private bool _initializing;

    private GameAuthLoginClient? _client;


    public GameAuthLoginViewModel(PageContext context)
    {
        _context = context;

        var server = context.Shell.CurrentGameBiz.EndsWith("_global", StringComparison.Ordinal) ? "global" : "cn";
        var credential = new DailyNoteService(context.Services.Paths).GetCredentials(server);
        (_stoken, _mid) = ParseCookie(credential?.Cookie);

        // 开关状态来自配置文件；初始化期间不要写回，否则会把默认值覆盖掉用户设置。
        _initializing = true;
        EnableLoginAuthTicket = context.Services.Config.Launcher.EnableLoginAuthTicket ?? false;
        _initializing = false;

        IsVisible = server == "cn"
                    && !string.IsNullOrWhiteSpace(_stoken)
                    && !string.IsNullOrWhiteSpace(_mid);
    }


    public Localization.AppStrings Strings => Localization.AppStrings.Instance;


    /// <summary>只有国服 + 拿到了 stoken/mid 才显示按钮（源码同样条件）。</summary>
    [ObservableProperty]
    private bool _isVisible;


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PassportIdText))]
    private long? _hyperionAid;


    [ObservableProperty]
    private string? _errorMessage;


    [ObservableProperty]
    private bool _enableLoginAuthTicket;


    /// <summary>Flyout 里的「通行证 ID: 」一行（源码是 Run + Run 拼起来，这里合成一个字符串）。</summary>
    public string PassportIdText => $"{Strings["AuthLogin.PassportId"]}{HyperionAid}";


    partial void OnEnableLoginAuthTicketChanged(bool value)
    {
        if (_initializing)
        {
            return;
        }

        _context.Services.Config.Launcher.EnableLoginAuthTicket = value;
        try
        {
            _context.Services.Config.Save(_context.Services.Paths.ConfigFile);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }


    /// <summary>按钮 Loaded / 「重新认证」都会走这里（源码 InitializeGameAuthLoginCommand）。</summary>
    [RelayCommand]
    private async Task InitializeGameAuthLoginAsync()
    {
        if (!IsVisible)
        {
            return;
        }

        try
        {
            ErrorMessage = null;
            _client ??= new GameAuthLoginClient();
            HyperionAid = await _client.GetHyperionAidAsync(_stoken!, _mid!).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            HyperionAid = null;
            ErrorMessage = ex.Message;
        }
    }


    /// <summary>自检用：不联网，直接摆出「已拿到 ID + 一次失败」的样子。</summary>
    public void ShowForCapture(string errorMessage)
    {
        IsVisible = true;
        HyperionAid = 100000001;
        ErrorMessage = errorMessage;
    }


    /// <summary>
    /// 从 cookie 字符串里挑出 <c>stoken</c> 与 <c>mid</c>。
    /// cookie 形如 <c>ltuid_v2=...; stoken=...; mid=...;</c>，参数顺序不固定。
    /// </summary>
    public static (string? Stoken, string? Mid) ParseCookie(string? cookie)
    {
        if (string.IsNullOrWhiteSpace(cookie))
        {
            return (null, null);
        }

        string? stoken = null;
        string? mid = null;
        foreach (var segment in cookie.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var index = segment.IndexOf('=');
            if (index <= 0)
            {
                continue;
            }

            var name = segment[..index].Trim();
            var value = segment[(index + 1)..].Trim();
            if (value.Length == 0)
            {
                continue;
            }

            if (name.Equals("stoken", StringComparison.OrdinalIgnoreCase))
            {
                stoken = value;
            }
            else if (name.Equals("mid", StringComparison.OrdinalIgnoreCase))
            {
                mid = value;
            }
        }

        return (stoken, mid);
    }

}
