using LunarWard.ViewModels.Starward;
using LunarWard.Installer;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 预下载按钮的文案 / 图标 / 数值格式化（对齐 Starward <c>PreDownloadButton.xaml.cs</c>）。
/// </summary>
public class PredownloadButtonTests
{

    private static string L(string key) => key;


    [Theory]
    [InlineData(GameInstallState.Stop, false, "Launcher.PreInstall")]
    [InlineData(GameInstallState.Waiting, false, "StartGameButton.Waiting")]
    [InlineData(GameInstallState.Paused, false, "DownloadGamePage.Paused")]
    [InlineData(GameInstallState.Error, false, "DownloadGamePage.SomethingError")]
    [InlineData(GameInstallState.Finish, false, "Launcher.PreInstallFinished")]
    [InlineData(GameInstallState.Stop, true, "Launcher.PreInstallFinished")]
    public void ButtonTextFollowsState(GameInstallState state, bool finished, string expectedKey)
    {
        Assert.Equal(expectedKey, PredownloadButtonState.Text(state, finished, "12.3%", L));
    }


    [Fact]
    public void DownloadingShowsPercentAsButtonText()
    {
        Assert.Equal("38.6%", PredownloadButtonState.Text(GameInstallState.Downloading, false, "38.6%", L));
    }


    [Fact]
    public void IconIsPauseWhenHoveringDownloading()
    {
        Assert.Equal("\uE62E", PredownloadButtonState.Icon(GameInstallState.Downloading, true));
    }


    [Fact]
    public void IconIsDownloadWhenNotHovering()
    {
        Assert.Equal("\uEBD3", PredownloadButtonState.Icon(GameInstallState.Downloading, false));
        Assert.Equal("\uEBD3", PredownloadButtonState.Icon(GameInstallState.Paused, false));
    }


    [Fact]
    public void IconIsPlayWhenHoveringPaused()
    {
        Assert.Equal("\uF5B0", PredownloadButtonState.Icon(GameInstallState.Paused, true));
        Assert.Equal("\uF5B0", PredownloadButtonState.Icon(GameInstallState.Queueing, true));
    }


    [Fact]
    public void PopupOnlyOpensWhenThereIsSomethingToShow()
    {
        Assert.False(PredownloadButtonState.PopupOpen(GameInstallState.Stop, false, true));
        Assert.True(PredownloadButtonState.PopupOpen(GameInstallState.Downloading, false, true));
        Assert.False(PredownloadButtonState.PopupOpen(GameInstallState.Downloading, false, false));
        Assert.True(PredownloadButtonState.PopupOpen(GameInstallState.Stop, true, true));
    }


    [Fact]
    public void ButtonTurnsOffAfterFinish()
    {
        Assert.False(PredownloadButtonState.ButtonEnabled(GameInstallState.Finish, false));
        Assert.False(PredownloadButtonState.ButtonEnabled(GameInstallState.Stop, true));
        Assert.True(PredownloadButtonState.ButtonEnabled(GameInstallState.Downloading, false));
    }


    [Fact]
    public void BytesUseGigabytesAboveOneGigabyte()
    {
        const long gb = 1L << 30;
        Assert.Equal("3.51/9.09 GB", PredownloadButtonState.BytesText((long)(3.51 * gb), (long)(9.09 * gb)));
    }


    [Fact]
    public void BytesUseMegabytesBelowOneGigabyte()
    {
        const long mb = 1L << 20;
        Assert.Equal("120.00/500.00 MB", PredownloadButtonState.BytesText(120 * mb, 500 * mb));
    }


    [Fact]
    public void SpeedUsesMegabytesAboveOneMegabyte()
    {
        Assert.Equal("22.40 MB/s", PredownloadButtonState.SpeedText((long)(22.4 * (1 << 20))));
        Assert.Equal("512.00 KB/s", PredownloadButtonState.SpeedText(512 * 1024));
    }


    [Fact]
    public void RemainTimeIsPlaceholderWhenUnknown()
    {
        Assert.Equal("--:--:--", PredownloadButtonState.RemainTimeText(0));
        Assert.Equal("00:04:12", PredownloadButtonState.RemainTimeText(252));
    }

}
