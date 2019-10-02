using System;
using System.IO.Compression;

namespace DevSyncLib.Command.Compression
{
    public class BrotliCompression : ICompression
    {
        public bool TryCompress(byte[] source, int sourceOffset, int sourceLength, byte[] destination, int destinationOffset, int destinationLength, out int written)
        {
            return BrotliEncoder.TryCompress(new ReadOnlySpan<byte>(source, sourceOffset, sourceLength), 
                new Span<byte>(destination, destinationOffset, destinationLength), out written, 0, 20);
        }

        public bool TryDecompress(byte[] source, int sourceOffset, int sourceLength, byte[] destination, int destinationOffset, int destinationLength, out int written)
        {
            return BrotliDecoder.TryDecompress(new ReadOnlySpan<byte>(source, sourceOffset, sourceLength),
                new Span<byte>(destination, destinationOffset, destinationLength), out written);
        }
    }
}
