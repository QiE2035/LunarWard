using LunarWard.Core;
using LunarWard.Wine;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// Wine <c>user.reg</c> 的账号读写（账号切换，P0）。
/// </summary>
/// <remarks>
/// fixture 用的是<b>真实格式</b>：段头里非 ASCII 字符写成 <c>\xHHHH</c>、
/// 值写成 <c>hex:</c> 且按 80 列换行续行。这两点都是踩坑踩出来的。
/// </remarks>
public class WineAccountTests
{
    /// <summary>
    /// 一个最小的、但格式与真实 <c>user.reg</c> 一致的片段。
    /// </summary>
    /// <remarks>
    /// 用 <c>\"\"\"</c>（raw string）写：里面的反斜杠**不做转义**，
    /// 所以这里看到的就是文件里的真实字节。注意段头里的 <c>\x7edd</c> 等不是转义，
    /// 而是 Wine 对非 ASCII 字符的写法。
    /// </remarks>
    private const string SampleUserReg = """
WINE REGISTRY Version 2
;; All keys relative to \\User\\S-1-5-21-0-0-0-1000

[Software\\miHoYo\\\x7edd\x533a\x96f6] 1792671601
#time=1d9a2b3c4e5f6a7
"MIHOYOSDK_ADL_PROD_CN_h3123967166"=hex:47,4b,30,51,6e,2b,6a,41,35,76,4b,4f,6e,\
  58,75,4e,63,77,76,6c,32,32,71,5a,77,4a,72,76,43,66,6a,75,6f,56,5a,54,31,74,
"OtherKey"="value"

[Software\\miHoYoSDK] 1790399767
"Unrelated"=dword:00000001
""";



    private static string CreatePrefix(TempDirectory temp, string content = SampleUserReg)
    {
        var prefix = temp.Combine("prefix");
        Directory.CreateDirectory(prefix);
        File.WriteAllText(Path.Join(prefix, "system.reg"), "WINE REGISTRY Version 2\n");
        File.WriteAllText(Path.Join(prefix, "user.reg"), content);
        return prefix;
    }


    [Fact]
    public void DecodeRegistryKey_DecodesUnicodeEscapes()
    {
        // 真实的 Wine 写法：绝区零 = \x7edd\x533a\x96f6
        Assert.Equal(@"Software\miHoYo\绝区零", WineAccountService.DecodeRegistryKey(@"Software\\miHoYo\\\x7edd\x533a\x96f6"));
        // ASCII 只做反斜杠还原
        Assert.Equal(@"Software\miHoYo\Genshin Impact", WineAccountService.DecodeRegistryKey(@"Software\\miHoYo\\Genshin Impact"));
        // 空值不炸
        Assert.Equal("", WineAccountService.DecodeRegistryKey(""));
    }


    [Fact]
    public void EncodeRegistryKey_RoundTrips()
    {
        foreach (var key in new[]
                 {
                     @"Software\miHoYo\绝区零",
                     @"Software\miHoYo\原神",
                     @"Software\miHoYo\Genshin Impact",
                     @"Software\Cognosphere\Star Rail",
                 })
        {
            var encoded = WineAccountService.EncodeRegistryKey(key);
            Assert.Equal(key, WineAccountService.DecodeRegistryKey(encoded));
        }
    }


    [Fact]
    public void Read_ParsesHexValueFromRealFormat()
    {
        using var temp = new TempDirectory();
        var prefix = CreatePrefix(temp);
        var service = new WineAccountService(prefix);

        var snapshot = service.Read(GameBiz.nap_cn);

        Assert.NotNull(snapshot);
        Assert.Equal(GameBiz.nap_cn, snapshot!.GameBiz);
        // 首行 15 字节 + 续行 23 字节（fixture 里就是这么多）
        Assert.Equal(38, snapshot.Value.Length);
        Assert.Equal(64, snapshot.Sha256.Length); // SHA-256 十六进制 = 32 字节 × 2
        // 前三个字节是 47,4b,30 —— 即 "GK0"
        Assert.Equal(0x47, snapshot.Value[0]);   // 'G'
        Assert.Equal(0x4b, snapshot.Value[1]);   // 'K'
        Assert.Equal(0x30, snapshot.Value[2]);   // '0'
        Assert.Equal(GameAccountSnapshot.ComputeSha256(snapshot.Value), snapshot.Sha256);
    }


