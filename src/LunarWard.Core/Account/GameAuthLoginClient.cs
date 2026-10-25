using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LunarWard.Core;

namespace LunarWard.Core.Account;

/// <summary>
/// 通行证授权登录，对应 Starward <c>Features/GameLauncher/GameAuthLoginService.cs</c>。
/// 只实现 UI 需要的「用 stoken/mid 换取通行证 HyperionAid」一段；上游的
/// <c>CreateAuthTicketByGameBiz</c> 在 Starward 里已无调用点（登录票据改由启动器自己处理），
/// 这里同样不实现，避免引入没有出口的网络路径。
/// </summary>
public sealed class GameAuthLoginClient
{

    private const string PassportApi = "https://passport-api.mihoyo.com";

    private const string AppId = "ddxf5dufpuyo";


    private readonly HttpClient _httpClient;


    private long? _hyperionAid;


    public GameAuthLoginClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new(new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.All })
        {
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
        };
    }


    /// <summary>
    /// 校验 stoken 并取回通行证 ID（Starward <c>GetHyperionAidAsync</c>）。
    /// 同一个实例只请求一次，之后返回缓存值。
    /// </summary>
    public async Task<long> GetHyperionAidAsync(string stoken, string mid, CancellationToken cancellationToken = default)
    {
        if (_hyperionAid.HasValue)
        {
            return _hyperionAid.Value;
        }

        if (string.IsNullOrWhiteSpace(stoken) || string.IsNullOrWhiteSpace(mid))
        {
            throw new miHoYoApiException(-1, "stoken 或 mid 为空。");
        }

        var obj = new
        {
            token = new
            {
                token_type = 1,
                token = stoken,
            },
            refresh = true,
            mid,
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{PassportApi}/account/ma-cn-session/app/verify")
        {
            Content = JsonContent.Create(obj),
            VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
        };
        request.Headers.Add("x-rpc-app_id", AppId);
        request.Headers.Add("x-rpc-client_type", "3");
        request.Headers.Add("x-rpc-game_biz", "hyp_cn");

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        // 只需要 aid 一个值，用 JsonDocument 读取，避免为一个字段再注册一份 JsonSerializerContext。
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;

        var retcode = root.TryGetProperty("retcode", out var retcodeElement) ? retcodeElement.GetInt32() : 0;
        if (retcode != 0)
        {
            var message = root.TryGetProperty("message", out var messageElement) ? messageElement.GetString() : null;
            throw new miHoYoApiException(retcode, message ?? "Passport verify failed.");
        }

        if (!root.TryGetProperty("data", out var data)
            || !data.TryGetProperty("user_info", out var userInfo)
            || !userInfo.TryGetProperty("aid", out var aidElement))
        {
            throw new miHoYoApiException(-1, "通行证返回值里没有 user_info.aid。");
        }

        _hyperionAid = aidElement.ValueKind == JsonValueKind.String
            ? long.Parse(aidElement.GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture)
            : aidElement.GetInt64();

        return _hyperionAid.Value;
    }

}
