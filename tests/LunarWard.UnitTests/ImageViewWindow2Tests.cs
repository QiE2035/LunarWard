using LunarWard.App.ViewModels.Starward;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 图片查看器的数值格式（对齐 Starward <c>ImageViewWindow2</c> 的 ZoomFactor/索引/像素/文件大小）。
/// </summary>
public class ImageViewWindow2Tests
{

    [Theory]
    [InlineData(1.0, "100%")]
    [InlineData(0.333, "33%")]
    [InlineData(0.525, "53%")]
    [InlineData(0.1, "10%")]
    [InlineData(5.0, "500%")]
    public void ZoomPercentIsRounded(double zoom, string expected)
    {
        Assert.Equal(expected, ImageViewWindow2State.ZoomPercent(zoom));
    }


    [Theory]
    [InlineData(0, 3, "1/3")]
    [InlineData(2, 12, "3/12")]
    [InlineData(-1, 12, "")]
    [InlineData(0, 0, "")]
    public void IndexTextFollowsSelection(int index, int count, string expected)
    {
        Assert.Equal(expected, ImageViewWindow2State.IndexText(index, count));
    }


    [Theory]
    [InlineData(1920, 1080, "1920 × 1080")]
    [InlineData(0, 1080, "")]
    [InlineData(1920, 0, "")]
    public void PixelSizeUsesMultiplicationSign(int width, int height, string expected)
    {
        Assert.Equal(expected, ImageViewWindow2State.PixelSizeText(width, height));
    }


    [Theory]
    [InlineData(512, "512 B")]
    [InlineData(2048, "2 KiB")]
    [InlineData(173568, "169.5 KiB")]
    [InlineData(5 * 1024 * 1024, "5 MiB")]
    [InlineData((long)(1.5 * 1024 * 1024 * 1024), "1.5 GiB")]
    [InlineData(-1, "")]
    public void FileSizePicksUnit(long bytes, string expected)
    {
        Assert.Equal(expected, ImageViewWindow2State.FileSizeText(bytes));
    }


    [Fact]
    public void ZoomIsClampedToSliderRange()
    {
        Assert.Equal(0.1, ImageViewWindow2State.ClampZoom(0.01));
        Assert.Equal(5.0, ImageViewWindow2State.ClampZoom(99));
        Assert.Equal(1.0, ImageViewWindow2State.ClampZoom(double.NaN));
    }


    [Fact]
    public void ZoomInOutStepAndClamp()
    {
        Assert.Equal(1.25, ImageViewWindow2State.ZoomIn(1.0), 3);
        Assert.Equal(0.8, ImageViewWindow2State.ZoomOut(1.0), 3);
        Assert.Equal(5.0, ImageViewWindow2State.ZoomIn(4.5), 3);
        Assert.Equal(0.1, ImageViewWindow2State.ZoomOut(0.12), 3);
    }

}
