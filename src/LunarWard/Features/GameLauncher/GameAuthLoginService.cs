using LunarWard.Core;
using LunarWard.Core.GameRecord;
using LunarWard.Core.HoYoPlay;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace LunarWard.Features.GameLauncher;

/// <summary>
/// 通行证登录（stoken 校验 / 免密登录票据）。<b>移植自</b>
/// <c>Starward/src/Starward/Features/GameLauncher/GameAuthLoginService.cs</c>。
/// </summary>
/// <remarks>
/// 与上游唯一差异：上游通过 <c>AppConfig.GetService&lt;GameRecordService&gt;().UpdateDeviceFpAsync</c>
/// 刷新 <c>x-rpc-device_fp</c>；本仓库 GameRecord（task-5）尚未落地，这里用 Core 里已有的
/// <see cref="HyperionClient"/> 完成同样的「3 天过期 + 落盘到 AppConfig」逻辑。
/// 见 <c>docs/09-LINUX-DIVERGENCES.md</c>。
/// </remarks>
public class GameAuthLoginService
{


    private readonly ILogger<GameAuthLoginService> _logger;


    private readonly HttpClient _httpClient;


    public GameAuthLoginService(ILogger<GameAuthLoginService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }



    private long? hyperionAid;



    public async Task<long?> GetHyperionAidAsync(CancellationToken cancellationToken = default)
    {
        if (!hyperionAid.HasValue)
        {
            await VerifyStokenAsync(cancellationToken);
        }
        return hyperionAid;
    }



