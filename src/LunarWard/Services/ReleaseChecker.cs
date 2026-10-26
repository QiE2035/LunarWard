using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LunarWard.Services;

/// <summary>检查更新的结果。对应 Starward「关于」页检查更新的三种状态：已是最新 / 有新版 / 失败。</summary>
public enum ReleaseCheckStatus
{
    /// <summary>当前版本不低于最新发布。</summary>
    UpToDate,

    /// <summary>最新发布的版本比当前版本新。</summary>
    UpdateAvailable,

    /// <summary>请求或解析失败（网络不可用、GitHub 限流、响应格式不对）。</summary>
    Failed,
}

/// <param name="Status">结果状态。</param>
/// <param name="LatestVersion">最新发布的版本号（去掉前导 <c>v</c>），失败时为 <c>null</c>。</param>
/// <param name="HtmlUrl">最新发布页地址，失败时为 <c>null</c>。</param>
/// <param name="Error">失败原因（仅 <see cref="ReleaseCheckStatus.Failed"/> 时有值）。</param>
public sealed record ReleaseCheckResult(
    ReleaseCheckStatus Status,
    string? LatestVersion = null,
    string? HtmlUrl = null,
    string? Error = null);

/// <summary>
/// 检查 LunarWard 的最新发布。
/// </summary>
/// <remarks>
/// <para>
/// Starward 内置更新器（下载安装包、替换自身）；LunarWard 在 Linux 上走发行包
/// （源码 / Arch PKGBUILD），不替换自身，所以只做「查最新发布 → 打开下载页」：
/// 对应上游 <c>AboutSetting.xaml:47-85</c> 的检查更新位置，但行为退化为提示 + 外链。
/// </para>
/// <para>
/// 接口用 GitHub 的 <c>releases/latest</c>（自动跳过 draft / prerelease）。
/// </para>
/// </remarks>
public static class ReleaseChecker
{
    public const string LatestReleaseApi = "https://api.github.com/repos/QiE2035/LunarWard/releases/latest";

    public const string ReleasesPage = "https://github.com/QiE2035/LunarWard/releases";

    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };


    /// <summary>请求最新发布并与 <paramref name="current"/> 比较。</summary>
    /// <exception cref="OperationCanceledException">调用方取消时原样抛出。</exception>
    public static async Task<ReleaseCheckResult> CheckAsync(HttpClient http, Version current, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
            request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
            request.Headers.TryAddWithoutValidation("User-Agent", "LunarWard-UpdateChecker");

            using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new ReleaseCheckResult(ReleaseCheckStatus.Failed,
                    Error: $"HTTP {(int)response.StatusCode}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var release = await JsonSerializer.DeserializeAsync<ReleaseDto>(stream, s_json, ct).ConfigureAwait(false);
            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
            {
                return new ReleaseCheckResult(ReleaseCheckStatus.Failed, Error: "empty release");
            }

            var latest = Normalize(release.TagName);
            var status = IsNewer(latest, current) ? ReleaseCheckStatus.UpdateAvailable : ReleaseCheckStatus.UpToDate;
            return new ReleaseCheckResult(status, latest, release.HtmlUrl ?? ReleasesPage);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            return new ReleaseCheckResult(ReleaseCheckStatus.Failed, Error: ex.Message);
        }
    }


    /// <summary>把 <c>v1.2.3</c> / <c>1.2.3</c> / <c>1.2.3-beta</c> 归一成 <c>1.2.3</c>。</summary>
    public static string Normalize(string tag)
    {
        var text = tag.Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
        {
            text = text[1..];
        }

        var dash = text.IndexOf('-');
        return dash > 0 ? text[..dash] : text;
    }


    /// <summary>解析版本号；<c>1.2</c> 视为 <c>1.2.0</c>，解析失败返回 <c>null</c>。</summary>
    public static Version? ParseVersion(string text)
    {
        var normalized = Normalize(text);
        if (Version.TryParse(normalized, out var version))
        {
            return version;
        }

        // 允许 1.2 / 1 这类写法（Version.TryParse 要求至少两段）
        return int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var major)
            ? new Version(major, 0)
            : null;
    }


    /// <summary>最新版本是否比当前版本新（只比较 major/minor/build，忽略修订号）。</summary>
    public static bool IsNewer(string latest, Version current)
    {
        var parsed = ParseVersion(latest);
        if (parsed is null)
        {
            return false;
        }

        return new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0))
            > new Version(current.Major, current.Minor, Math.Max(current.Build, 0));
    }


    private sealed record ReleaseDto(
        [property: JsonPropertyName("tag_name")] string? TagName,
        [property: JsonPropertyName("html_url")] string? HtmlUrl);
}
