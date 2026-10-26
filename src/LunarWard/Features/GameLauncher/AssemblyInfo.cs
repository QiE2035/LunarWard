using System.Runtime.CompilerServices;

// 上游 Starward 的 GameLauncherService / GameSelector 等类型是 internal（同上），
// 单测要覆盖 StartGameAsync 的纯逻辑部分（GetGameExeName、BuildStartInfo 的环境/命令行拼装），
// 又不能为了测试把它们改成 public（会偏离上游可见性），故只对测试程序集暴露 internal。
[assembly: InternalsVisibleTo("LunarWard.UnitTests")]
