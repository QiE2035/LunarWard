using Xunit;
using System.Net;
using System.Text;
using LunarWard.App.Services;

namespace LunarWard.UnitTests;

/// <summary>
/// 关于页「检查更新」的解析与版本比较（网络用假的 <see cref="HttpMessageHandler"/>，
/// 不真的访问 GitHub）。
/// </summary>
public class ReleaseCheckerTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }

    private static HttpClient Client(HttpStatusCode code, string body) => new(new StubHandler(_ => new HttpResponseMessage(code)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    }));


    private const string Body = """
        { "tag_name": "v0.2.0", "html_url": "https://example.invalid/releases/v0.2.0", "name": "0.2.0" }
        """;


    [Fact]
    public async Task NewerRelease_ReportsUpdateAvailable()
    {
        using var http = Client(HttpStatusCode.OK, Body);
        var result = await ReleaseChecker.CheckAsync(http, new Version(0, 1, 0));
        Assert.Equal(ReleaseCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal("0.2.0", result.LatestVersion);
        Assert.Equal("https://example.invalid/releases/v0.2.0", result.HtmlUrl);
    }


    [Fact]
    public async Task SameOrOlderRelease_ReportsUpToDate()
    {
        using var http = Client(HttpStatusCode.OK, Body);
        Assert.Equal(ReleaseCheckStatus.UpToDate, (await ReleaseChecker.CheckAsync(http, new Version(0, 2, 0))).Status);
        Assert.Equal(ReleaseCheckStatus.UpToDate, (await ReleaseChecker.CheckAsync(http, new Version(0, 3, 0))).Status);
    }


    [Fact]
    public async Task NonSuccessStatus_ReportsFailed()
    {
        using var http = Client(HttpStatusCode.Forbidden, "{}");
        var result = await ReleaseChecker.CheckAsync(http, new Version(1, 0, 0));
        Assert.Equal(ReleaseCheckStatus.Failed, result.Status);
        Assert.Contains("403", result.Error);
    }


    [Fact]
    public async Task MalformedJson_ReportsFailed()
    {
        using var http = Client(HttpStatusCode.OK, "not json");
        Assert.Equal(ReleaseCheckStatus.Failed, (await ReleaseChecker.CheckAsync(http, new Version(1, 0, 0))).Status);
    }


    [Fact]
    public async Task ReleaseWithoutTag_ReportsFailed()
    {
        using var http = Client(HttpStatusCode.OK, """{ "html_url": "https://example.invalid" }""");
        Assert.Equal(ReleaseCheckStatus.Failed, (await ReleaseChecker.CheckAsync(http, new Version(1, 0, 0))).Status);
    }


    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("v1.2.3-beta1", "1.2.3")]
    [InlineData("V0.1", "0.1")]
    public void Normalize_StripsPrefixAndPrereleaseSuffix(string tag, string expected)
        => Assert.Equal(expected, ReleaseChecker.Normalize(tag));


    [Theory]
    [InlineData("v0.1", 0, 1, 0)]
    [InlineData("2", 2, 0, 0)]
    [InlineData("nightly", 0, 0, 0)]
    public void IsNewer_ComparesMajorMinorBuild(string latest, int major, int minor, int build)
        => Assert.False(ReleaseChecker.IsNewer(latest, new Version(major, minor + 1, build)));
}
