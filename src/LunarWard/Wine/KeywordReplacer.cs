namespace LunarWard.Wine;

/// <summary>
/// 关键字替换。标准集沿用 an-anime-team，扩展集为本项目新增。
/// </summary>
/// <remarks>见 <c>docs/03-LAUNCH-ENGINE.md</c> §7。</remarks>
public static class KeywordReplacer
{
    /// <summary>
    /// 替换 <c>%keyword%</c>。未知关键字<b>原样保留</b>（便于 UI 提示拼写错误，
    /// 也避免误吞用户的其他 <c>%...%</c> 文本）。
    /// </summary>
    public static string Replace(string input, IReadOnlyDictionary<string, string?> keywords)
    {
        if (string.IsNullOrEmpty(input) || keywords.Count == 0)
        {
            return input;
        }

        var result = input;
        foreach (var (key, value) in keywords)
        {
            if (value is null)
            {
                continue;
            }

            result = result.Replace("%" + key + "%", value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }


    /// <summary>替换字符串列表中的每一项。</summary>
    public static List<string> Replace(IEnumerable<string> inputs, IReadOnlyDictionary<string, string?> keywords)
        => inputs.Select(x => Replace(x, keywords)).ToList();


    /// <summary>标准关键字集合的构造器。</summary>
    public sealed class Builder
    {
        private readonly Dictionary<string, string?> _map = new(StringComparer.OrdinalIgnoreCase);


        public Builder With(string key, string? value)
        {
            _map[key] = value;
            return this;
        }


        /// <summary><c>%build%</c> —— runner 目录。</summary>
        public Builder Build(string? value) => With("build", value);

        /// <summary><c>%prefix%</c> —— wine prefix。</summary>
        public Builder Prefix(string? value) => With("prefix", value);

        /// <summary><c>%game%</c> —— 游戏目录。</summary>
        public Builder Game(string? value) => With("game", value);

        /// <summary><c>%launcher%</c> —— LunarWard 数据目录。</summary>
        public Builder Launcher(string? value) => With("launcher", value);

        /// <summary><c>%temp%</c> —— 临时目录。</summary>
        public Builder Temp(string? value) => With("temp", value);

        /// <summary><c>%exe%</c> —— 游戏可执行文件名。</summary>
        public Builder Exe(string? value) => With("exe", value);

        /// <summary><c>%pid%</c> —— 游戏进程 pid（仅 Running 之后可用）。</summary>
        public Builder Pid(int? value) => With("pid", value?.ToString(System.Globalization.CultureInfo.InvariantCulture));

        /// <summary><c>%channel%</c> —— 当前渠道。</summary>
        public Builder Channel(string? value) => With("channel", value);

        /// <summary><c>%runtime%</c> —— 解析出的运行时名。</summary>
        public Builder Runtime(string? value) => With("runtime", value);


        public IReadOnlyDictionary<string, string?> Build() => _map;
    }
}


/// <summary>
/// 命令模板。扩展了 an-anime-team 的集合，新增 <c>%post%</c>（外部程序拼接位）。
/// </summary>
/// <remarks>
/// 用插槽替代"字符串探测 INJECTOR_NAME"的脆弱做法，见
/// <c>docs/03-LAUNCH-ENGINE.md</c> §7.3。
/// </remarks>
public static class CommandTemplate
{
    public const string COMMAND = "command";
    public const string BASH_COMMAND = "bash_command";
    public const string WINDOWS_COMMAND = "windows_command";
    public const string LAUNCH_ARGS = "launch_args";
    public const string POST = "post";


    /// <summary>渲染完整命令模板。</summary>
    public static string Expand(
        string template,
        string bashCommand,
        string windowsCommand,
        IEnumerable<string> launchArgs,
        string post = "")
    {
        var args = string.Join(' ', launchArgs.Where(a => !string.IsNullOrEmpty(a)));
        var command = string.Join(' ', new[] { bashCommand, windowsCommand, args }.Where(s => !string.IsNullOrEmpty(s)));
        return KeywordReplacer.Replace(template, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [COMMAND] = command,
            [BASH_COMMAND] = bashCommand,
            [WINDOWS_COMMAND] = windowsCommand,
            [LAUNCH_ARGS] = args,
            [POST] = post,
        });
    }


    /// <summary>模板里是否包含 <c>%post%</c> 插槽。</summary>
    public static bool ContainsPost(string? template)
        => !string.IsNullOrEmpty(template) && template.Contains("%post%", StringComparison.OrdinalIgnoreCase);
}


/// <summary>
/// Shell 风格的分词器（用于用户填写的参数行）。
/// </summary>
/// <remarks>
/// 支持单引号、双引号与反斜杠转义；<b>不做变量展开</b>。
/// 不能简单 <c>Split(' ')</c>——路径里带空格是常态。
/// </remarks>
public static class CommandLineSplitter
{
    public static List<string> Split(string? commandLine)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return result;
        }

        var current = new System.Text.StringBuilder();
        var inSingle = false;
        var inDouble = false;
        var hasToken = false;

        for (var i = 0; i < commandLine.Length; i++)
        {
            var c = commandLine[i];
            if (inSingle)
            {
                if (c == '\'')
                {
                    inSingle = false;
                }
                else
                {
                    current.Append(c);
                }

                continue;
            }

            if (inDouble)
            {
                if (c == '"')
                {
                    inDouble = false;
                }
                else if (c == '\\' && i + 1 < commandLine.Length)
                {
                    current.Append(commandLine[++i]);
                }
                else
                {
                    current.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '\'':
                    inSingle = true;
                    hasToken = true;
                    break;
                case '"':
                    inDouble = true;
                    hasToken = true;
                    break;
                case '\\' when i + 1 < commandLine.Length:
                    current.Append(commandLine[++i]);
                    hasToken = true;
                    break;
                case ' ' or '\t':
                    if (hasToken || current.Length > 0)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                        hasToken = false;
                    }

                    break;
                default:
                    current.Append(c);
                    hasToken = true;
                    break;
            }
        }

        if (hasToken || current.Length > 0)
        {
            result.Add(current.ToString());
        }

        return result;
    }
}
