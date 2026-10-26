using System.Security.Cryptography;
using LunarWard.RPC.GameInstall;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// Sophon 断点续传用的文件切片流（GameInstall/FileSliceStream.cs）：
/// 从同一底层文件切出 [start, start+length) 的只读窗口，多段切片拼接后必须与源文件逐字节一致。
/// </summary>
public class FileSliceStreamTests
{

    private static string CreateTemporaryFilePath()
    {
        string folder = Path.Combine(Path.GetTempPath(), "lunarward-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "payload.bin");
    }


    private static void DeleteTemporaryFolder(string path)
    {
        string? folder = Path.GetDirectoryName(path);
        if (folder is not null)
        {
            Directory.Delete(folder, true);
        }
    }


    [Fact]
    public void Length_And_Position_ReportSliceWindow()
    {
        string path = CreateTemporaryFilePath();
        try
        {
            byte[] data = RandomNumberGenerator.GetBytes(1024 * 64 + 13);
            File.WriteAllBytes(path, data);

            using FileSliceStream slice = new(path, 100, 1000);

            Assert.Equal(1000, slice.Length);
            Assert.Equal(0, slice.Position);
            Assert.False(slice.CanWrite);

            slice.Position = 500;
            Assert.Equal(500, slice.Position);
        }
        finally
        {
            DeleteTemporaryFolder(path);
        }
    }


    [Fact]
    public void Read_ReturnsOnlySliceBytes()
    {
        string path = CreateTemporaryFilePath();
        try
        {
            byte[] data = RandomNumberGenerator.GetBytes(4096 + 37);
            File.WriteAllBytes(path, data);

            using FileSliceStream slice = new(path, 123, 2000);
            byte[] buffer = new byte[3000];
            int read = slice.Read(buffer, 0, buffer.Length);

            Assert.Equal(2000, read);
            Assert.Equal(data.AsSpan(123, 2000).ToArray(), buffer.AsSpan(0, read).ToArray());
            Assert.Equal(0, slice.Read(buffer, 0, buffer.Length));
        }
        finally
        {
            DeleteTemporaryFolder(path);
        }
    }


    [Fact]
    public void SequentialSlices_ReassembleWholeFile()
    {
        string path = CreateTemporaryFilePath();
        try
        {
            byte[] data = RandomNumberGenerator.GetBytes(1024 * 300 + 7);
            File.WriteAllBytes(path, data);
            byte[] expected = SHA256.HashData(data);

            using MemoryStream output = new();
            const int chunkSize = 1024 * 64;
            for (long start = 0; start < data.Length; start += chunkSize)
            {
                long length = Math.Min(chunkSize, data.Length - start);
                using FileSliceStream slice = new(path, start, length);
                Assert.Equal(length, slice.Length);
                slice.CopyTo(output);
            }

            Assert.Equal(data.Length, output.Length);
            Assert.Equal(expected, SHA256.HashData(output.ToArray()));
        }
        finally
        {
            DeleteTemporaryFolder(path);
        }
    }


    [Fact]
    public void Read_AtSliceEnd_ReturnsZero()
    {
        string path = CreateTemporaryFilePath();
        try
        {
            byte[] data = RandomNumberGenerator.GetBytes(64);
            File.WriteAllBytes(path, data);

            using FileSliceStream slice = new(path, 32, 16);
            slice.Seek(0, SeekOrigin.End);

            Assert.Equal(0, slice.Read(new byte[16], 0, 16));
        }
        finally
        {
            DeleteTemporaryFolder(path);
        }
    }

}
