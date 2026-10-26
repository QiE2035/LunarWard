namespace LunarWard.RPC;

/// <summary>
/// 平台替换：上游 Starward.RPC 是库工程，由主程序带 <c>rpc</c> 参数启动。
/// Linux 上为了能独立启动/自测，这里补一个入口，并把参数归一化成上游的形状（args[0] == "rpc"）。
/// </summary>
internal static class EntryPoint
{

    private static void Main(string[] args)
    {
        if (args.Length == 0 || !string.Equals(args[0], "rpc", StringComparison.OrdinalIgnoreCase))
        {
            args = ["rpc", .. args];
        }
        RpcRunner.Run(args);
    }

}
