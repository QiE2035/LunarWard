using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia.Media;

namespace LunarWard.Helpers;

/// <summary>
/// 从图片像素里取一组「背景 / 前景」强调色。
/// 上游：src/Starward/Helpers/AccentColorHelper.cs（WinRT Color/HsvColor + IBuffer 重载）。
/// 平台替换：Color/HsvColor 换 Avalonia.Media，删掉 IBuffer(IBufferByteAccess) 重载，
/// 由调用方把像素或文件读成 BGRA 字节再进来；算法逐行保留（含 hueCircle 与 30° 窗口）。
/// </summary>
internal static class AccentColorHelper
{


    public static unsafe (Color? Back, Color? Fore) GetAccentColor(byte[] bgra, int width, int height)
    {
        if (bgra.Length % 4 == 0)
        {
            fixed (byte* ptr = bgra)
            {
                return GetAccentColorInternal(ptr, width, height);
            }
        }
        return (null, null);
    }


    /// <summary>从 PNG/JPEG 文件读像素（BGRA8888）后取色；上游用 IBuffer 重载的位置在 Linux 走这里。</summary>
    public static (Color? Back, Color? Fore) GetAccentColor(string filePath, int width, int height)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            return GetAccentColor(stream, width, height);
        }
        catch
        {
            return (null, null);
        }
    }


    public static (Color? Back, Color? Fore) GetAccentColor(Stream stream, int width, int height)
    {
        try
        {
            using var bitmap = new Avalonia.Media.Imaging.Bitmap(stream);
            return GetAccentColor(bitmap, width, height);
        }
        catch
        {
            return (null, null);
        }
    }


    public static unsafe (Color? Back, Color? Fore) GetAccentColor(Avalonia.Media.Imaging.Bitmap bitmap, int width, int height)
    {
        int stride = width * 4;
        byte[] buffer = new byte[stride * height];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new Avalonia.PixelRect(0, 0, width, height), handle.AddrOfPinnedObject(), buffer.Length, stride);
            return GetAccentColor(buffer, width, height);
        }
        catch
        {
            return (null, null);
        }
        finally
        {
            handle.Free();
        }
    }




    private static unsafe (Color? Back, Color? Fore) GetAccentColorInternal(void* bgra, int width, int height)
    {
        try
        {
            uint* p = (uint*)bgra;
            long b = 0, g = 0, r = 0;
            int[] hueCircle = new int[360];
            for (int y = 0; y < height; y += 2)
            {
                for (int x = 0; x < width; x += 2)
                {
                    Bgra32 pixel = Unsafe.AsRef<Bgra32>(p);
                    b += pixel.B;
                    g += pixel.G;
                    r += pixel.R;
                    int hue = Bgra32ToHue(pixel);
                    // ignore white black gray
                    if (hue >= 0)
                    {
                        hueCircle[hue]++;
                    }
                    p += 2;
                }
                p += width - width % 2;
            }

            int c = (width / 2) * (height / 2);
            Unsafe.SkipInit(out Color color);
            color = Color.FromArgb(255, (byte)(r / c), (byte)(g / c), (byte)(b / c));
            (double h, double s, double v) = ToHsv(color);

            int maxHueStart = 0;
            int maxHueCount = 0;
            for (int i = ((int)h) + 30; i < ((int)h) + 300; i++)
            {
                int count = 0;
                for (int j = i; j < i + 30; j++)
                {
                    int hue2 = j % 360;
                    count += hueCircle[hue2];
                }
                if (count > maxHueCount)
                {
                    maxHueStart = i;
                    maxHueCount = count;
                }
            }

            long sum = 0;
            for (int i = maxHueStart; i < maxHueStart + 30; i++)
            {
                int hue2 = i % 360;
                sum += hue2 * hueCircle[hue2];
            }

            if (maxHueCount <= 0)
            {
                return (null, null);
            }

            return (FromHsv(h, 0.6, v), FromHsv((double)sum / maxHueCount, 0.9, 0.9));
        }
        catch { }
        return (null, null);
    }




    [StructLayout(LayoutKind.Explicit, Size = 4)]
    private readonly struct Bgra32
    {
        [FieldOffset(0)] public readonly byte B;
        [FieldOffset(1)] public readonly byte G;
        [FieldOffset(2)] public readonly byte R;
        [FieldOffset(3)] public readonly byte A;
    }



    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Bgra32ToHue(in Bgra32 bgra)
    {
        byte max = Math.Max(Math.Max(bgra.R, bgra.G), bgra.B);
        byte min = Math.Min(Math.Min(bgra.R, bgra.G), bgra.B);
        float chroma = max - min;
        float h;

        if (chroma <= 8)
        {
            // ignore white black gray
            h = -1;
        }
        else if (max == bgra.R)
        {
            h = (((bgra.G - bgra.B) / chroma) + 6) % 6;
        }
        else if (max == bgra.G)
        {
            h = 2 + ((bgra.B - bgra.R) / chroma);
        }
        else
        {
            h = 4 + ((bgra.R - bgra.G) / chroma);
        }
        return (int)(h * 60);
    }


    /// <summary>等价于 WinRT <c>Color.ToHsv()</c>（H: 0-360, S/V: 0-1）。</summary>
    private static (double H, double S, double V) ToHsv(Color color)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;
        double h = 0;
        if (delta > 0)
        {
            if (max == r)
            {
                h = 60 * (((g - b) / delta) % 6);
            }
            else if (max == g)
            {
                h = 60 * (((b - r) / delta) + 2);
            }
            else
            {
                h = 60 * (((r - g) / delta) + 4);
            }
        }
        if (h < 0)
        {
            h += 360;
        }
        double s = max <= 0 ? 0 : delta / max;
        return (h, s, max);
    }


    /// <summary>等价于 WinRT <c>ColorHelper.FromHsv()</c>。</summary>
    private static Color FromHsv(double h, double s, double v)
    {
        h = ((h % 360) + 360) % 360;
        s = Math.Clamp(s, 0, 1);
        v = Math.Clamp(v, 0, 1);
        double c = v * s;
        double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
        double m = v - c;
        (double r, double g, double b) = h switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        return Color.FromArgb(255,
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }


}
