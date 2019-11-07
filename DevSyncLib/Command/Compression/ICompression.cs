namespace DevSyncLib.Command.Compression
{
    public interface ICompression
    {
        bool TryCompress(byte[] source, int sourceOffset, int sourceLength, byte[] destination, int destinationOffset, int destinationLength, out int written);

        bool TryDecompress(byte[] source, int sourceOffset, int sourceLength, byte[] destination, int destinationOffset, int destinationLength, out int written);
    }
}
