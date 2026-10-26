using Avalonia.Media.Imaging;
using System;

namespace LunarWard.Features.Screenshot;

/// <summary>
/// 上游 <c>Starward.Features.Screenshot.ScreenCaptureItem</c>：一帧游戏窗口画面。
/// </summary>
/// <remarks>
/// 差异（docs/09）：上游持有 Win2D <c>CanvasBitmap</c>、ICC <c>ColorPrimaries</c> 并逐帧分析
/// HDR（MaxCLL / SdrWhiteLevel）。Linux 侧没有 Win2D / Windows.Graphics.Capture，
/// 位图改用 Avalonia <see cref="Bitmap"/>，HDR 元数据字段保留（恒为 SDR 值），
/// ICC 主色度分析不做（见 ScreenCaptureService 注释）。
/// </remarks>
internal class ScreenCaptureItem : IDisposable
{

    public Bitmap Bitmap { get; set; } = null!;

    public bool HDR { get; set; }

    public float MaxCLL { get; set; }

    public float SdrWhiteLevel { get; set; }

    public DateTimeOffset FrameTime { get; set; }



    private bool disposedValue;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                Bitmap?.Dispose();
            }
            Bitmap = null!;
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
