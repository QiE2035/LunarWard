using System.Diagnostics;
using LunarWard.Installer;
using Xunit;

namespace LunarWard.Installer.UnitTests;

/// <summary>
/// hpatchz 调用层的行为测试：不上网、不依赖系统是否装 hdiffpatch，
/// 用环境变量指向临时脚本模拟外部 CLI。
/// </summary>
public sealed class DiffPatcherTests : IDisposable
{
    private readonly string _dir = Path.Join(Path.GetTempPath(), "lunarward-hpatch-test-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string? _previousEnv = Environment.GetEnvironmentVariable(HdiffPatchPatcher.ExecutableEnvironmentVariable);

    public DiffPatcherTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(HdiffPatchPatcher.ExecutableEnvironmentVariable, _previousEnv);
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private string WriteStub(string script)
    {
        var path = Path.Join(_dir, "hpatchz");
        File.WriteAllText(path, script);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }

    [Fact]
    public void TryPatch_ReturnsFalse_WhenDiffMissing()
    {
        Environment.SetEnvironmentVariable(HdiffPatchPatcher.ExecutableEnvironmentVariable, "/bin/true");
        var patcher = new HdiffPatchPatcher();

        Assert.True(patcher.IsAvailable);
        Assert.False(patcher.TryPatch(null, Path.Join(_dir, "nope.diff"), new MemoryStream()));
    }

    [Fact]
    public void TryPatch_ReturnsFalse_WhenExecutableUnavailable()
    {
        // 指到一个不存在的路径：解析成 null（未安装 hpatchz 的等价情形）。
        Environment.SetEnvironmentVariable(HdiffPatchPatcher.ExecutableEnvironmentVariable, Path.Join(_dir, "missing-hpatchz"));
        var patcher = new HdiffPatchPatcher();

        if (patcher.Executable is null)
        {
            Assert.False(patcher.IsAvailable);
            Assert.False(patcher.TryPatch(null, Path.Join(_dir, "nope.diff"), new MemoryStream()));
        }
        else
        {
            // 机器上恰好有 hpatchz 且实现选择了它；这里只断言不会抛异常。
            Assert.False(patcher.TryPatch(null, Path.Join(_dir, "nope.diff"), new MemoryStream()));
        }
    }

    [Fact]
    public void TryPatch_NoSource_CopiesStubOutput()
    {
        // 无源文件时参数序：hpatchz -f <diff> <target>
        Environment.SetEnvironmentVariable(
            HdiffPatchPatcher.ExecutableEnvironmentVariable, WriteStub("#!/bin/sh\ncp \"$2\" \"$3\"\n"));

        var diff = Path.Join(_dir, "full.diff");
        File.WriteAllText(diff, "PATCHED-CONTENT");

        using var target = new MemoryStream();
        Assert.True(new HdiffPatchPatcher().TryPatch(null, diff, target));
        Assert.Equal("PATCHED-CONTENT", System.Text.Encoding.UTF8.GetString(target.ToArray()));
    }

    [Fact]
    public void TryPatch_WithSource_UsesSourceArguments()
    {
        // 有源文件时参数序：hpatchz <source> <diff> <target>
        Environment.SetEnvironmentVariable(
            HdiffPatchPatcher.ExecutableEnvironmentVariable, WriteStub("#!/bin/sh\ncp \"$2\" \"$3\"\n"));

        var source = Path.Join(_dir, "old.bin");
        var diff = Path.Join(_dir, "delta.diff");
        File.WriteAllText(source, "OLD");
        File.WriteAllText(diff, "NEW-FROM-DIFF");

        using var target = new MemoryStream();
        Assert.True(new HdiffPatchPatcher().TryPatch(source, diff, target));
        Assert.Equal("NEW-FROM-DIFF", System.Text.Encoding.UTF8.GetString(target.ToArray()));
    }

    [Fact]
    public void TryPatch_LosingStub_FailsCleanly()
    {
        Environment.SetEnvironmentVariable(
            HdiffPatchPatcher.ExecutableEnvironmentVariable, WriteStub("#!/bin/sh\nexit 3\n"));
        var diff = Path.Join(_dir, "bad.diff");
        File.WriteAllText(diff, "x");

        using var target = new MemoryStream();
        Assert.False(new HdiffPatchPatcher().TryPatch(null, diff, target));
    }
}
