using System;

namespace DevSyncLib
{
    public class FsChange
    {
        public FsChangeType ChangeType;
        public string Path;
        // change
        public long Length;
        public bool IsDirectory;
        public DateTime LastWriteTime;
        // rename
        public string OldPath;
        public long BodySize => HasBody ? Length : 0;
        public bool HasBody => ChangeType == FsChangeType.Change && !IsDirectory;

        public bool IsEmpty => ChangeType == FsChangeType.EmptyMarker;
        public static readonly FsChange Empty = new FsChange(FsChangeType.EmptyMarker, null);

        public override string ToString()
        {
            return $"{ChangeType} {(ChangeType == FsChangeType.Rename ? $"{OldPath} -> " : "")}{Path}{(ChangeType == FsChangeType.Change && Length >= 0 ? $", {Length}" : "")}";
        }

        public FsChange(FsChangeType changeType, string path)
        {
            ChangeType = changeType;
            Path = path;
        }
    }
}
