using System;

namespace DevSyncLib.Command.Compression
{
    public interface ICompression
    {
        bool TryCompress(ReadOnlySpan<byte> source, Span<byte> destination, out int written);

        bool TryDecompress(ReadOnlySpan<byte> source, Span<byte> destination, out int written);
    }
}
