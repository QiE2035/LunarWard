using System.Diagnostics;
using Timer = System.Timers.Timer;
using ElapsedEventArgs = System.Timers.ElapsedEventArgs;

namespace LunarWard.Features.Update;

/// <summary>
/// 运行日志上报。对应上游 <c>Starward/Features/Update/LogUploadService.cs</c>。
/// </summary>
/// <remarks>
/// 平台差异（<c>docs/09-LINUX-DIVERGENCES.md</c>）：上游上报到 Starward 的日志服务（app_name 为
/// Starward/Starward.Debug），本仓库保留同一服务地址与请求结构，app_name 改为 LunarWard/LunarWard.Debug。
/// </remarks>
internal static class LogUploadService
{

    private static LogUploadClient _client;

    private static Timer _timer;

    private static DateTimeOffset _startTime;

    private static bool _started;


    public static void Start()
    {
        try
        {
            _timer = new Timer(TimeSpan.FromMinutes(5));
            _timer.Elapsed += UploadLog;
            _timer.Start();
            _startTime = DateTimeOffset.Now;
            UploadLog(null, null!);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }



    private static async void UploadLog(object? sender, ElapsedEventArgs e)
    {
        try
        {
            if (_client is null)
            {
                _client = AppConfig.GetService<LogUploadClient>();
#if DEBUG
                _client.AppName = "LunarWard.Debug";
#else
                _client.AppName = "LunarWard";
#endif
                _client.AppVersion = AppConfig.AppVersion;
                _client.DeviceId = AppConfig.DeviceId.ToString();
                _client.SessionId = AppConfig.SessionId.ToString();
            }
            if (!_started)
            {
                await _client.UploadLogAsync(new LogUploadRequestBase("Runtime", "Start") { Time = _startTime });
                _started = true;
            }
            await _client.UploadLogAsync(new LogUploadRequestBase("Runtime", "Running"));
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }


}
