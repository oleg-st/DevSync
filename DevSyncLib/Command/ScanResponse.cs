using System.Collections.Generic;

namespace DevSyncLib.Command
{
    public class ScanResponse : Packet
    {
        public override short Signature => 4;
        public Dictionary<string, FsEntry> FileList;

        public override void Read(Reader reader)
        {
            int count = reader.ReadInt();
            FileList = new Dictionary<string, FsEntry>(count);
            for (int i = 0; i < count; i++)
            {
                var fsEntry = reader.ReadFsEntry();
                FileList.Add(fsEntry.Path, fsEntry);
            }
        }

        public override void Write(Writer writer)
        {
            writer.WriteInt(FileList.Count);
            foreach (var fsEntry in FileList.Values)
            {
                writer.WriteFsEntry(fsEntry);
            }
        }
    }
}
