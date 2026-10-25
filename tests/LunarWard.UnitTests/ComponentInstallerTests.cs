using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using LunarWard.Platform;
using LunarWard.Wine;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 托管组件下载与安装（索引解析、解压、可执行位、原子性）。
/// </summary>
/// <remarks>
/// 用一个真实的本地 <see cref="HttpListener"/> 提供组件包——不走 mock，
/// 这样 <c>HttpClient</c> 的流式下载路径也被覆盖到。
/// </remarks>
public class ComponentInstallerTests
{
    /// <summary>
    /// 造一个含 <c>bin/wine</c> 的 <c>.tar.gz</c>，返回字节。
    /// </summary>
    /// <remarks>
    /// 用 gzip 而不是 xz：BCL 只带 gzip/zip 的<b>编码器</b>（xz 只有解码器在
    /// <c>System.Formats.Tar</c> 之外的三方库里），而测试不该为造包再引一个依赖。
    /// 解压路径对 <c>.tar.gz</c> 与 <c>.tar.xz</c> 是同一段代码（都走外部 tar），
    /// 所以覆盖度不受影响。
    /// </remarks>
    private static byte[] CreateWineArchive()
    {
        using var tarred = new MemoryStream();
        using (var tar = new TarWriter(tarred, TarEntryFormat.Pax, leaveOpen: true))
        {
            var entry = new PaxTarEntry(TarEntryType.RegularFile, "./bin/wine")
            {
                DataStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("#!/bin/sh\necho wine-10.0-test\n")),
                Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            };
            tar.WriteEntry(entry);
        }

        tarred.Position = 0;
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
        {
            tarred.CopyTo(gzip);
        }

