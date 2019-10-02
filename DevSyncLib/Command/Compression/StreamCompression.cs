using System;
using System.IO;
using System.IO.Compression;

namespace DevSyncLib.Command.Compression
{
    public abstract class StreamCompression<T> : ICompression where T: Stream
    {
        protected abstract T CreateCompressStream(Stream stream);

        protected abstract T CreateDecompressStream(Stream stream);

        public bool TryCompress(byte[] source, int sourceOffset, int sourceLength, byte[] destination, int destinationOffset, int destinationLength, out int written)
        {
            using var outputStream = new LimitedBufferStream(destination, destinationOffset, destinationLength);
            using var gzip = CreateCompressStream(outputStream);
            gzip.Write(source, sourceOffset, sourceLength);
            gzip.Flush();

            if (outputStream.IsLimitReached || outputStream.Position > source.Length)
            {
                written = 0;
                return false;
            }

            written = (int)outputStream.Position;

            return true;
        }

        public bool TryDecompress(byte[] source, int sourceOffset, int sourceLength, byte[] destination, int destinationOffset, int destinationLength, out int written)
        {
            using (var inputStream = new LimitedBufferStream(source, sourceOffset, sourceLength))
            {
                using var outputMs = new LimitedBufferStream(destination, destinationOffset, destinationLength);
                using var gzip = CreateDecompressStream(inputStream);
                gzip.CopyTo(outputMs);
                if (outputMs.IsLimitReached)
                {
                    written = 0;
                    return false;
                }

                written = (int)outputMs.Position;
            }

            return true;
        }
    }
}
