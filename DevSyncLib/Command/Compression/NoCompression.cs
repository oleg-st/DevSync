using System;
using System.IO.Compression;

namespace DevSyncLib.Command.Compression
{
    public class NoCompression : ICompression
    {
        public NoCompression()
        {
        }

        public bool TryCompress(byte[] source, int sourceOffset, int sourceLength, byte[] destination, int destinationOffset, int destinationLength, out int written)
        {
            written = 0;
            return false;
        }

        public bool TryDecompress(byte[] source, int sourceOffset, int sourceLength, byte[] destination, int destinationOffset, int destinationLength, out int written)
        {
            written = 0;
            return false;
        }
    }
}
