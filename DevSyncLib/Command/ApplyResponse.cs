using System.Collections.Generic;

namespace DevSyncLib.Command
{
    public class ApplyResponse : Packet
    {
        public override int Signature => 6;

        public List<FsChangeResult> Result;

        public override void Read(Reader reader)
        {
            int count = reader.ReadInt();
            Result = new List<FsChangeResult>(count);
            for (int i = 0; i < count; i++)
            {
                var fsChangeResult = reader.ReadFsChangeResult();
                Result.Add(fsChangeResult);
            }
        }

        public override void Write(Writer writer)
        {
            writer.WriteInt(Result.Count);
            foreach (var fsChangeResult in Result)
            {
                writer.WriteFsChangeResult(fsChangeResult);
            }
        }
    }
}