    public async Task VerifyStokenAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(AppConfig.stoken) || string.IsNullOrWhiteSpace(AppConfig.mid))
        {
            return;
        }
        var obj = new
        {
            token = new
            {
                token_type = 1,
                token = AppConfig.stoken,
            },
            refresh = true,
            mid = AppConfig.mid,
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "https://passport-api.mihoyo.com/account/ma-cn-session/app/verify")
        {
            Content = JsonContent.Create(obj),
            VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
        };
        request.Headers.Add("x-rpc-app_id", "ddxf5dufpuyo");
        request.Headers.Add("x-rpc-client_type", "3");
        request.Headers.Add("x-rpc-game_biz", "hyp_cn");
        await UpdateDeviceFpAsync(false, cancellationToken);
        request.Headers.Add("x-rpc-device_fp", AppConfig.HyperionDeviceFp);
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var node = await response.Content.ReadFromJsonAsync<Root<Data>>(cancellationToken);
        if (node!.Retcode != 0)
        {
            throw new miHoYoApiException(node.Retcode, node.Message);
        }
        hyperionAid = node.Data.UserInfo.Aid;
        if (node.Data.NewToken != null && node.Data.NewToken.Token != AppConfig.stoken)
        {
            AppConfig.stoken = node.Data.NewToken.Token;
            AppConfig.SaveConfiguration();
        }
    }



    public async Task<string?> CreateAuthTicketByGameBiz(GameId gameId)
    {
        try
        {
            if (gameId.GameBiz.Server is not "cn")
            {
                return null;
            }
            CancellationToken cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(3)).Token;
            long? aid = await GetHyperionAidAsync(cancellationToken);
            if (!hyperionAid.HasValue)
            {
                return null;
            }
            var obj = new
            {
                game_biz = gameId.GameBiz.ToString(),
                stoken = AppConfig.stoken,
                uid = hyperionAid,
                mid = AppConfig.mid,
            };
            var request = new HttpRequestMessage(HttpMethod.Post, "https://passport-api.mihoyo.com/account/ma-cn-verifier/app/createAuthTicketByGameBiz")
            {
                Content = JsonContent.Create(obj),
                VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
            };
            request.Headers.Add("x-rpc-app_id", "ddxf5dufpuyo");
            request.Headers.Add("x-rpc-client_type", "3");
            request.Headers.Add("x-rpc-game_biz", "hyp_cn");
            await UpdateDeviceFpAsync(false, cancellationToken);
            request.Headers.Add("x-rpc-device_fp", AppConfig.HyperionDeviceFp);
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var node = await response.Content.ReadFromJsonAsync<Root<AuthTicket>>(cancellationToken);
            if (node!.Retcode != 0)
            {
                throw new miHoYoApiException(node.Retcode, node.Message);
            }
            return node.Data.Ticket;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateAuthTicketByGameBiz");
        }
        return null;
    }




    /// <summary>
    /// 与上游 <c>GameRecordService.UpdateDeviceFpAsync</c> 相同的「设备指纹缓存 3 天」逻辑。
    /// </summary>
    private static async Task UpdateDeviceFpAsync(bool forceUpdate, CancellationToken cancellationToken)
    {
        string? id = AppConfig.HyperionDeviceId;
        string? fp = AppConfig.HyperionDeviceFp;
        DateTimeOffset lastUpdateTime = AppConfig.HyperionDeviceFpLastUpdateTime;
        HyperionClient client = new HyperionClient();
        if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(fp))
        {
            client.DeviceId = id;
            client.DeviceFp = fp;
        }
        if (forceUpdate || DateTimeOffset.Now - lastUpdateTime > TimeSpan.FromDays(3))
        {
            await client.GetDeviceFpAsync(cancellationToken);
            AppConfig.HyperionDeviceId = client.DeviceId;
            AppConfig.HyperionDeviceFp = client.DeviceFp;
            AppConfig.HyperionDeviceFpLastUpdateTime = DateTimeOffset.Now;
        }
    }




    public class Data
    {
        [JsonPropertyName("user_info")]
        public UserInfo UserInfo { get; set; }

        [JsonPropertyName("realname_info")]
        public object RealnameInfo { get; set; }

        [JsonPropertyName("need_realperson")]
        public bool NeedRealperson { get; set; }

        [JsonPropertyName("new_token")]
        public NewToken NewToken { get; set; }
    }

    public class NewToken
    {
        [JsonPropertyName("token_type")]
        public int TokenType { get; set; }

        [JsonPropertyName("token")]
        public string Token { get; set; }
    }

    public class Root<T>
    {
        [JsonPropertyName("retcode")]
        public int Retcode { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("data")]
        public T Data { get; set; }
    }

    public class UserInfo
    {
        [JsonPropertyName("aid")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long Aid { get; set; }

        [JsonPropertyName("mid")]
        public string Mid { get; set; }

        [JsonPropertyName("account_name")]
        public string AccountName { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("is_email_verify")]
        public int IsEmailVerify { get; set; }

        [JsonPropertyName("area_code")]
        public string AreaCode { get; set; }

        [JsonPropertyName("mobile")]
        public string Mobile { get; set; }

        [JsonPropertyName("safe_area_code")]
        public string SafeAreaCode { get; set; }

        [JsonPropertyName("safe_mobile")]
        public string SafeMobile { get; set; }

        [JsonPropertyName("realname")]
        public string Realname { get; set; }

        [JsonPropertyName("identity_code")]
        public string IdentityCode { get; set; }

        [JsonPropertyName("rebind_area_code")]
        public string RebindAreaCode { get; set; }

        [JsonPropertyName("rebind_mobile")]
        public string RebindMobile { get; set; }

        [JsonPropertyName("rebind_mobile_time")]
        public string RebindMobileTime { get; set; }

        [JsonPropertyName("links")]
        public List<object> Links { get; set; }

        [JsonPropertyName("country")]
        public string Country { get; set; }

        [JsonPropertyName("password_time")]
        public string PasswordTime { get; set; }

        [JsonPropertyName("unmasked_email")]
        public string UnmaskedEmail { get; set; }

        [JsonPropertyName("unmasked_email_type")]
        public int UnmaskedEmailType { get; set; }
    }


    public class AuthTicket
    {
        [JsonPropertyName("ticket")]
        public string Ticket { get; set; }
    }





}
