using System.IO;
using System.IO.Compression;

namespace DevSyncLib.Command.Compression;

public class GZipCompression : StreamCompression<GZipStream>
{
    protected override GZipStream CreateCompressStream(Stream stream)
    {
        return new GZipStream(stream, CompressionLevel.Fastest);
    }

    protected override GZipStream CreateDecompressStream(Stream stream)
    {
        return new GZipStream(stream, CompressionMode.Decompress);
    }
}