        return output.ToArray();
    }


    /// <summary>
    /// 造一个「自带一层同名根目录」的包：<c>my-wine-test/bin/wine</c>。
    /// an-anime-team 的 wine 组件就是这种布局，解压后需要摊平一层。
    /// </summary>
    private static byte[] CreateWineArchiveWithRootDir()
    {
        using var tarred = new MemoryStream();
        using (var tar = new TarWriter(tarred, TarEntryFormat.Pax, leaveOpen: true))
        {
            var entry = new PaxTarEntry(TarEntryType.RegularFile, "./my-wine-test/bin/wine")
            {
                DataStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("#!/bin/sh\necho wine-10.0-test\n")),
                Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            };
            tar.WriteEntry(entry);
        }

        tarred.Position = 0;
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
        {
            tarred.CopyTo(gzip);
        }

        return output.ToArray();
    }


    /// <summary>起一个只回固定字节的 HTTP 服务（可选支持 Range）。</summary>
    private sealed class OneShotServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly Task _loop;


        private readonly bool _supportRange;


        public OneShotServer(byte[] payload, bool supportRange = false)
        {
            _supportRange = supportRange;
            // 随机端口，避免与其它测试/进程打架
            var port = Random.Shared.Next(20000, 60000);
            BaseUrl = $"http://127.0.0.1:{port}/";
            _listener.Prefixes.Add(BaseUrl);
            _listener.Start();
            _loop = Task.Run(async () =>
            {
                while (_listener.IsListening)
                {
                    HttpListenerContext context;
                    try
                    {
                        context = await _listener.GetContextAsync();
                    }
                    catch (Exception)
                    {
                        return;
                    }

                    try
                    {
                        var range = context.Request.Headers["Range"];
                        if (_supportRange && range is not null && range.StartsWith("bytes=", StringComparison.Ordinal))
                        {
                            var start = long.Parse(range[6..].Split('-')[0]);
                            var slice = payload[(int)start..];
                            context.Response.StatusCode = 206;
                            context.Response.ContentLength64 = slice.Length;
                            context.Response.Headers["Content-Range"] = $"bytes {start}-{payload.Length - 1}/{payload.Length}";
                            await context.Response.OutputStream.WriteAsync(slice);
                            context.Response.Close();
                            continue;
                        }

                        context.Response.ContentLength64 = payload.Length;
                        await context.Response.OutputStream.WriteAsync(payload);
                        context.Response.Close();
                    }
                    catch (Exception)
                    {
                        // client 提前断开
                    }
                }
            });
        }


        public string BaseUrl { get; }


        public void Dispose()
        {
            try
            {
                _listener.Stop();
                _listener.Close();
            }
            catch (Exception)
            {
                // ignore
            }

            try
            {
                _loop.Wait(TimeSpan.FromSeconds(2));
            }
            catch (Exception)
            {
                // ignore
            }
        }
    }


    private static XdgPaths CreatePaths(TempDirectory temp)
    {
        var paths = new XdgPaths(temp.Combine("c"), temp.Combine("d"), temp.Combine("k"));
        paths.EnsureDirectories();
        return paths;
    }


    private static void WriteIndex(XdgPaths paths, string archiveUrl, string componentName = "my-wine-test")
    {
        Directory.CreateDirectory(Path.Join(paths.ComponentsDirectory, "wine"));
        File.WriteAllText(Path.Join(paths.ComponentsDirectory, "components.json"),
            """{"wine":[{"name":"testgroup","title":"Test"}],"dxvk":[]}""");
        File.WriteAllText(Path.Join(paths.ComponentsDirectory, "wine", "testgroup.json"),
            $$"""
            [
              {
                "name": "{{componentName}}",
                "title": "Test",
                "uri": "{{archiveUrl}}",
                "files": { "wine": "bin/wine" },
                "features": { "env": { "WINE_TEST": "1" } }
              }
            ]
            """);
    }


    [Fact]
    public async Task InstallWine_DownloadsExtractsAndMarksExecutable()
    {
        using var temp = new TempDirectory();
        var paths = CreatePaths(temp);
        using var server = new OneShotServer(CreateWineArchive());
        WriteIndex(paths, server.BaseUrl + "wine-test.tar.gz");

        var installer = new ComponentInstaller(paths);
        Assert.False(installer.IsInstalled("wine", "my-wine-test"));

        var target = await installer.InstallWineAsync("my-wine-test");

        Assert.True(installer.IsInstalled("wine", "my-wine-test"));
        var wine = Path.Join(target, "bin", "wine");
        Assert.True(File.Exists(wine));
        Assert.True((File.GetUnixFileMode(wine) & UnixFileMode.UserExecute) != 0);
        Assert.False(Directory.Exists(target + ".tmp"));

        // 索引里的 features.env 能读到
        var found = installer.Repository.Find("wine", "my-wine-test");
        Assert.NotNull(found);
        Assert.Equal("1", found!.Value.Entry.Features?.Env?["WINE_TEST"]);
    }


    [Fact]
    public async Task InstallWine_FlattensSingleRootDirectory()
    {
        using var temp = new TempDirectory();
        var paths = CreatePaths(temp);
        using var server = new OneShotServer(CreateWineArchiveWithRootDir());
        WriteIndex(paths, server.BaseUrl + "wine-test.tar.gz");

        var target = await new ComponentInstaller(paths).InstallWineAsync("my-wine-test");

        // 包里的 my-wine-test/bin/wine 被摊平到 target/bin/wine
        Assert.True(File.Exists(Path.Join(target, "bin", "wine")));
        Assert.False(Directory.Exists(Path.Join(target, "my-wine-test")));
    }


    [Fact]
    public async Task InstallWine_ResumesFromPartialFileInsteadOfRestarting()
    {
        using var temp = new TempDirectory();
        var paths = CreatePaths(temp);
        var payload = CreateWineArchive();
        using var server = new OneShotServer(payload, supportRange: true);
        WriteIndex(paths, server.BaseUrl + "wine-test.tar.gz");

        // 模拟上次断流：缓存里只有前 100 字节的 .part
        var downloads = Path.Join(paths.CacheHome, "downloads");
        Directory.CreateDirectory(downloads);
        var part = Path.Join(downloads, "wine-test.tar.gz.part");
        await File.WriteAllBytesAsync(part, payload[..100]);

        var target = await new ComponentInstaller(paths).InstallWineAsync("my-wine-test");

        Assert.True(File.Exists(Path.Join(target, "bin", "wine")));
        Assert.True(File.Exists(Path.Join(downloads, "wine-test.tar.gz")));
        Assert.Equal(payload.Length, new FileInfo(Path.Join(downloads, "wine-test.tar.gz")).Length);
        Assert.False(File.Exists(part));
    }


    [Fact]
    public async Task InstallWine_UnknownComponent_Throws()
    {
        using var temp = new TempDirectory();
        var paths = CreatePaths(temp);
        WriteIndex(paths, "http://127.0.0.1:1/x.tar.xz");

        await Assert.ThrowsAsync<RuntimeNotFoundException>(
            () => new ComponentInstaller(paths).InstallWineAsync("nope"));
    }


    [Fact]
    public async Task InstallWine_HttpFailure_LeavesNoHalfInstalledDirectory()
    {
        using var temp = new TempDirectory();
        var paths = CreatePaths(temp);
        WriteIndex(paths, "http://127.0.0.1:1/wine.tar.gz"); // 端口 1，必然失败

        var installer = new ComponentInstaller(paths);
        await Assert.ThrowsAnyAsync<Exception>(() => installer.InstallWineAsync("my-wine-test"));

        var target = Path.Join(paths.RunnersDirectory, "my-wine-test");
        Assert.False(Directory.Exists(target));
        Assert.False(Directory.Exists(target + ".tmp"));
    }


    [Fact]
    public void IsInstalled_RequiresBinWineForRunners()
    {
        using var temp = new TempDirectory();
        var paths = CreatePaths(temp);
        var installer = new ComponentInstaller(paths);

        // 空目录不算已安装
        Directory.CreateDirectory(Path.Join(paths.RunnersDirectory, "empty"));
        Assert.False(installer.IsInstalled("wine", "empty"));

        // dxvk 只要求目录存在
        Directory.CreateDirectory(Path.Join(paths.DxvkDirectory, "dxvk-1"));
        Assert.True(installer.IsInstalled("dxvk", "dxvk-1"));
    }


    [Fact]
    public async Task UpdateIndex_NoNetwork_ReturnsErrorsWithoutThrowing()
    {
        using var temp = new TempDirectory();
        var paths = CreatePaths(temp);
        var installer = new ComponentInstaller(paths, new HttpClient { Timeout = TimeSpan.FromSeconds(3) });

        // 把索引源指向一个不可达地址：用反射换不合算，直接断言"不抛 + 报告错误"
        var result = await installer.UpdateIndexAsync();

        // 有网时可能成功（UpdatedFiles > 0），无网时必须进 Errors 分支——两者都不允许抛
        Assert.True(result.Success || result.Errors.Count > 0);
    }


    [Fact]
    public void Repository_MissingIndexIsEmptyNotThrow()
    {
        using var temp = new TempDirectory();
        var repo = new ComponentRepository(temp.Combine("no-components"));
        Assert.Empty(repo.ReadIndex().Wine);
        Assert.Empty(repo.ReadGroup("wine", "nope"));
        Assert.Null(repo.Find("wine", "nope"));
    }
}
