using System;
using System.Linq;
using System.Reflection;
using LunarWard.Frameworks;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 外壳（<c>Features/ViewHost/MainView</c>）对页面的硬性约定：<c>Features.*</c> 下的
/// <c>*Page</c> 必须继承 <see cref="PageBase"/> 且有无参构造——因为 <c>Frameworks/Frame</c>
/// 用 <c>Activator.CreateInstance</c> 导航，上游 WinUI <c>Frame</c> 也是这个语义。
/// </summary>
/// <remarks>
/// 这条测试是 docs/10-ALIGNMENT.md「一上游文件 = 一移植文件、页面无参可实例化」规定的机器检查：
/// 任何页面若被改回旧壳基类（<c>Views.Starward.StarwardPage</c>，endgame 会删）或加了 ctor 注入，
/// 都会在这里失败，而不是等到运行时点进页面才崩。
/// </remarks>
public sealed class ViewHostPageContractTests
{
    private static Assembly AppAssembly => typeof(PageBase).Assembly;


    public static TheoryData<string> FeaturePageTypes()
    {
        var data = new TheoryData<string>();
        foreach (Type type in AppAssembly.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition)
            {
                continue;
            }
            if (type.Namespace?.StartsWith("LunarWard.Features", StringComparison.Ordinal) is not true)
            {
                continue;
            }
            if (!type.Name.EndsWith("Page", StringComparison.Ordinal))
            {
                continue;
            }
            data.Add(type.FullName!);
        }
        return data;
    }


    [Theory]
    [MemberData(nameof(FeaturePageTypes))]
    public void Feature_page_should_derive_from_PageBase(string typeName)
    {
        Type type = AppAssembly.GetType(typeName, throwOnError: true)!;
        Assert.True(typeof(PageBase).IsAssignableFrom(type),
            $"{typeName} 必须继承 LunarWard.Frameworks.PageBase（上游 PageBase），否则新外壳 Frame 无法导航。");
    }


    [Theory]
    [MemberData(nameof(FeaturePageTypes))]
    public void Feature_page_should_have_public_parameterless_ctor(string typeName)
    {
        Type type = AppAssembly.GetType(typeName, throwOnError: true)!;
        bool hasParameterless = type.GetConstructor(Type.EmptyTypes) is not null;
        Assert.True(hasParameterless,
            $"{typeName} 必须有 public 无参构造：Frameworks.Frame 用 Activator.CreateInstance 实例化页面，依赖请在页内用 AppConfig.GetService<T>() 取。");
    }
}