    [Fact]
    public void Read_MissingSection_ReturnsNull()
    {
        using var temp = new TempDirectory();
        var prefix = CreatePrefix(temp);
        var service = new WineAccountService(prefix);

        // 样例里没有原神的段
        Assert.Null(service.Read(GameBiz.hk4e_cn));
    }


    [Fact]
    public void Read_NoUserReg_ReturnsNull()
    {
        using var temp = new TempDirectory();
        var service = new WineAccountService(temp.Combine("nope"));
        Assert.False(service.IsAvailable);
        Assert.Null(service.Read(GameBiz.nap_cn));
    }


    [Theory]
    [InlineData(GameBiz.hk4e_cn, "MIHOYOSDK_ADL_PROD_CN_h3123967166", @"Software\miHoYo\原神")]
    [InlineData(GameBiz.hk4e_bilibili, "MIHOYOSDK_ADL_PROD_CN_h3123967166", @"Software\miHoYo\原神")]
    [InlineData(GameBiz.hk4e_global, "MIHOYOSDK_ADL_PROD_OVERSEA_h1158948810", @"Software\miHoYo\Genshin Impact")]
    [InlineData(GameBiz.nap_cn, "MIHOYOSDK_ADL_PROD_CN_h3123967166", @"Software\miHoYo\绝区零")]
    [InlineData(GameBiz.nap_global, "MIHOYOSDK_ADL_PROD_OVERSEA_h1158948810", @"Software\miHoYo\ZenlessZoneZero")]
    [InlineData(GameBiz.hkrpg_cn, "MIHOYOSDK_ADL_PROD_CN_h3123967166", @"Software\miHoYo\崩坏：星穹铁道")]
    public void KeyAndSectionMapping(string gameBiz, string expectedKey, string expectedSection)
    {
        Assert.Equal(expectedKey, WineAccountService.GetAccountKeyName(gameBiz));
        Assert.Equal(expectedSection, WineAccountService.GetSectionName(gameBiz));
    }


    [Fact]
    public void Write_SameValue_LeavesFileByteIdentical()
    {
        using var temp = new TempDirectory();
        var prefix = CreatePrefix(temp);
        var service = new WineAccountService(prefix);
        var path = Path.Join(prefix, "user.reg");
        var before = File.ReadAllText(path);

        var snapshot = service.Read(GameBiz.nap_cn);
        var error = service.Write(snapshot!);

        // 写同一个值不应该改动任何字节（这里是"内容没有变化"而不是失败）
        Assert.NotNull(error);
        Assert.Equal(before, File.ReadAllText(path));
    }


    [Fact]
    public void Write_DifferentValue_ChangesOnlyTheTargetKey()
    {
        using var temp = new TempDirectory();
        var prefix = CreatePrefix(temp);
        var service = new WineAccountService(prefix);
        var path = Path.Join(prefix, "user.reg");

        var original = service.Read(GameBiz.nap_cn)!;
        var other = new GameAccountSnapshot
        {
            GameBiz = GameBiz.nap_cn,
            Uid = 999888777,
            Value = (byte[])original.Value.Clone(),
        };
        other.Value[0] ^= 0xFF;
        other.Sha256 = GameAccountSnapshot.ComputeSha256(other.Value);

        var beforeLines = File.ReadAllLines(path);
        var error = service.Write(other);
        Assert.Null(error);

        var afterLines = File.ReadAllLines(path);

        // 段数不变
        Assert.Equal(beforeLines.Count(l => l.StartsWith('[')), afterLines.Count(l => l.StartsWith('[')));

        // 读回来就是新值
        var readBack = service.Read(GameBiz.nap_cn)!;
        Assert.Equal(other.Sha256, readBack.Sha256);

        // 不相关的键没有被碰
        Assert.Contains(afterLines, l => l.Contains("\"OtherKey\"=\"value\""));
        Assert.Contains(afterLines, l => l.Contains("\"Unrelated\"=dword:00000001"));
        Assert.Contains(afterLines, l => l.Contains("WINE REGISTRY Version 2"));
    }


