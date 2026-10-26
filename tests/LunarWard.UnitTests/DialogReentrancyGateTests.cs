using System.Threading;
using System.Threading.Tasks;
using LunarWard.Services;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 对话框防重入闸门：连点齿轮 / 关闭按钮时只允许第一个 <c>ShowAsync</c> 进入，
/// 其余静默忽略（WinUI 的 ShowAsync 会抛「已有对话框」，这里选择不把异常甩给 async void 事件）。
/// </summary>
public class DialogReentrancyGateTests
{
    [Fact]
    public void StartsClosed()
    {
        var gate = new DialogReentrancyGate();
        Assert.False(gate.IsOpen);
    }


    [Fact]
    public void TryEnter_FirstCallSucceeds_SecondIsRefused()
    {
        var gate = new DialogReentrancyGate();

        Assert.True(gate.TryEnter());
        Assert.True(gate.IsOpen);

        Assert.False(gate.TryEnter());
        Assert.True(gate.IsOpen);
    }


    [Fact]
    public void Exit_ReleasesTheGateForTheNextDialog()
    {
        var gate = new DialogReentrancyGate();

        Assert.True(gate.TryEnter());
        gate.Exit();
        Assert.False(gate.IsOpen);

        Assert.True(gate.TryEnter());
        gate.Exit();
    }


    /// <summary>两次打开并发时只有一个赢家（Interlocked 语义，不能靠「先看 IsOpen 再赋值」）。</summary>
    [Fact]
    public async Task ConcurrentTryEnter_OnlyOneWins()
    {
        var gate = new DialogReentrancyGate();
        int winners = 0;

        using var start = new ManualResetEventSlim(false);
        var tasks = new Task[8];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                start.Wait();
                if (gate.TryEnter())
                {
                    Interlocked.Increment(ref winners);
                }
            });
        }

        start.Set();
        await Task.WhenAll(tasks);

        Assert.Equal(1, Volatile.Read(ref winners));
        Assert.True(gate.IsOpen);

        gate.Exit();
        Assert.False(gate.IsOpen);
    }
}
