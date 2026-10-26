using Avalonia.Media.Imaging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LunarWard.Features.Screenshot;

/// <summary>
/// 上游 <c>Starward.Features.Screenshot.ScreenCaptureContext</c>（175 行）：按窗口复用抓屏资源。
/// </summary>
/// <remarks>
/// 上游持有 <c>GraphicsCaptureItem</c>、<c>Direct3D11CaptureFramePool</c>、<c>GraphicsCaptureSession</c>
/// 并用 <c>WinEventHook</c> 感知窗口关闭。Linux 侧没有等价的「零拷贝帧池」API，
/// 每次抓取由外部工具重新取一帧（X11: <c>import -window</c>；Wayland: <c>grim</c>），
/// 这里仍保留同样的对象生命周期：按窗口句柄复用实例、窗口消失时触发
/// <see cref="CaptureWindowClosed"/> 让上层清缓存（差异见 docs/09 §8.4）。
/// </remarks>
internal class ScreenCaptureContext : IDisposable
{


    private bool _disposedValue;



    /// <summary>
    /// 上游是 Win32 HWND；Linux 侧是 X11 window id（Wayland 下恒为 0，表示整屏）。
    /// </summary>
    public nint WindowHandle { get; private set; }



    public event EventHandler? CaptureWindowClosed;



    public ScreenCaptureContext(nint windowHandle)
    {
        WindowHandle = windowHandle;
    }



    /// <summary>
    /// 抓取一帧。窗口已不存在时触发 <see cref="CaptureWindowClosed"/> 并抛出。
    /// </summary>
    public async Task<Bitmap> CaptureAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await ScreenCaptureHelper.CaptureWindowAsync(WindowHandle, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            CaptureWindowClosed?.Invoke(this, EventArgs.Empty);
            throw;
        }
    }



    /// <summary>
    /// 上游在窗口尺寸变化后重建 <c>GraphicsCaptureItem</c> / 帧池；外部工具每次重新抓帧，无需重建。
    /// </summary>
    public void RecreateResource()
    {

    }



    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                CaptureWindowClosed = null;
            }
            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }


}