    [Fact]
    public void Write_PreservesHexLineLayout()
    {
        using var temp = new TempDirectory();
        var prefix = CreatePrefix(temp);
        var service = new WineAccountService(prefix);
        var path = Path.Join(prefix, "user.reg");

        var original = service.Read(GameBiz.nap_cn)!;
        var other = new GameAccountSnapshot
        {
            GameBiz = GameBiz.nap_cn,
            Value = (byte[])original.Value.Clone(),
        };
        other.Value[0] ^= 0xFF;
        other.Sha256 = GameAccountSnapshot.ComputeSha256(other.Value);

        var beforeLineCount = File.ReadAllLines(path).Length;
        service.Write(other);
        var afterLineCount = File.ReadAllLines(path).Length;

        // 等长替换必须保持行数不变（否则就是把整段重新格式化了）
        Assert.Equal(beforeLineCount, afterLineCount);
    }


    [Fact]
    public void Write_EmptyValue_IsRejected()
    {
        using var temp = new TempDirectory();
        var prefix = CreatePrefix(temp);
        var service = new WineAccountService(prefix);

        var error = service.Write(new GameAccountSnapshot
        {
            GameBiz = GameBiz.nap_cn,
            Value = [],
        });

        Assert.NotNull(error);
        Assert.Contains("为空", error);
    }


    [Fact]
    public void Write_UnsupportedGameBiz_IsRejected()
    {
        using var temp = new TempDirectory();
        var prefix = CreatePrefix(temp);
        var service = new WineAccountService(prefix);

        var error = service.Write(new GameAccountSnapshot { GameBiz = "unknown_biz", Value = [1, 2, 3] });
        Assert.NotNull(error);
    }


    [Fact]
    public void Write_NoUserReg_IsRejected()
    {
        using var temp = new TempDirectory();
        var service = new WineAccountService(temp.Combine("nope"));

        var error = service.Write(new GameAccountSnapshot { GameBiz = GameBiz.nap_cn, Value = [1] });
        Assert.NotNull(error);
        Assert.Contains("user.reg", error);
    }


    [Fact]
    public void Write_MissingSection_AppendsNewSection()
    {
        using var temp = new TempDirectory();
        var prefix = CreatePrefix(temp);
        var service = new WineAccountService(prefix);
        var path = Path.Join(prefix, "user.reg");

        var before = File.ReadAllLines(path).Count(l => l.StartsWith('['));

        var error = service.Write(new GameAccountSnapshot
        {
            GameBiz = GameBiz.hk4e_cn,
            Value = [0xAA, 0xBB, 0xCC],
            Uid = 100000000,
        });

        Assert.Null(error);
        var after = File.ReadAllText(path);
        Assert.Equal(before + 1, after.Split('\n').Count(l => l.StartsWith('[')));

        // 段头必须用 Wine 的转义写法（原神 = \x539f\x795e）
        Assert.Contains(@"\x539f\x795e]", after);
        Assert.Contains("aa,bb,cc", after);
    }


    [Fact]
    public void Read_BeforeWrite_GetSnapshotThenRestore()
    {
        using var temp = new TempDirectory();
        var prefix = CreatePrefix(temp);
        var service = new WineAccountService(prefix);

        var snapshot = service.Read(GameBiz.nap_cn)!;
        var sha = snapshot.Sha256;

        var other = new GameAccountSnapshot
        {
            GameBiz = GameBiz.nap_cn,
            Value = (byte[])snapshot.Value.Clone(),
        };
        other.Value[^1] ^= 0xFF;
        other.Sha256 = GameAccountSnapshot.ComputeSha256(other.Value);

        service.Write(other);
        Assert.NotEqual(sha, service.Read(GameBiz.nap_cn)!.Sha256);

        // 恢复
        service.Write(snapshot);
        Assert.Equal(sha, service.Read(GameBiz.nap_cn)!.Sha256);
    }
}
