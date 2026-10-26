using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 会读写进程级静态状态（例如 <c>GachaLogClient.InstallPathProvider</c>）的测试集合。
/// </summary>
/// <remarks>
/// <see cref="LunarWard.AppServices"/> 的构造函数会把「Wine 安装路径提供者」装进
/// <c>GachaLogClient</c> 的静态属性；xUnit 默认并行跑不同测试类，于是「构造过 AppServices 的类」
/// 和「断言该静态属性为 null 的类」会互相干扰（CI Release 上真实复现过一次）。
/// 把这类测试放进同一个不可并行化的集合，保证顺序确定。
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GlobalStaticsCollection
{
    public const string Name = "GlobalStatics";
}
