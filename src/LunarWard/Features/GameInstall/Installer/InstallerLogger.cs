namespace LunarWard.Installer;

public enum InstallerLogLevel
{
    Debug = 0,
    Information = 1,
    Warning = 2,
    Error = 3,
}

/// <summary>
/// 安装器内部的最小日志抽象。
///
/// <para>上游用 <c>Microsoft.Extensions.Logging.ILogger</c>，但本项目不允许往
/// <c>Directory.Packages.props</c> 里加包，<c>LunarWard.Core</c> 也不带日志。
/// 因此这里自带一个极小的门面：消息模板用 <c>{Name}</c> 占位，参数按顺序替换，
/// 与上游的调用点形状保持一致（<c>LogInformation("... {a} {b}", a, b)</c>）。</para>
/// </summary>
public interface IInstallerLogger
{
    bool IsEnabled(InstallerLogLevel level);

    void Log(InstallerLogLevel level, Exception? exception, string messageTemplate, params object?[] args);
}


public static class InstallerLoggerExtensions
{
    public static void LogDebug(this IInstallerLogger logger, string messageTemplate, params object?[] args)
        => logger.Log(InstallerLogLevel.Debug, null, messageTemplate, args);

    public static void LogInformation(this IInstallerLogger logger, string messageTemplate, params object?[] args)
        => logger.Log(InstallerLogLevel.Information, null, messageTemplate, args);

    public static void LogWarning(this IInstallerLogger logger, string messageTemplate, params object?[] args)
        => logger.Log(InstallerLogLevel.Warning, null, messageTemplate, args);

    public static void LogWarning(this IInstallerLogger logger, Exception? exception, string messageTemplate, params object?[] args)
        => logger.Log(InstallerLogLevel.Warning, exception, messageTemplate, args);

    public static void LogError(this IInstallerLogger logger, string messageTemplate, params object?[] args)
        => logger.Log(InstallerLogLevel.Error, null, messageTemplate, args);

    public static void LogError(this IInstallerLogger logger, Exception? exception, string messageTemplate, params object?[] args)
        => logger.Log(InstallerLogLevel.Error, exception, messageTemplate, args);
}


/// <summary>丢弃所有日志的默认实现。</summary>
public sealed class NullInstallerLogger : IInstallerLogger
{
    public static NullInstallerLogger Instance { get; } = new();

    public bool IsEnabled(InstallerLogLevel level) => false;

    public void Log(InstallerLogLevel level, Exception? exception, string messageTemplate, params object?[] args)
    {
    }
}


/// <summary>
/// 输出到 <see cref="Console"/> 的实现，供没有接到 Serilog 的调用方直接使用。
/// </summary>
public sealed class ConsoleInstallerLogger : IInstallerLogger
{
    private readonly InstallerLogLevel _minimumLevel;
    private readonly object _sync = new();

    public ConsoleInstallerLogger(InstallerLogLevel minimumLevel = InstallerLogLevel.Information)
    {
        _minimumLevel = minimumLevel;
    }

    public bool IsEnabled(InstallerLogLevel level) => level >= _minimumLevel;

    public void Log(InstallerLogLevel level, Exception? exception, string messageTemplate, params object?[] args)
    {
        if (!IsEnabled(level))
        {
            return;
        }

        var message = InstallerLogFormatter.Format(messageTemplate, args);
        var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
        if (exception is not null)
        {
            line += Environment.NewLine + exception;
        }

        lock (_sync)
        {
            Console.Error.WriteLine(line);
        }
    }
}


/// <summary>把 Serilog 之类的后端以委托形式接进来的适配器。</summary>
public sealed class DelegateInstallerLogger : IInstallerLogger
{
    private readonly Action<InstallerLogLevel, Exception?, string> _sink;

    public DelegateInstallerLogger(Action<InstallerLogLevel, Exception?, string> sink)
    {
        _sink = sink;
    }

    public bool IsEnabled(InstallerLogLevel level) => true;

    public void Log(InstallerLogLevel level, Exception? exception, string messageTemplate, params object?[] args)
    {
        _sink(level, exception, InstallerLogFormatter.Format(messageTemplate, args));
    }
}


/// <summary>
/// 实现结构化日志的 <c>{Name}</c> 模板填充。
/// 支持 <c>{{</c> / <c>}}</c> 转义与 <c>{@Name}</c> 解构前缀，不实现对齐/格式说明符。
/// </summary>
public static class InstallerLogFormatter
{
    public static string Format(string template, object?[]? args)
    {
        if (string.IsNullOrEmpty(template) || args is null || args.Length == 0)
        {
            return template ?? "";
        }

        var builder = new System.Text.StringBuilder(template.Length + 32);
        var position = 0;
        for (var i = 0; i < template.Length; i++)
        {
            var c = template[i];
            if (c == '{')
            {
                if (i + 1 < template.Length && template[i + 1] == '{')
                {
                    builder.Append('{');
                    i++;
                    continue;
                }

                var end = template.IndexOf('}', i + 1);
                if (end < 0)
                {
                    builder.Append(template, i, template.Length - i);
                    break;
                }

                var name = template.Substring(i + 1, end - i - 1);
                if (name.StartsWith('@'))
                {
                    name = name[1..];
                }

                builder.Append(position < args.Length ? args[position]?.ToString() ?? "(null)" : "{" + name + "}");
                position++;
                i = end;
            }
            else if (c == '}' && i + 1 < template.Length && template[i + 1] == '}')
            {
                builder.Append('}');
                i++;
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
