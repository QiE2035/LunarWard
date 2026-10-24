using LunarWard.Installer;

namespace LunarWard.App.Services;

/// <summary>
/// 把安装器的 <see cref="IInstallerLogger"/> 接到应用的 <see cref="LogService"/>。
/// 上游安装器跑在独立进程里写自己的日志文件；内嵌后统一进应用日志，便于用户在诊断里看到下载/校验失败。
/// </summary>
public sealed class InstallerLogAdapter(LogService log) : IInstallerLogger
{
    public bool IsEnabled(InstallerLogLevel level) => level >= InstallerLogLevel.Information;


    public void Log(InstallerLogLevel level, Exception? exception, string messageTemplate, params object?[] args)
    {
        var message = Format(messageTemplate, args);
        switch (level)
        {
            case InstallerLogLevel.Error:
                log.Error(message, exception);
                break;
            case InstallerLogLevel.Warning:
                log.Warn(message);
                break;
            default:
                log.Info(message);
                break;
        }
    }


    /// <summary>把 <c>{Name}</c> 占位模板换成实参，保持安装器日志的可读性。</summary>
    internal static string Format(string template, object?[] args)
    {
        if (args.Length == 0)
        {
            return template;
        }

        var builder = new System.Text.StringBuilder(template.Length + 32);
        var index = 0;
        for (var i = 0; i < template.Length; i++)
        {
            if (template[i] == '{' && i + 1 < template.Length && template[i + 1] != '{' && index < args.Length)
            {
                var end = template.IndexOf('}', i + 1);
                if (end > i)
                {
                    builder.Append(args[index++]?.ToString() ?? "");
                    i = end;
                    continue;
                }
            }

            builder.Append(template[i]);
        }

        return builder.ToString();
    }
}
