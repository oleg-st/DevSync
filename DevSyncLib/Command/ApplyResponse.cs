using DevSyncLib.Logger;
using System.Collections.Generic;
using System.Diagnostics;

namespace DevSyncLib.Command;

public class ApplyResponse(ILogger logger) : Packet(logger)
{
    public override short Signature => 6;

    public IEnumerable<FsChangeResult>? Result;

    public static IEnumerable<FsChangeResult> ReadResults(Reader reader)
    {
        while (true)
        {
            var fsChangeResult = reader.ReadFsChangeResult();
            if (fsChangeResult.IsEmpty)
            {
                break;
            }
            yield return fsChangeResult;
        }
    }

    public override void Read(Reader reader)
    {
        Result = ReadResults(reader);
    }

    public override void Write(Writer writer)
    {
        Debug.Assert(Result != null);
        foreach (var fsChangeResult in Result)
        {
            writer.WriteFsChangeResult(fsChangeResult);
        }
        writer.WriteFsChangeResult(FsChangeResult.Empty);
    }
}