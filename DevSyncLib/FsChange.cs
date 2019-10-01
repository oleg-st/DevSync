using System;
using System.IO;

namespace DevSyncLib
{
    public class FsChange
    {
        public FsChangeType ChangeType;
        public FsEntry FsEntry;
        public FsEntry OldFsEntry;
        public long BodySize => HasBody ? FsEntry.Length : 0;
        public bool HasBody => ChangeType == FsChangeType.Change && FsEntry.Length > 0;
        // change is expired -> ignore it
        public bool Expired;
        public string Key => FsEntry.Path;

        public bool IsEmpty => ChangeType == FsChangeType.EmptyMarker;
        public static readonly FsChange Empty = new FsChange {ChangeType = FsChangeType.EmptyMarker};

        public override string ToString()
        {
            return $"{ChangeType} {(ChangeType == FsChangeType.Rename ? $"{OldFsEntry.Path} -> " : "")}{FsEntry.Path}{(ChangeType == FsChangeType.Change && FsEntry.Length >= 0 ? $", {FsEntry.Length}" : "")}";
        }
        
        public static FsChange FromFilename(string fullname, string path)
        {
            var fsChange = new FsChange
            {
                FsEntry = new FsEntry { Path =  path}
            };

            // file
            var fileInfo = new FileInfo(fullname);
            if (fileInfo.Exists)
            { 
                fsChange.ChangeType = FsChangeType.Change;
                fsChange.FsEntry.LastWriteTime = fileInfo.LastWriteTime;
                fsChange.FsEntry.Length = fileInfo.Length;
            } else 
            { // directory
                var directoryInfo = new DirectoryInfo(fullname);
                if (directoryInfo.Exists)
                {
                    fsChange.ChangeType = FsChangeType.Change;
                    fsChange.FsEntry.LastWriteTime = directoryInfo.LastWriteTime;
                    fsChange.FsEntry.Length = -1;
                }
                // remove
                else
                {
                    fsChange.ChangeType = FsChangeType.Remove;
                    // dummy
                    fsChange.FsEntry.Length = 0;
                    fsChange.FsEntry.LastWriteTime = DateTime.UnixEpoch;
                }
            }
            return fsChange;
        }
    }
}
