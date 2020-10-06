using System;
using System.IO.Compression;

namespace DevSyncLib.Command.Compression
{
    public class BrotliCompression : ICompression
    {
        private readonly int _quality;
        private readonly int _window;

        // fastest with window 1MB same as chunk size
        public BrotliCompression(int quality = 0, int window = 20)
        {
            _quality = quality;
            _window = window;
        }

        public bool TryCompress(byte[] source, int sourceOffset, int sourceLength, byte[] destination, int destinationOffset, int destinationLength, out int written)
        {
            return BrotliEncoder.TryCompress(new ReadOnlySpan<byte>(source, sourceOffset, sourceLength),
                new Span<byte>(destination, destinationOffset, destinationLength), out written, _quality, _window);
        }

        public bool TryDecompress(byte[] source, int sourceOffset, int sourceLength, byte[] destination, int destinationOffset, int destinationLength, out int written)
        {
            return BrotliDecoder.TryDecompress(new ReadOnlySpan<byte>(source, sourceOffset, sourceLength),
                new Span<byte>(destination, destinationOffset, destinationLength), out written);
        }
    }
}
