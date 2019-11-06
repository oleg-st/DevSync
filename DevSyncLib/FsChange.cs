using System;
using System.IO;

namespace DevSyncLib
{
    public class FsChange
    {
        public FsChangeType ChangeType;
        public FsEntry FsEntry;
        public string OldPath;
        public long BodySize => HasBody ? FsEntry.Length : 0;
        public bool HasBody => ChangeType == FsChangeType.Change && !FsEntry.IsDirectory;
        // change is expired -> ignore it
        public bool Expired;
        public string Key => FsEntry.Path;

        public bool IsEmpty => ChangeType == FsChangeType.EmptyMarker;
        public static readonly FsChange Empty = new FsChange {ChangeType = FsChangeType.EmptyMarker};

        public override string ToString()
        {
            return $"{ChangeType} {(ChangeType == FsChangeType.Rename ? $"{OldPath} -> " : "")}{FsEntry.Path}{(ChangeType == FsChangeType.Change && FsEntry.Length >= 0 ? $", {FsEntry.Length}" : "")}";
        }
        
        public static FsChange FromFilename(string fullname, string path)
        {
            var fsChange = new FsChange
            {
                FsEntry = new FsEntry { Path = path }
            };
            var fileInfo = new FileInfo(fullname);
            var attributes = fileInfo.Attributes;
            // exists?
            if (attributes != (FileAttributes)(-1))
            { 
                fsChange.ChangeType = FsChangeType.Change;
                fsChange.FsEntry.LastWriteTime = fileInfo.LastWriteTime;
                fsChange.FsEntry.Length = (attributes & FileAttributes.Directory) != 0 ? -1 : fileInfo.Length;
            } else 
            {
                fsChange.ChangeType = FsChangeType.Remove;
                // dummy
                fsChange.FsEntry.Length = 0;
                fsChange.FsEntry.LastWriteTime = DateTime.UnixEpoch;
            }
            return fsChange;
        }
    }
}
