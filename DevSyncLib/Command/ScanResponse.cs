using System.Collections.Generic;
using DevSyncLib.Logger;

namespace DevSyncLib.Command
{
    public class ScanResponse : Packet
    {
        public override short Signature => 4;
        public IEnumerable<FsEntry> FileList;

        public override void Read(Reader reader)
        {
            FileList = ReadFsEntries(reader);
        }

        protected IEnumerable<FsEntry> ReadFsEntries(Reader reader)
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
            foreach (var fsEntry in FileList)
            {
                writer.WriteFsEntry(fsEntry);
            }
            writer.WriteFsEntry(FsEntry.Empty);
        }

        public ScanResponse(ILogger logger) : base(logger)
        {
        }
    }
}
