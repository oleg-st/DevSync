using System;
using System.IO;
using System.IO.Compression;

namespace DevSyncLib.Command.Compression
{
    public class GzipCompression : ICompress
    {
        public bool TryCompress(ReadOnlySpan<byte> source, Span<byte> destination, out int written)
        {
            using (var ms = new MemoryStream())
            {
                using (var gzip = new GZipStream(ms, CompressionLevel.Fastest))
                {
                    gzip.Write(source);
                    gzip.Flush();
                }
                var data = ms.ToArray();
                if (data.Length > source.Length)
                {
                    written = 0;
                    return false;
                }

                written = data.Length;
                // TODO: remove copy?
                new Span<byte>(data).CopyTo(destination);
            }

            return true;
        }

        public bool TryDecompress(ReadOnlySpan<byte> source, Span<byte> destination, out int written)
        {
            using (var ms = new MemoryStream(source.ToArray()))
            {
                using var outputMs = new MemoryStream();
                using var gzip = new GZipStream(ms, CompressionMode.Decompress);
                gzip.CopyTo(outputMs);
                var data = outputMs.ToArray();

                // TODO: remove copy?
                new Span<byte>(data).CopyTo(destination);
                written = data.Length;
            }

            return true;
        }
    }
}
