using LunarWard.Setup.Core;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LunarWard.Features.Update;

/// <summary>
/// 安装包下载与更新启动。对应上游 <c>Starward/Features/Update/SetupService.cs</c>。
/// </summary>
/// <remarks>
/// 平台差异（<c>docs/09-LINUX-DIVERGENCES.md</c>）：上游用 <c>Process.Start</c> +
/// <c>UseShellExecute=true</c> + <c>Verb="runas"</c>（UAC 提权）拉起安装器，并传
/// <c>update --InstallFolder … --OldVersion … --pid …</c> 参数。Linux 没有 UAC/runas，
/// 这里保持相同的参数与可执行文件（LoginShell 直接执行），权限交给用户与包管理器。
/// </remarks>
internal class SetupService
{

    private readonly ILogger<SetupService> _logger;

    private readonly HttpClient _httpClient;

    private readonly ReleaseClient _releaseClient;


    public SetupService(ILogger<SetupService> logger, HttpClient httpClient, ReleaseClient releaseClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        _releaseClient = releaseClient;
    }


    public long SetupTotalBytes { get; private set; }

    public long SetupDownloadBytes { get; private set; }



    private async Task<ReleaseInfoDetail> GetReleaseInfoDetailAsync(CancellationToken cancellationToken = default)
    {
        return await _releaseClient.GetLatestReleaseInfoDetailAsync(AppConfig.EnablePreviewRelease, AppConfig.AppVersion, RuntimeInformation.ProcessArchitecture, (InstallType)(AppConfig.IsPortable ? 1 : 0), cancellationToken);
    }



    public async Task<string?> DownloadSetupAsync(ReleaseInfoDetail? detail, CancellationToken cancellationToken = default)
    {
        detail ??= await GetReleaseInfoDetailAsync(cancellationToken);

        if (detail?.Setup is null)
        {
            return null;
        }

        string setupPath = Path.Combine(AppConfig.CacheFolder, detail.Setup.FileName);
        string url = detail.Setup.Url;
        long size = detail.Setup.Size;
        string hash = detail.Setup.Hash;

        if (File.Exists(setupPath))
        {
            if (await FileHashHelper.CheckSHA256Async(setupPath, size, hash, cancellationToken))
            {
                return setupPath;
            }
            File.Delete(setupPath);
        }

        SetupTotalBytes = detail.Setup.Size;
        await DownloadFileAsync(setupPath, url, size, hash, cancellationToken);
        return setupPath;
    }


    private async Task DownloadFileAsync(string path, string url, long size, string hash, CancellationToken cancellationToken = default)
    {
        using var fs = File.Open(path, FileMode.OpenOrCreate);
        await DownloadFileAsync(fs, url, size, hash, cancellationToken);
    }


    private async Task DownloadFileAsync(Stream stream, string url, long size, string hash, CancellationToken cancellationToken = default)
    {
        bool success = false;
        for (int i = 0; i < 3; i++)
        {
            SetupDownloadBytes = stream.Length;
            if (stream.Length < SetupTotalBytes)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(stream.Length, null);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentRange?.From is not null)
                {
                    stream.Position = response.Content.Headers.ContentRange.From.Value;
                    SetupDownloadBytes = stream.Position;
                }
                using var hs = await response.Content.ReadAsStreamAsync(cancellationToken);
                int read = 0;
                Memory<byte> buffer = new byte[8192];
                while ((read = await hs.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await stream.WriteAsync(buffer[..read], cancellationToken);
                    SetupDownloadBytes += read;
                }
                SetupDownloadBytes = stream.Length;
            }
            stream.Position = 0;
            if (await FileHashHelper.CheckSHA256Async(stream, hash, cancellationToken))
            {
                success = true;
                break;
            }
            stream.SetLength(0);
        }
        if (!success)
        {
            throw new Exception("Setup file checksum mismatched.");
        }
    }



    public async Task UpdateAsync(ReleaseInfoDetail detail, CancellationToken cancellationToken = default)
    {
        string? setupPath = await DownloadSetupAsync(detail, cancellationToken);
        if (!File.Exists(setupPath))
        {
            throw new NotSupportedException("Update is not supported.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        Process.Start(new ProcessStartInfo
        {
            FileName = setupPath,
            UseShellExecute = true,
            Arguments = $"""
                update --InstallFolder "{AppContext.BaseDirectory.TrimEnd('/')}" --OldVersion "{AppConfig.AppVersion}" --NewVersion "{detail.Version}" --Preview "{AppConfig.EnablePreviewRelease}" --pid {Environment.ProcessId}
                """,
        });
    }

}
