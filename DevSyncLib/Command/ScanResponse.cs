using DevSyncLib.Logger;
using System.Collections.Generic;
using System.Diagnostics;

namespace DevSyncLib.Command;

public class ScanResponse(ILogger logger) : Packet(logger)
{
    public override short Signature => 4;
    public IEnumerable<FsEntry>? FileList;

    public override void Read(Reader reader)
    {
        FileList = ReadFsEntries(reader);
    }

    protected static IEnumerable<FsEntry> ReadFsEntries(Reader reader)
    {
        while (true)
        {
            var fsEntry = reader.ReadFsEntry();
            if (fsEntry.IsEmpty)
            {
                break;
            }
            yield return fsEntry;
        }
    }

    public override void Write(Writer writer)
    {
        Debug.Assert(FileList != null);
        foreach (var fsEntry in FileList)
        {
            writer.WriteFsEntry(fsEntry);
        }
        writer.WriteFsEntry(FsEntry.Empty);
    }
}