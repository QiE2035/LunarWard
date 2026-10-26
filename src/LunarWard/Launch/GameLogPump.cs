using System.IO;

namespace LunarWard.Launch;

/// <summary>
/// 游戏日志泵：把子进程 stdout/stderr 搬到日志文件，超过上限后继续消费但不再落盘。
/// </summary>
/// <remarks>
/// <para>
/// 审计 M-04 / RK-03：子进程以 <c>RedirectOutput = true</c> 启动（<see cref="LaunchEngine"/>），
/// 旧实现读满 8 MiB 就 <c>break</c>。读端退出后管道缓冲区一旦写满，子进程的下一次
/// <c>write</c> 就会阻塞 —— Proton/DXVK 打开调试日志时足够触发，表现为「游戏永远不出画面，
/// launcher 永远显示运行中」。
/// </para>
/// <para>
/// 这里把「读」和「写」拆开：读取永远读到 EOF，只在超过上限后跳过写盘，并把截断事件通过
/// <c>warn</c> 上报一次。抽成独立类是为了能单测（不需要真的起子进程）。
/// </para>
/// </remarks>
public static class GameLogPump
{
    /// <summary>
    /// 把 <paramref name="reader"/> 的内容逐行写入 <paramref name="target"/>。
    /// </summary>
    /// <param name="reader">子进程输出（调用方负责生命周期）。</param>
    /// <param name="target">日志文件流；写满 <paramref name="limitBytes"/> 后不再写入。</param>
    /// <param name="limitBytes">落盘上限（字节）。</param>
    /// <param name="warn">截断/异常时的告警出口，可为空。</param>
    /// <returns>实际写入的行数（超出上限后不再增加）。</returns>
    public static int Pump(TextReader reader, Stream target, long limitBytes, Action<string>? warn = null)
    {
        int written = 0;
        bool truncated = false;
        try
        {
            using var writer = new StreamWriter(target, leaveOpen: true) { AutoFlush = true };
            string? line;

            // 读取时不持有目标流的锁：stdout/stderr 两个泵并行阻塞在 ReadLine 上，
            // 若把锁包住整个循环，一个泵等 stderr、另一个等 stdout 就会互等死锁。
            // 只有「判断长度 + 写 」需要互斥。
            while ((line = reader.ReadLine()) is not null)
            {
                lock (target)
                {
                    if (!truncated && target.Length > limitBytes)
                    {
                        truncated = true;
                        warn?.Invoke($"游戏日志超过上限（{limitBytes} 字节），后续输出不再落盘（继续消费管道以免游戏阻塞）。");
                    }

                    if (!truncated)
                    {
                        writer.WriteLine(line);
                        written++;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // 进程退出后读流会抛异常，正常；其他异常至少留痕。
            warn?.Invoke($"游戏日志泵异常：{ex.Message}");
        }

        return written;
    }
}
