using LunarWard.Installer;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 7z 解压器的输出解析（审计 BT-05：安装器核心路径此前零测试）。
/// 这里只覆盖不依赖 7z 二进制的纯函数；真正解压由临时目录 + 造包的集成测试后续补。
/// </summary>
public class SevenZipArchiveExtractorTests
{
    [Theory]
    [InlineData("  0%", 0d)]
    [InlineData(" 12%", 0.12d)]
    [InlineData("100%", 1d)]
    [InlineData(" 45%  - file.bin", 0.45d)]
    public void ParseLastPercent_ReadsPercent(string text, double expected)
    {
        Assert.Equal(expected, SevenZipArchiveExtractor.ParseLastPercent(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("no digits%")]
    [InlineData("7z (a) - extracting files")]
    public void ParseLastPercent_ReturnsNullWithoutNumber(string text)
    {
        Assert.Null(SevenZipArchiveExtractor.ParseLastPercent(text));
    }

    [Fact]
    public void ParseLastPercent_UsesLastPercentToken()
    {
        // 7z 的进度行可能同时出现 "100%" 与尾部计数，取最后一个百分号前的数字。
        Assert.Equal(0.75d, SevenZipArchiveExtractor.ParseLastPercent("  5% ... 75%"));
    }
}
