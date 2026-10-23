using System.Security.Cryptography;
using System.Text;
using LunarWard.Core;
using LunarWard.Platform;

namespace LunarWard.Games;

/// <summary>
/// 一个已保存的账号快照。
/// </summary>
/// <remarks>
/// <para>
/// 上游 Starward 把游戏写进注册表的 <c>MIHOYOSDK_ADL_PROD_*</c> 二进制值
/// 当作"账号"：切换账号 = 把这个 blob 写回去。Linux 下同样的数据在
/// Wine prefix 的 <c>user.reg</c> 里，格式是 <c>hex:47,4b,30,...</c>。
/// </para>
/// <para>
/// <b>安全</b>：<see cref="Value"/> 是**账号凭据**（内含 cookie 明文）。
/// 只存本地、权限 0600、不写日志、不做任何上传。
/// </para>
/// </remarks>
public sealed class GameAccountSnapshot
{
    /// <summary>值内容的 SHA-256（十六进制大写），用于去重与标识。</summary>
    public string Sha256 { get; set; } = "";

    public string GameBiz { get; set; } = "";

    public long Uid { get; set; }

    /// <summary>用户起的名字（默认用 UID 或 "-"）。</summary>
    public string Name { get; set; } = "";

    /// <summary>注册表值的原始字节。</summary>
    public byte[] Value { get; set; } = [];

    public DateTime Time { get; set; } = DateTime.Now;


    public static string ComputeSha256(byte[] value)
        => Convert.ToHexString(SHA256.HashData(value));
}


/// <summary>
/// Wine <c>user.reg</c> 的账号读写。
/// </summary>
/// <remarks>
/// <para>
/// 只做<b>账号相关键</b>的读写（<c>MIHOYOSDK_ADL_PROD_*</c> / <c>__LastUid__*</c> /
/// <c>App_LastUserID*</c>），其余内容<b>原样保留</b>：
/// 解析 → 改目标键 → 原子写回。任何解析失败都拒绝写入（绝不破坏用户的 prefix）。
/// </para>
/// <para>
/// 键名里的游戏名是 UTF-8 中文（如 <c>[Software\\miHoYo\\原神]</c>），
/// <c>user.reg</c> 是 UTF-8 文本，直接按字节匹配即可。
/// </para>
/// </remarks>
public sealed class WineAccountService
{
    /// <summary>国内服用的键。</summary>
    public const string AdlProdCn = "MIHOYOSDK_ADL_PROD_CN_h3123967166";

    /// <summary>国际服用的键。</summary>
    public const string AdlProdOversea = "MIHOYOSDK_ADL_PROD_OVERSEA_h1158948810";

    public const string Adl0 = "MIHOYOSDK_ADL_0";
    public const string LastUidHk4e = "__LastUid___h2153286551";
    public const string LastUserIdHkrpg = "App_LastUserID_h2841727341";
    public const string LastUserIdBh3 = "GENERAL_DATA_V2_LastLoginUserId_h47158221";


    private readonly string _prefix;


    public WineAccountService(string prefix)
    {
        _prefix = prefix;
    }


    public string UserRegPath => Path.Join(_prefix, "user.reg");


    public bool IsAvailable => File.Exists(UserRegPath);


    /// <summary>
    /// 该 GameBiz 对应的账号键名。
    /// </summary>
    public static string? GetAccountKeyName(string gameBiz) => gameBiz switch
    {
        GameBiz.hk4e_cn or GameBiz.hk4e_bilibili => AdlProdCn,
        GameBiz.hk4e_global => AdlProdOversea,
        GameBiz.hkrpg_cn or GameBiz.hkrpg_bilibili => AdlProdCn,
        GameBiz.hkrpg_global => AdlProdOversea,
        GameBiz.nap_cn or GameBiz.nap_bilibili => AdlProdCn,
        GameBiz.nap_global => AdlProdOversea,
        GameBiz.bh3_cn => AdlProdCn,
        GameBiz.bh3_global or GameBiz.bh3_os or GameBiz.bh3_jp or GameBiz.bh3_kr
            or GameBiz.bh3_asia => AdlProdOversea,
        _ => null,
    };


