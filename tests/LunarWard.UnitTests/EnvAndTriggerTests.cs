using LunarWard.Launch;
using LunarWard.Wine;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>U5 — <c>EnvPatchKind</c> 三态矩阵。</summary>
public class EnvPatchTests
{
    [Fact]
    public void OverrideEmptyString_IsNotRemove()
    {
        var program = new ExternalProgram
        {
            Id = "p",
            Path = "/bin/true",
            Env =
            [
                new EnvEntry { Key = "DISPLAY", Kind = EnvPatchKind.Override, Value = "" },
                new EnvEntry { Key = "UNWANTED", Kind = EnvPatchKind.Remove },
            ],
        };

        var inherited = new Dictionary<string, string>
        {
            ["DISPLAY"] = ":0",
            ["UNWANTED"] = "1",
            ["KEEP"] = "yes",
        };

        var env = program.ApplyEnv(inherited);

        // 存在且为空
        Assert.True(env.ContainsKey("DISPLAY"));
        Assert.Equal("", env["DISPLAY"]);

        // 不存在
        Assert.False(env.ContainsKey("UNWANTED"));

        Assert.Equal("yes", env["KEEP"]);
    }


    [Fact]
    public void Inherit_DoesNotTouchValue()
    {
        var program = new ExternalProgram
        {
            Id = "p",
            Path = "/bin/true",
            Env = [new EnvEntry { Key = "PATH", Kind = EnvPatchKind.Inherit, Value = "/nope" }],
        };

        var env = program.ApplyEnv(new Dictionary<string, string> { ["PATH"] = "/usr/bin" });
        Assert.Equal("/usr/bin", env["PATH"]);
    }


    [Fact]
    public void Override_OnMissingKey_AddsIt()
    {
        var program = new ExternalProgram
        {
            Id = "p",
            Path = "/bin/true",
            Env = [new EnvEntry { Key = "NEW", Kind = EnvPatchKind.Override, Value = "v" }],
        };

        var env = program.ApplyEnv(new Dictionary<string, string>());
        Assert.Equal("v", env["NEW"]);
    }


    [Fact]
    public void Remove_OnMissingKey_IsNoOp()
    {
        var program = new ExternalProgram
        {
            Id = "p",
            Path = "/bin/true",
            Env = [new EnvEntry { Key = "GONE", Kind = EnvPatchKind.Remove }],
        };

        var env = program.ApplyEnv(new Dictionary<string, string> { ["A"] = "1" });
        Assert.Single(env);
    }


    [Fact]
    public void ApplyEnv_DoesNotMutateInherited()
    {
        var inherited = new Dictionary<string, string> { ["X"] = "1" };
        var program = new ExternalProgram
        {
            Id = "p",
            Path = "/bin/true",
            Env = [new EnvEntry { Key = "X", Kind = EnvPatchKind.Remove }],
        };

        program.ApplyEnv(inherited);
        Assert.Equal("1", inherited["X"]);
    }


    [Theory]
    [InlineData(EnvPatchKind.Inherit, true)]
    [InlineData(EnvPatchKind.Override, true)]
    [InlineData(EnvPatchKind.Remove, false)]
    public void ThreeStateMatrix(EnvPatchKind kind, bool keySurvives)
    {
        var program = new ExternalProgram
        {
            Id = "p",
            Path = "/bin/true",
            Env = [new EnvEntry { Key = "K", Kind = kind, Value = kind == EnvPatchKind.Override ? "" : null }],
        };
        var env = program.ApplyEnv(new Dictionary<string, string> { ["K"] = "orig" });
        Assert.Equal(keySurvives, env.ContainsKey("K"));
    }
}


/// <summary>U8 — <c>Trigger × ProgramKind</c> 合法性。</summary>
public class TriggerTests
{
    [Fact]
    public void Trigger_HasExactlyTwoValues()
    {
        var values = Enum.GetValues<Trigger>();
        Assert.Equal(2, values.Length);
        Assert.Equal([Trigger.Auto, Trigger.Manual], values);
    }


    [Fact]
    public void AutoWindowsExe_ProducesSecondClientWarning()
    {
        using var temp = new TempDirectory();
        var exe = temp.Combine("inject.exe");
        File.WriteAllText(exe, "");

        var program = new ExternalProgram
        {
            Id = "p",
            Path = exe,
            Kind = ProgramKind.WindowsExe,
            Trigger = Trigger.Auto,
        };

        var warnings = program.GetWarnings(new Dictionary<string, string?>()).ToList();
        Assert.Contains(warnings, w => w.Message.Contains("第二个客户端"));
    }


    [Fact]
    public void ManualWindowsExe_NoSecondClientWarning()
    {
        using var temp = new TempDirectory();
        var exe = temp.Combine("tool.exe");
        File.WriteAllText(exe, "");

        var program = new ExternalProgram
        {
            Id = "p",
            Path = exe,
            Kind = ProgramKind.WindowsExe,
            Trigger = Trigger.Manual,
        };

        var warnings = program.GetWarnings(new Dictionary<string, string?>()).ToList();
        Assert.DoesNotContain(warnings, w => w.Message.Contains("第二个客户端"));
    }


    [Fact]
    public void MissingFile_ProducesErrorWarning_ButDoesNotThrow()
    {
        var program = new ExternalProgram
        {
            Id = "p",
            Path = "/definitely/does/not/exist.exe",
            Trigger = Trigger.Manual,
        };

        var warnings = program.GetWarnings(new Dictionary<string, string?>()).ToList();
        Assert.Contains(warnings, w => w.Severity == WarningSeverity.Error);
    }


    [Theory]
    [InlineData("hk4e-touch.exe", "伴随启动")]
    [InlineData("zzz-touch-unlocker.exe", "伴随启动")]
    [InlineData("myhook.dll", "伴随启动")]
    [InlineData("map-tool.exe", "手动启动")]
    [InlineData("plain.exe", null)]
    public void HeuristicSuggestion(string fileName, string? expectedFragment)
    {
        var program = new ExternalProgram { Id = "p", Path = fileName };
        var suggestion = program.GetSuggestion();
        if (expectedFragment is null)
        {
            Assert.Null(suggestion);
        }
        else
        {
            Assert.NotNull(suggestion);
            Assert.Contains(expectedFragment, suggestion);
        }
    }
}
