using System.Globalization;
using System.Reflection;
using Avalonia.Media;
using LunarWard.Features.GameRecord;
using LunarWard.Frameworks.WebView;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// <c>Features/GameRecord</c> 移植后的纯托管面（不依赖 GTK / 网络 / 数据库）。
/// </summary>
/// <remarks>
/// 这些测试挑的是「移植过程中改写过的等价物」，用来防止后续把上游形状改回去：
/// <list type="bullet">
/// <item><c>AvatarRankHelper.RankToVisibility</c>：上游返回 WinUI <c>Visibility</c>，这里返回 <c>bool</c>；</item>
/// <item><c>ColorToSolidColorBrushConverter</c>：Avalonia 的 <c>IValueConverter</c> 签名（末参 <see cref="CultureInfo"/>）；</item>
/// <item><c>GameRecordAssets.FromMsAppx</c>：WinUI 包 URI → 输出目录文件，缺文件返回 null 而不是抛异常；</item>
/// <item><c>WebView</c> 的便捷成员：未初始化时按「空值 + 不抛异常」降级，与 <c>CoreWebView2</c> 语义一致。</item>
/// </list>
/// </remarks>
public sealed class GameRecordPortTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    [InlineData(6, true)]
    public void RankToVisibility_returns_bool_for_IsVisible(int rank, bool expected)
    {
        Assert.Equal(expected, AvatarRankHelper.RankToVisibility(rank));
    }


    [Fact]
    public void ColorToSolidColorBrush_converter_uses_avalonia_signature()
    {
        Type type = typeof(GameRecordRoleChangedMessage).Assembly
            .GetType("LunarWard.Features.GameRecord.ColorToSolidColorBrushConverter", throwOnError: true)!;
        var converter = Activator.CreateInstance(type, nonPublic: true)!;
        MethodInfo convert = type.GetMethod("Convert")!;

        object? result = convert.Invoke(converter, new object?[] { Colors.Red, typeof(IBrush), null, CultureInfo.InvariantCulture });

        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Colors.Red, brush.Color);
    }


    [Theory]
    [InlineData("Assets/Image/__not_exists__.png")]
    [InlineData("Assets/Image/")]
    public void Assets_missing_file_returns_null_instead_of_throwing(string relativePath)
    {
        Type type = typeof(GameRecordRoleChangedMessage).Assembly
            .GetType("LunarWard.Features.GameRecord.GameRecordAssets", throwOnError: true)!;
        MethodInfo fromMsAppx = type.GetMethod("FromMsAppx", BindingFlags.Public | BindingFlags.Static)!;

        Assert.Null(fromMsAppx.Invoke(null, new object?[] { relativePath }));
    }


    [Fact]
    public void WebView_convenience_members_degrade_before_initialize()
    {
        var webView = new WebView();

        Assert.False(webView.CanGoBack);
        Assert.False(webView.CanGoForward);
        Assert.False(webView.IsEmbedded);
        Assert.Equal(string.Empty, webView.DocumentTitle);
        Assert.NotNull(webView.StatusMessage);
        // Linux: 未初始化时 ExecuteScriptAsync 与 WebView2「拿不到脚本结果」一样返回 JSON 的 null。
        Assert.Equal("null", webView.ExecuteScriptAsync("1 + 1").GetAwaiter().GetResult());

        // 未初始化时导航不抛异常（挂起到 _pendingSource，初始化后生效）。
        webView.Navigate("about:blank");
        webView.Reload();
        webView.Stop();
    }


    [Fact]
    public void WebView_source_property_is_Uri()
    {
        var webView = new WebView { Source = new Uri("https://www.miyoushe.com/") };

        Assert.Equal("https://www.miyoushe.com/", webView.Source!.ToString());
    }
}