    /// <summary>该 GameBiz 的注册表段落名（Wine 侧，不含 HKEY_CURRENT_USER\\）。</summary>
    public static string? GetSectionName(string gameBiz) => gameBiz switch
    {
        GameBiz.hk4e_cn or GameBiz.hk4e_bilibili => @"Software\miHoYo\原神",
        GameBiz.hk4e_global => @"Software\miHoYo\Genshin Impact",
        GameBiz.hkrpg_cn or GameBiz.hkrpg_bilibili => @"Software\miHoYo\崩坏：星穹铁道",
        GameBiz.hkrpg_global => @"Software\Cognosphere\Star Rail",
        GameBiz.nap_cn or GameBiz.nap_bilibili => @"Software\miHoYo\绝区零",
        GameBiz.nap_global => @"Software\miHoYo\ZenlessZoneZero",
        GameBiz.bh3_cn => @"Software\miHoYo\崩坏3",
        GameBiz.bh3_global => @"Software\miHoYo\Honkai Impact 3rd",
        GameBiz.bh3_os => @"Software\miHoYo\Honkai Impact 3",
        GameBiz.bh3_asia => @"Software\miHoYo\崩壊3rd",
        GameBiz.bh3_jp => @"Software\miHoYo\崩壊3rd",
        GameBiz.bh3_kr => @"Software\miHoYo\붕괴3rd",
        _ => null,
    };


    /// <summary>
    /// 读取当前账号快照；该 key 不存在或 prefix 不可读时返回 <c>null</c>。
    /// </summary>
    public GameAccountSnapshot? Read(string gameBiz)
    {
        var section = GetSectionName(gameBiz);
        var keyName = GetAccountKeyName(gameBiz);
        if (section is null || keyName is null || !IsAvailable)
        {
            return null;
        }

        string content;
        try
        {
            content = File.ReadAllText(UserRegPath, Encoding.UTF8);
        }
        catch (IOException)
        {
            return null;
        }

        var bytes = ParseHexValue(content, section, keyName);
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        return new GameAccountSnapshot
        {
            GameBiz = gameBiz,
            Sha256 = GameAccountSnapshot.ComputeSha256(bytes),
            Value = bytes,
            Uid = ReadUid(content, section, gameBiz),
            Name = "-",
        };
    }


    /// <summary>
    /// 切换账号：把快照写回 <c>user.reg</c>。
    /// </summary>
    /// <returns>成功返回 <c>null</c>；失败返回原因（绝不抛异常给 UI）。</returns>
    public string? Write(GameAccountSnapshot snapshot)
    {
        var section = GetSectionName(snapshot.GameBiz);
        var keyName = GetAccountKeyName(snapshot.GameBiz);
        if (section is null || keyName is null)
        {
            return $"不支持的区服：{snapshot.GameBiz}";
        }

        if (!IsAvailable)
        {
            return $"prefix 里没有 user.reg：{_prefix}";
        }

        if (snapshot.Value.Length == 0)
        {
            return "账号数据为空";
        }

        // 有 wine 客户端在跑时不要改 —— 游戏会在退出时覆盖回去
        if (File.GetLastWriteTimeUtc(UserRegPath) > DateTime.UtcNow.AddSeconds(5))
        {
            return "user.reg 刚刚被写过（可能有 wine 进程在跑），请先退出游戏";
        }

        string content;
        try
        {
            content = File.ReadAllText(UserRegPath, Encoding.UTF8);
        }
        catch (IOException ex)
        {
            return $"读 user.reg 失败：{ex.Message}";
        }

        var updated = SetHexValue(content, section, keyName, snapshot.Value);

        // 顺带把"最后一次登录的 UID"也写上，与上游行为一致
        if (snapshot.Uid > 0)
        {
            updated = snapshot.GameBiz switch
            {
                var b when b.StartsWith("hk4e", StringComparison.Ordinal)
                    => SetHexValue(updated, section, LastUidHk4e, Encoding.UTF8.GetBytes($"{snapshot.Uid}\0")),
                var b when b.StartsWith("hkrpg", StringComparison.Ordinal)
                    => SetDwordValue(updated, section, LastUserIdHkrpg, (int)snapshot.Uid),
                var b when b.StartsWith("bh3", StringComparison.Ordinal)
                    => SetDwordValue(updated, section, LastUserIdBh3, (int)snapshot.Uid),
                _ => updated,
            };
        }

        if (updated == content)
        {
            return "写入后内容没有变化（键名可能不匹配）";
        }

        try
        {
            AtomicFile.WriteAllText(UserRegPath, updated);
        }
        catch (IOException ex)
        {
            return $"写 user.reg 失败：{ex.Message}";
        }

        return null;
    }


