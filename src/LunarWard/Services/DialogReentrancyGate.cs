using System;
using System.Threading;

namespace LunarWard.Services;

/// <summary>
/// 对话框防重入闸门：同一时刻只允许一个 ContentDialog 浮层。
/// </summary>
/// <remarks>
/// 从 <see cref="DialogService"/> 里拆出来单独成类，是为了让「第二次请求被忽略」这条规则能用
/// 单元测试覆盖 —— 测试进程里没有 Avalonia UI 线程，不能实例化控件。
/// 线程安全：<see cref="TryEnter"/> 用 <c>Interlocked.CompareExchange</c>，
/// 所以哪怕两个 async 流程在 UI 线程外同时请求，也只有一个能进。
/// </remarks>
public sealed class DialogReentrancyGate
{
    private int _entered;


    /// <summary>当前是否已经有对话框在显示。</summary>
    public bool IsOpen => Volatile.Read(ref _entered) != 0;


    /// <summary>尝试进入；已经在显示时返回 <c>false</c>（调用方应直接忽略这次请求）。</summary>
    public bool TryEnter()
    {
        if (Interlocked.CompareExchange(ref _entered, 1, 0) == 0)
        {
            return true;
        }

        return false;
    }


    /// <summary>释放闸门；重复调用是幂等的。</summary>
    public void Exit() => Interlocked.Exchange(ref _entered, 0);
}
