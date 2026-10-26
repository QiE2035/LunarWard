using LunarWard.Core.Gacha;

namespace LunarWard.Features.Gacha;

public class GachaBanner
{


    public IGachaType GachaType { get; private set; }


    public int Value => GachaType.Value;


    public string ToLocalization() => GachaType.ToLocalization();


    /// <summary>
    /// XAML 用：上游 WinUI 的 <c>Text="{x:Bind ToLocalization()}"</c> 是函数绑定，
    /// Avalonia 的编译绑定/反射绑定都不支持调用方法，这里提供一个只读属性作为绑定目标
    /// （docs/09「二十二、全量 XAML 重编译暴露的共性问题」第 1 条）。
    /// </summary>
    public string LocalizedName => ToLocalization();



    public GachaBanner(IGachaType gachaType)
    {
        GachaType = gachaType;
    }


}