    // ── 解析 ───────────────────────────────────────────────────────────

    /// <summary>
    /// 从 <c>user.reg</c> 文本里取某段落某键的 <c>hex:</c> 值（返回原始字节）。
    /// </summary>
    public static byte[]? ParseHexValue(string content, string section, string keyName)
    {
        var (start, end) = FindSectionBounds(content, section);
        if (start < 0)
        {
            return null;
        }

        var block = content[start..end];
        var literal = ParseHexLiteral(block, keyName);
        if (literal is not null)
        {
            return literal;
        }

        // 也兼容 "str"="value" 的字符串形式
        var stringValue = ParseStringValue(block, keyName);
        return stringValue is null ? null : Encoding.UTF8.GetBytes(stringValue);
    }


    private static (int Start, int End) FindSectionBounds(string content, string section)
    {
        // 两种写法都要认：
        //   1) 纯 ASCII 键：[Software\\miHoYo\\Genshin Impact]
        //   2) 非 ASCII 键：[Software\\miHoYo\\\x7edd\x533a\x96f6]  ← Wine 把非 ASCII 写成 \xHHHH
        // 所以不能用"找固定字符串"，必须逐段解码后比较。
        var escaped = section.Replace("\\", "\\\\", StringComparison.Ordinal);
        var asciiHeader = "[" + escaped + "]";

        var asciiIndex = content.IndexOf(asciiHeader, StringComparison.Ordinal);
        if (asciiIndex >= 0)
        {
            var asciiEnd = content.IndexOf("\n[", asciiIndex + asciiHeader.Length, StringComparison.Ordinal);
            return (asciiIndex, asciiEnd < 0 ? content.Length : asciiEnd);
        }

        // 逐个段落解码比对
        var position = 0;
        while (true)
        {
            var start = content.IndexOf("\n[", position, StringComparison.Ordinal);
            if (start < 0)
            {
                if (position == 0 && content.StartsWith('['))
                {
                    start = -1;
                }
                else
                {
                    return (-1, -1);
                }
            }

            var headerStart = start < 0 ? 0 : start + 1;
            var close = content.IndexOf(']', headerStart);
            if (close < 0)
            {
                return (-1, -1);
            }

            var decoded = DecodeRegistryKey(content[(headerStart + 1)..close]);
            if (string.Equals(decoded, section, StringComparison.Ordinal))
            {
                var end = content.IndexOf("\n[", close, StringComparison.Ordinal);
                return (headerStart, end < 0 ? content.Length : end);
            }

            position = close + 1;
        }
    }


