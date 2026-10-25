using LunarWard.Launch;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 审计 M-04 / RK-03：日志泵读满上限后必须继续消费管道，否则子进程写满缓冲区就卡死。
/// </summary>
public sealed class GameLogPumpTests
{
    [Fact]
    public void Pump_StopsWritingAfterLimit_ButConsumesWholeReader()
    {
        // 20 行 × 100 字节 = 2000 字节；上限 500 字节。
        string[] lines = [.. Enumerable.Range(0, 20).Select(i => new string((char)('a' + i % 26), 100))];
        using var reader = new StringReader(string.Join('\n', lines));
        using var target = new MemoryStream();

        List<string> warnings = [];
        int written = GameLogPump.Pump(reader, target, 500, warnings.Add);

        // 读端必须读到底（否则子进程会阻塞在管道写）
        Assert.Equal(-1, reader.Peek());

        // 落盘被截断，且只告警一次
        Assert.True(written < lines.Length, $"应在上限处停止落盘，实际写入 {written} 行");
        Assert.Single(warnings);
        Assert.Contains("上限", warnings[0]);
    }


    [Fact]
    public void Pump_UnderLimit_WritesEveryLine()
    {
        using var reader = new StringReader("第一行\n第二行\n第三行");
        using var target = new MemoryStream();

        int written = GameLogPump.Pump(reader, target, 1024 * 1024);

        Assert.Equal(3, written);
    }


    [Fact]
    public void Pump_ReportsReaderFailure()
    {
        using var target = new MemoryStream();
        List<string> warnings = [];

        int written = GameLogPump.Pump(new ThrowingReader(), target, 1024, warnings.Add);

        Assert.Equal(0, written);
        Assert.Single(warnings);
    }


    private sealed class ThrowingReader : TextReader
    {
        public override string? ReadLine() => throw new IOException("boom");
    }
}
