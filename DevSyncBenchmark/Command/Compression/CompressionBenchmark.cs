using System;
using BenchmarkDotNet.Attributes;
using DevSyncLib.Command.Compression;

namespace DevSyncBenchmark.Command.Compression
{
    public class CompressionBenchmark
    {
        private byte[] _data;
        private ICompression _brotliCompression;
        private ICompression _deflateCompression;
        private ICompression _gzipCompression;
        private ICompression _lz4Compression;

        [GlobalSetup]
        public void Setup()
        {
            var random = new Random();
            _data = new byte[65536];
            Array.Fill<byte>(_data, 0xFF);
            random.NextBytes(new Span<byte>(_data, 16384, 16384));

            _brotliCompression = new BrotliCompression();
            _deflateCompression = new DeflateCompression();
            _gzipCompression= new GZipCompression();
            _lz4Compression = new LZ4Compression();
        }

        private void TestCompression(ICompression compression)
        {
            var compressed = new byte[65536];
            var uncompressed = new byte[65536];

            compression.TryCompress(_data, 0, _data.Length, compressed, 0, compressed.Length,
                out var writtenCompressed);
            compression.TryDecompress(compressed, 0, writtenCompressed, uncompressed, 0, uncompressed.Length,
                out _);
        }

        [Benchmark]
        public void Brotli()
        {
            TestCompression(_brotliCompression);
        }

        [Benchmark]
        public void Deflate()
        {
            TestCompression(_deflateCompression);
        }

        [Benchmark]
        public void Gzip()
        {
            TestCompression(_gzipCompression);
        }

        [Benchmark]
        public void LZ4()
        {
            TestCompression(_lz4Compression);
        }
    }
}
