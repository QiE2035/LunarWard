using System.Diagnostics;

namespace LunarWard.Frameworks;

/// <summary>
/// 用桌面默认程序打开 URI。
/// </summary>
/// <remarks>
/// <para>
/// 上游 Windows 用 <c>Windows.System.Launcher.LaunchUriAsync</c>（返回 <c>bool</c>，见 Starward
/// <c>Features/Setting/AboutSetting.xaml.cs</c>）；Linux 没有该 API，等价物是 <c>xdg-open</c>——
/// <see cref="ProcessStartInfo.UseShellExecute"/> 在 .NET/Linux 上正是通过 <c>xdg-open</c> 分派。
/// </para>
/// <para>
/// 与上游一致只接受 http/https 绝对地址；没有默认浏览器/xdg-open 时静默失败（旧
/// <c>AppServices.OpenUriAsync</c> 同样吞掉异常），返回是否已启动。
/// </para>
/// </remarks>
public static class ShellHelper
{


    /// <summary>用桌面默认程序打开 <paramref name="uri"/>。</summary>
    /// <returns>已交给系统打开返回 <see langword="true"/>；无默认程序或参数不是 http(s) 时返回 <see langword="false"/>。</returns>
    public static bool OpenUri(string uri)
    {
        try
        {
            if (Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsed)
                && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
            {
                using Process? process = Process.Start(new ProcessStartInfo(parsed.AbsoluteUri)
                {
                    UseShellExecute = true,
                });
                return process is not null;
            }
        }
        catch (Exception)
        {
            // 没有默认浏览器：静默失败（与上游 LaunchUriAsync 返回 false 等价）
        }

        return false;
    }


}