    /// <summary>
    /// 解码 Wine <c>user.reg</c> 的键名转义。
    /// </summary>
    /// <remarks>
    /// Wine 的规则：<c>\\</c> → <c>\</c>；非 ASCII 字符写成 <c>\xHHHH</c>（UTF-16 码元）。
    /// 注意 <c>\xHHHH</c> 是**四位**十六进制，而 <c>\xHH</c>（两位）表示原始字节。
    /// </remarks>
    public static string DecodeRegistryKey(string encoded)
    {
        if (string.IsNullOrEmpty(encoded))
        {
            return encoded;
        }

        var builder = new StringBuilder(encoded.Length);
        for (var i = 0; i < encoded.Length; i++)
        {
            if (encoded[i] != '\\' || i + 1 >= encoded.Length)
            {
                builder.Append(encoded[i]);
                continue;
            }

            var next = encoded[i + 1];

            // \xHHHH （四位）
            if (next == 'x' && i + 5 < encoded.Length
                && IsHex(encoded[i + 2]) && IsHex(encoded[i + 3]) && IsHex(encoded[i + 4]) && IsHex(encoded[i + 5]))
            {
                var code = Convert.ToInt32(encoded.Substring(i + 2, 4), 16);
                builder.Append((char)code);
                i += 5;
                continue;
            }

            // \a \b \t \n \v \f \r \" \\ …（C 风格 + 未知字符原样）
            builder.Append(next switch
            {
                'a' => '\a',
                'b' => '\b',
                't' => '\t',
                'n' => '\n',
                'v' => '\v',
                'f' => '\f',
                'r' => '\r',
                '0' => '\0',
                _ => next,
            });
            i++;
        }

        return builder.ToString();
    }


    private static bool IsHex(char c)
        => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';


    private static byte[]? ParseHexLiteral(string block, string keyName)
    {
        var marker = "\"" + keyName + "\"=hex:";
        var index = block.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }

        // 值可能跨行，每行以 \ 续行
        var valueStart = index + marker.Length;
        var builder = new StringBuilder();
        var lines = block[valueStart..].Split('\n');
        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            builder.Append(line);
            if (!line.EndsWith('\\'))
            {
                break;
            }

