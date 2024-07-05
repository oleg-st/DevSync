using DevSyncLib.Command.Compression;
using System;
using Xunit;

namespace DevSyncTest.Command.Compression;

public class CompressionTest
{
    private void TestCompression(ICompression compression)
    {
        var source = new byte[65536];
        var compressed = new byte[65536];
        var uncompressed = new byte[65536];

        var random = new Random();
        Array.Fill<byte>(source, 0xFF);
        random.NextBytes(new Span<byte>(source, 16384, 16384));

        Assert.True(compression.TryCompress(source, 0, source.Length, compressed, 0, compressed.Length, out var writtenCompressed));
        Assert.True(compression.TryDecompress(compressed, 0, writtenCompressed, uncompressed, 0, uncompressed.Length, out var writtenUncompressed));
        Assert.Equal(writtenUncompressed, source.Length);
        Assert.Equal(source, uncompressed);
    }

    [Fact]
    public void TestBrotli()
    {
        TestCompression(new BrotliCompression());
    }

    [Fact]
    public void TestDeflate()
    {
        TestCompression(new DeflateCompression());
    }

    [Fact]
    public void TestGzip()
    {
        TestCompression(new GZipCompression());
    }
}