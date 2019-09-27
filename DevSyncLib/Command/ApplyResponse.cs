using System.Collections.Generic;

namespace DevSyncLib.Command
{
    public class ApplyResponse : Packet
    {
        public override short Signature => 6;

        public List<FsChangeResult> Result;

        public override void Read(Reader reader)
        {
            Result = new List<FsChangeResult>();
            while (true)
            {
                var fsChangeResult = reader.ReadFsChangeResult();
                if (fsChangeResult.IsEnd)
                {
                    break;
                }
                Result.Add(fsChangeResult);
            }
        }

        public override void Write(Writer writer)
        {
            foreach (var fsChangeResult in Result)
            {
                writer.WriteFsChangeResult(fsChangeResult);
            }
            writer.WriteFsChangeResult(FsChangeResult.EndChange);
        }
    }
}
