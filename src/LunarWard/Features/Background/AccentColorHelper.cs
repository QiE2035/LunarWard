using System;
using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Messaging;

namespace LunarWard.Features.Background;

/// <summary>
/// 从背景图取强调色并刷新主题色（对应上游
/// <c>Starward/Features/Background/AccentColorHelper.cs</c>，197 行）。
/// </summary>
/// <remarks>
/// 平台替换：WinRT <c>Color</c>/<c>HsvColor</c>/<c>ColorHelper.FromHsv</c> 换成
/// <see cref="Color"/>、<see cref="HsvColor"/> 与 <c>ToRgb</c>；
/// 删除 <c>IBufferByteAccess</c>/<c>IMemoryBufferByteAccess</c> 两个 COM 重载；
/// 其余取色算法（隔行隔列平均 → HSV 饱和 0.6 → 七个强调色资源 → 发消息）与上游一致。
/// </remarks>
internal static class AccentColorHelper
{


    public static unsafe Color? GetAccentColor(byte[] bgra, int width, int height)
    {
        if (bgra.Length % 4 == 0)
        {
            fixed (byte* ptr = bgra)
            {
                return GetAccentColorInternal(ptr, width, height);
            }
        }
        return null;
    }




    public static unsafe Color? GetAccentColor(nint bufferPtr, uint capacity, int width, int height)
    {
        if (capacity > 0 && capacity % 4 == 0)
        {
            return GetAccentColorInternal((void*)bufferPtr, width, height);
        }
        return null;
    }




    private static unsafe Color? GetAccentColorInternal(void* bgra, int width, int height)
    {
        try
        {
            uint* p = (uint*)bgra;
            long b = 0, g = 0, r = 0;
            for (int y = 0; y < height; y += 2)
            {
                for (int x = 0; x < width; x += 2)
                {
                    uint pixel = *p;
                    b += pixel & 0xFF;
                    g += (pixel >> 8) & 0xFF;
                    r += (pixel >> 16) & 0xFF;
                    p += 2;
                }
                p += width - width % 2;
            }

            int c = (width / 2) * (height / 2);
            if (c <= 0)
            {
                return null;
            }
            Color color = Color.FromArgb(255, (byte)(r / c), (byte)(g / c), (byte)(b / c));
            HsvColor hsv = color.ToHsv();
            return new HsvColor(1, hsv.H, 0.6, hsv.V).ToRgb();
        }
        catch { }
        return null;
    }




    private static Color ColorMix(Color input, Color blend, double percent)
    {
        return Color.FromArgb(255,
                              (byte)(input.R * percent + blend.R * (1 - percent)),
                              (byte)(input.G * percent + blend.G * (1 - percent)),
                              (byte)(input.B * percent + blend.B * (1 - percent)));
    }




    public static void ChangeAppAccentColor(Color? color)
    {
        if (color is null)
        {
            return;
        }
        if (Application.Current is not { } app)
        {
            return;
        }

        Color light1 = ColorMix(color.Value, Colors.White, 0.8);
        Color light2 = ColorMix(color.Value, Colors.White, 0.6);
        Color light3 = ColorMix(color.Value, Colors.White, 0.4);
        Color dark1 = ColorMix(color.Value, Colors.Black, 0.8);
        Color dark2 = ColorMix(color.Value, Colors.Black, 0.6);
        Color dark3 = ColorMix(color.Value, Colors.Black, 0.4);

        app.Resources["SystemAccentColor"] = color.Value;
        app.Resources["SystemAccentColorLight1"] = light1;
        app.Resources["SystemAccentColorLight2"] = light2;
        app.Resources["SystemAccentColorLight3"] = light3;
        app.Resources["SystemAccentColorDark1"] = dark1;
        app.Resources["SystemAccentColorDark2"] = dark2;
        app.Resources["SystemAccentColorDark3"] = dark3;

        WeakReferenceMessenger.Default.Send(new AccentColorChangedMessage());
    }


}
