using System;
using System.IO.Compression;

namespace DevSyncLib.Command.Compression
{
    public class BrotliCompression : ICompression
    {
        public bool TryCompress(ReadOnlySpan<byte> source, Span<byte> destination, out int written)
        {
            return BrotliEncoder.TryCompress(source, destination, out written, 0, 20);
        }

        public bool TryDecompress(ReadOnlySpan<byte> source, Span<byte> destination, out int written)
        {
            return BrotliDecoder.TryDecompress(source, destination, out written);
        }
    }
}
