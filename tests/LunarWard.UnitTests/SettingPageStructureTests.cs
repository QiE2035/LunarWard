using System.ComponentModel;
using System.Reflection;
using LunarWard.Features.Setting;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 设置页「code-behind 承载成员 + DataContext=this」的结构约束
/// （对齐上游 <c>Starward/src/Starward/Features/Setting/*.xaml.cs</c>）。
/// </summary>
public class SettingPageStructureTests
{
    /// <summary>上游 Starward 的二级设置页（NavigationViewItem 的 Tag）。</summary>
    private static readonly (string Tag, Type Type)[] Pages =
    [
        ("AboutSetting", typeof(AboutSetting)),
        ("GeneralSetting", typeof(GeneralSetting)),
        ("DownloadSetting", typeof(DownloadSetting)),
        ("FileManageSetting", typeof(FileManageSetting)),
        ("ScreenshotSetting", typeof(ScreenshotSetting)),
        ("HotkeySetting", typeof(HotkeySetting)),
        ("GamepadControlSetting", typeof(GamepadControlSetting)),
        ("AdvancedSetting", typeof(AdvancedSetting)),
        ("ToolboxSetting", typeof(ToolboxSetting)),
    ];


    [Fact]
    public void Pages_LiveInFeaturesSetting_AndDeriveFromSettingPageBase()
    {
        foreach (var (tag, type) in Pages)
        {
            Assert.Equal("LunarWard.Features.Setting", type.Namespace);
            Assert.Equal(tag, type.Name);
            Assert.True(typeof(SettingPageBase).IsAssignableFrom(type), $"{type.Name} 应继承 SettingPageBase");
            Assert.True(typeof(LunarWard.Views.Starward.StarwardPage).IsAssignableFrom(type));
            Assert.True(type.IsSealed, $"{type.Name} 与上游页面一样是 sealed");
        }
    }


    /// <summary>
    /// 绑定通知必须走本类实现的 <see cref="INotifyPropertyChanged"/>：
    /// <see cref="Avalonia.AvaloniaObject"/> 也有同名事件，接口映射一旦指向基类，
    /// 页面自己的 CLR 属性变更就通知不到 XAML。
    /// </summary>
    [Fact]
    public void PropertyChangedInterface_MapsToSettingPageBaseImplementation()
    {
        var declared = typeof(SettingPageBase).GetEvent(nameof(INotifyPropertyChanged.PropertyChanged));
        Assert.NotNull(declared);
        Assert.Equal(typeof(SettingPageBase), declared!.DeclaringType);

        Assert.Equal(typeof(SettingPageBase), MappedAddHandlerOwner(typeof(SettingPageBase)));

        foreach (var (_, type) in Pages)
        {
            Assert.Equal(typeof(SettingPageBase), MappedAddHandlerOwner(type));
        }
    }


    /// <summary>返回 <see cref="INotifyPropertyChanged.PropertyChanged"/> 的 add 访问器实际声明类型。</summary>
    private static Type? MappedAddHandlerOwner(Type type)
    {
        var addMethod = typeof(INotifyPropertyChanged)
            .GetEvent(nameof(INotifyPropertyChanged.PropertyChanged))!.AddMethod!;
        var map = type.GetInterfaceMap(typeof(INotifyPropertyChanged));
        for (var i = 0; i < map.InterfaceMethods.Length; i++)
        {
            if (map.InterfaceMethods[i] == addMethod)
            {
                return map.TargetMethods[i].DeclaringType;
            }
        }

        return null;
    }


    /// <summary>宿主页与上游 <c>SettingPage.xaml</c> 一样是 <c>Features/Setting</c> 下的页面。</summary>
    [Fact]
    public void HostPage_IsInFeaturesSetting_AndHasSectionNavigation()
    {
        var type = typeof(SettingPage);
        Assert.Equal("LunarWard.Features.Setting", type.Namespace);
        Assert.True(typeof(LunarWard.Views.Starward.StarwardPage).IsAssignableFrom(type));

        var select = type.GetMethod("SelectSection", BindingFlags.Public | BindingFlags.Instance, [typeof(string)]);
        Assert.NotNull(select);

        // 上游 NavigationViewItem.Tag 的全部取值都要能解析成枚举（含 Files 短名与 GamepadControlSetting）
        var kind = typeof(SettingPage).Assembly.GetType("LunarWard.Features.Setting.SettingSectionKind");
        Assert.NotNull(kind);
        Assert.Equal(9, Enum.GetValues(kind!).Length);
    }


    [Theory]
    [InlineData("AboutSetting", "About")]
    [InlineData("GeneralSetting", "General")]
    [InlineData("Files", "FileManage")]
    [InlineData("GamepadControlSetting", "Gamepad")]
    [InlineData("KeyBoardShortcuts", "Hotkey")]
    [InlineData("advanced", "Advanced")]
    public void SectionTags_ParseToMatchingKind(string tag, string expected)
    {
        var parse = typeof(SettingPage).Assembly.GetType("LunarWard.Features.Setting.SettingSectionTags")!
            .GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, [typeof(string)])!;
        var result = parse.Invoke(null, [tag]);
        Assert.NotNull(result);
        Assert.Equal(expected, result!.ToString());
    }
}
