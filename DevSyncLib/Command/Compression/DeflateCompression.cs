using System.IO;
using System.IO.Compression;

namespace DevSyncLib.Command.Compression;

public class DeflateCompression : StreamCompression<DeflateStream>
{
    protected override DeflateStream CreateCompressStream(Stream stream)
    {
        return new DeflateStream(stream, CompressionLevel.Fastest);
    }

    protected override DeflateStream CreateDecompressStream(Stream stream)
    {
        return new DeflateStream(stream, CompressionMode.Decompress);
    }
}