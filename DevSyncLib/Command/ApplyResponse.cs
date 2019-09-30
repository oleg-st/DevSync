using System.Collections.Generic;
using DevSyncLib.Logger;

namespace DevSyncLib.Command
{
    public class ApplyResponse : Packet
    {
        public override short Signature => 6;

        public IEnumerable<FsChangeResult> Result;

        public IEnumerable<FsChangeResult> ReadResults(Reader reader)
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
            foreach (var fsChangeResult in Result)
            {
                writer.WriteFsChangeResult(fsChangeResult);
            }
            writer.WriteFsChangeResult(FsChangeResult.Empty);
        }

        public ApplyResponse(ILogger logger) : base(logger)
        {
        }
    }
}