            builder.Length -= 1; // 去掉续行符
        }

        var hex = builder.ToString().TrimEnd(',', '\\', ' ');
        var result = new List<byte>();
        foreach (var part in hex.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var text = part.Trim();
            if (text.Length == 0)
            {
                continue;
            }

            if (!byte.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var value))
            {
                return null;
            }

            result.Add(value);
        }

        return result.ToArray();
    }


    private static string? ParseStringValue(string block, string keyName)
    {
        var marker = "\"" + keyName + "\"=\"";
        var index = block.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }

        var start = index + marker.Length;
        var builder = new StringBuilder();
        for (var i = start; i < block.Length; i++)
        {
            if (block[i] == '\\' && i + 1 < block.Length)
            {
                builder.Append(block[++i]);
            }
            else if (block[i] == '"')
            {
                break;
            }
            else
            {
                builder.Append(block[i]);
            }
        }

        return builder.ToString();
    }


    private static long ReadUid(string content, string section, string gameBiz)
    {
        var key = gameBiz.StartsWith("hk4e", StringComparison.Ordinal) ? LastUidHk4e
            : gameBiz.StartsWith("hkrpg", StringComparison.Ordinal) ? LastUserIdHkrpg
            : gameBiz.StartsWith("bh3", StringComparison.Ordinal) ? LastUserIdBh3
            : null;
        if (key is null)
        {
            return 0;
        }

        var (start, end) = FindSectionBounds(content, section);
        if (start < 0)
        {
            return 0;
        }

        var block = content[start..end];
        var dword = ParseDwordValue(block, key);
        if (dword > 0)
        {
            return dword;
        }

        // 原神把 UID 存成 UTF-8 字符串的 hex（"100000000\0"）
        var raw = ParseHexLiteral(block, key);
        if (raw is { Length: > 0 })
        {
            var text = Encoding.UTF8.GetString(raw).TrimEnd('\0').Trim();
            if (long.TryParse(text, out var uid))
            {
                return uid;
            }
        }

        return 0;
    }


    private static int ParseDwordValue(string block, string keyName)
    {
        var marker = "\"" + keyName + "\"=dword:";
        var index = block.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            return 0;
        }

        var start = index + marker.Length;
        var end = start;
        while (end < block.Length && Uri.IsHexDigit(block[end]))
        {
            end++;
        }

        return int.TryParse(block[start..end], System.Globalization.NumberStyles.HexNumber, null, out var value)
            ? value
            : 0;
    }


    // ── 写入 ───────────────────────────────────────────────────────────

    /// <summary>写入 <c>hex:</c> 值；键不存在时追加到段落末尾，段落不存在时追加到文件末尾。</summary>
    public static string SetHexValue(string content, string section, string keyName, byte[] value)
    {
        var line = BuildHexLine(keyName, value, indent: "    ");
        return SetValue(content, section, keyName, line, isHex: true, pendingHexValue: value);
    }


    /// <summary>写入 <c>dword:</c> 值。</summary>
    public static string SetDwordValue(string content, string section, string keyName, int value)
    {
        var line = $"\"{keyName}\"=dword:{value:x8}";
        return SetValue(content, section, keyName, line, isHex: false);
    }


    /// <summary>
    /// 在段落内设置一个值。段落不存在则追加新段；键不存在则追加到段落末尾。
    /// </summary>
    /// <remarks>
    /// 实现要点（踩过的坑）：<c>block</c> 的**第一行是段头** <c>[Software\\...]</c>，
    /// 不能把它连同段落一起丢掉——否则写回后会整段消失（段数 114 → 113）。
    /// </remarks>
    private static string SetValue(
        string content, string section, string keyName, string newLine, bool isHex, byte[]? pendingHexValue = null)
    {
        var (start, end) = FindSectionBounds(content, section);
        if (start < 0)
        {
            // 段落不存在：整段追加到文件末尾
            var builder = new StringBuilder(content.TrimEnd('\n'));
            builder.Append('\n');
            builder.Append('[').Append(EncodeRegistryKey(section)).Append(']');
            builder.Append('\n');
            builder.Append(newLine);
            builder.Append('\n');
            return builder.ToString();
        }

        var block = content[start..end];

        // 段头行 = block 的第一行（含 [ ]）
        var headerEnd = block.IndexOf('\n');
        var headerLine = headerEnd < 0 ? block : block[..headerEnd];
        var body = headerEnd < 0 ? "" : block[(headerEnd + 1)..];

        var marker = "\"" + keyName + "\"=" + (isHex ? "hex:" : "dword:");
        var index = body.IndexOf(marker, StringComparison.Ordinal);

        string newBody;
        if (index < 0)
        {
            // 键不存在：追加到段落末尾
            newBody = body.TrimEnd('\n') + (body.Length > 0 ? "\n" : "") + newLine + "\n";
        }
        else
        {
            // 行首缩进
            var lineStart = body.LastIndexOf('\n', Math.Max(0, index - 1));
            var indentStart = lineStart < 0 ? 0 : lineStart + 1;
            var indent = body[indentStart..index];

            // 值可能跨多行（hex 用反斜杠续行）：找到该值真正的结束
            var pos = index + marker.Length;
            while (true)
            {
                var eol = body.IndexOf('\n', pos);
                if (eol < 0)
                {
                    pos = body.Length;
                    break;
                }

                var line = body[pos..eol].TrimEnd('\r');
                pos = eol + 1;
                if (!line.EndsWith('\\'))
                {
                    break;
                }
            }

            // 已有值：优先"原位替换数字"，保留原换行布局（避免几百行无意义 diff）
            string? replacement = null;
            if (isHex)
            {
                var existingText = body[(index + marker.Length)..pos];
                replacement = ReplaceHexDigits(existingText, pendingHexValue!);
                if (replacement is not null)
                {
                    newBody = body[..index] + marker + replacement + body[pos..];
                    return content[..start] + headerLine + "\n" + newBody + content[end..];
                }
            }

            newBody = body[..indentStart] + indent + newLine.TrimStart() + "\n" + body[pos..];
        }

        return content[..start] + headerLine + "\n" + newBody + content[end..];
    }


    /// <summary>
    /// 把注册表键编码成 Wine <c>user.reg</c> 的写法（<c>\</c> 双写；非 ASCII 用 <c>\xHHHH</c>）。
    /// </summary>
    public static string EncodeRegistryKey(string key)
    {
        var builder = new StringBuilder(key.Length * 2);
        foreach (var c in key)
        {
            if (c == '\\')
            {
                builder.Append("\\\\");
            }
            else if (c < 0x20 || c > 0x7E)
            {
                builder.Append("\\x").Append(((int)c).ToString("x4"));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }


    /// <summary>
    /// 生成 Wine 风格的 <c>hex:</c> 行（新建键时用）。
    /// </summary>
    /// <remarks>
    /// Wine 的 <c>dump_hex</c> 按 80 列对齐：第一行放得下就继续放，
    /// 放不下时在<b>行尾</b>写逗号 + 反斜杠续行，续行缩进两个空格后每行再放 25 字节。
    /// 已存在键的改写不走这里（见 <see cref="ReplaceHexDigits"/>：保留原布局）。
    /// </remarks>
    private static string BuildHexLine(string keyName, byte[] value, string indent)
    {
        const int bytesPerLine = 25;

        var builder = new StringBuilder();
        var prefix = indent + "\"" + keyName + "\"=hex:";
        builder.Append(prefix);

        // 第一行：按 80 列尽量放
        var firstLineCapacity = Math.Max(1, (80 - prefix.Length) / 3);
        var index = 0;

        for (var i = 0; i < firstLineCapacity && index < value.Length; i++, index++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(value[index].ToString("x2"));
        }

        while (index < value.Length)
        {
            builder.Append(",\\").Append('\n').Append("  ");
            for (var i = 0; i < bytesPerLine && index < value.Length; i++, index++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append(value[index].ToString("x2"));
            }
        }

        return builder.ToString();
    }


    /// <summary>
    /// 用一个新值替换已有 <c>hex:</c> 值的数字部分，<b>保留原有换行与缩进布局</b>。
    /// </summary>
    /// <remarks>
    /// 为什么不重新格式化：Wine 的换行位置取决于键名长度与 80 列对齐，
    /// 重新生成虽然数据等价，但会把用户的 <c>user.reg</c> 搅出几百行无意义 diff。
    /// 只改数字、按原来的位置填，最稳。
    /// </remarks>
    private static string? ReplaceHexDigits(string existingValueText, byte[] newValue)
    {
        // existingValueText 是 "hex:" 之后、到该值结束为止的原文（含换行）
        var digitPositions = new List<int>();
        for (var i = 0; i < existingValueText.Length; i++)
        {
            if (IsHex(existingValueText[i]))
            {
                digitPositions.Add(i);
            }
        }

        // 原值里的数字个数必须是偶数（每字节两位）
        if (digitPositions.Count % 2 != 0)
        {
            return null;
        }

        var originalBytes = digitPositions.Count / 2;
        var builder = new StringBuilder(existingValueText);

        if (newValue.Length == originalBytes)
        {
            // 等长：逐字节原位替换
            for (var i = 0; i < newValue.Length; i++)
            {
                var hex = newValue[i].ToString("x2");
                builder[digitPositions[i * 2]] = hex[0];
                builder[digitPositions[i * 2 + 1]] = hex[1];
            }

            return builder.ToString();
        }

        // 不等长：只保留布局骨架（换行与缩进），按新值重填
        return null;
    }
}
