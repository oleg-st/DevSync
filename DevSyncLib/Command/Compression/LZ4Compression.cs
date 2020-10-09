using K4os.Compression.LZ4;

namespace DevSyncLib.Command.Compression
{
    public class LZ4Compression : ICompression
    {
        public bool TryCompress(byte[] source, int sourceOffset, int sourceLength, byte[] destination, int destinationOffset,
            int destinationLength, out int written)
        {
            written = LZ4Codec.Encode(source, sourceOffset, sourceLength, destination, destinationOffset, destinationLength);
            return written > 0;
        }

        public bool TryDecompress(byte[] source, int sourceOffset, int sourceLength, byte[] destination, int destinationOffset,
            int destinationLength, out int written)
        {
            written = LZ4Codec.Decode(source, sourceOffset, sourceLength, destination, destinationOffset, destinationLength);
            return written > 0;
        }
    }
}
