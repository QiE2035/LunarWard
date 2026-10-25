using System.Runtime.InteropServices;

namespace LunarWard.Platform;

/// <summary>
/// 进程级文件锁（<c>flock(2)</c>）。用于保证同一时刻只有一个启动器实例
/// 在操作某个 prefix。
/// </summary>
public interface IFileLock : IDisposable
{
    bool IsHeld { get; }
}


/// <summary>基于 <c>flock(2)</c> 的独占锁。</summary>
public sealed class FlockFileLock : IFileLock
{
    private readonly FileStream _stream;

    private bool _held;


    private FlockFileLock(FileStream stream)
    {
        _stream = stream;
        _held = true;
    }


    /// <summary>
    /// 尝试立即获取独占锁；失败返回 <c>null</c>（调用方应提示"另一个实例正在运行"）。
    /// </summary>
    public static FlockFileLock? TryAcquire(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        var fd = (int)stream.SafeFileHandle.DangerousGetHandle();
        if (Unix.Libc.Flock(fd, Unix.Libc.LOCK_EX | Unix.Libc.LOCK_NB) != 0)
        {
            stream.Dispose();
            return null;
        }

        return new FlockFileLock(stream);
    }


    public bool IsHeld => _held;


    public void Dispose()
    {
        if (!_held)
        {
            return;
        }

        _held = false;
        try
        {
            var fd = (int)_stream.SafeFileHandle.DangerousGetHandle();
            Unix.Libc.Flock(fd, Unix.Libc.LOCK_UN);
        }
        catch (Exception)
        {
            // ignore
        }

        _stream.Dispose();
    }
}


/// <summary>
/// 原子写工具：写 <c>.tmp</c> → <c>fsync</c> → <c>rename</c>。
/// 配置、<c>config.ini</c>、启动报告全部走这里，避免半写文件。
/// </summary>
public static class AtomicFile
{
    /// <summary>原子写入文本。</summary>
    /// <remarks>
    /// 配置文件里有 cookie / authkey，<c>.tmp</c> 也必须是 0600（审计 M-01：曾用默认
    /// 0644 创建 tmp，崩溃后留下同机可读的明文）；失败路径删掉 tmp，不留残留。
    /// </remarks>
    public static void WriteAllText(string path, string content)
    {
        var full = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(full)!;
        Directory.CreateDirectory(dir);
        var tmp = full + ".tmp";
        try
        {
            using (var fs = CreateOwnerOnly(tmp))
            {
                using var writer = new StreamWriter(fs, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                writer.Write(content);
                writer.Flush();
                fs.Flush(flushToDisk: true);
            }

            File.Move(tmp, full, overwrite: true);
        }
        catch
        {
            TryDelete(tmp);
            throw;
        }
    }


    /// <summary>以 0600 创建/截断临时文件（非 Unix 平台退回默认行为）。</summary>
    private static FileStream CreateOwnerOnly(string path)
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.None,
            BufferSize = 64 * 1024,
            Options = FileOptions.WriteThrough,
        };
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        return new FileStream(path, options);
    }


    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }


    /// <summary>原子写入字节。</summary>
    public static void WriteAllBytes(string path, ReadOnlySpan<byte> content)
    {
        var full = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(full)!;
        Directory.CreateDirectory(dir);
        var tmp = full + ".tmp";
        try
        {
            using (var fs = CreateOwnerOnly(tmp))
            {
                fs.Write(content);
                fs.Flush(flushToDisk: true);
            }
        }
        catch
        {
            TryDelete(tmp);
            throw;
        }

        File.Move(tmp, full, overwrite: true);
    }
}
