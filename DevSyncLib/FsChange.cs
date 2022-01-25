using System;

namespace DevSyncLib
{
    public class FsChange : IEquatable<FsChange>
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
        public bool HasBody => IsChange && !IsDirectory;

        public static readonly FsChange Empty = new FsChange(FsChangeType.EmptyMarker, null);

        public bool IsEmpty => ChangeType == FsChangeType.EmptyMarker;

        // need body
        public bool IsChange => ChangeType == FsChangeType.Change || ChangeType == FsChangeType.ChangeAndRename;
        // has old path
        public bool IsRename => ChangeType == FsChangeType.Rename || ChangeType == FsChangeType.ChangeAndRename;

        public bool IsRemove => ChangeType == FsChangeType.Remove;

        public override string ToString()
        {
            return $"{ChangeType} {(IsRename ? $"{OldPath} -> " : "")}{Path}{(ChangeType == FsChangeType.Change && Length >= 0 ? $", {Length}" : "")}";
        }

        public bool Equals(FsChange other)
        {
            return other != null && ChangeType == other.ChangeType && ChangeType switch
            {
                FsChangeType.Change => Path == other.Path &&
                                       Length == other.Length &&
                                       LastWriteTime.Equals(other.LastWriteTime),
                FsChangeType.Remove => Path == other.Path,
                FsChangeType.Rename => Path == other.Path && OldPath == other.OldPath,
                FsChangeType.ChangeAndRename => Path == other.Path &&
                                                OldPath == other.OldPath &&
                                                Length == other.Length &&
                                                LastWriteTime.Equals(other.LastWriteTime),
                _ => true
            };
        }

        public FsChange(FsChangeType changeType, string path)
        {
            ChangeType = changeType;
            Path = path;
        }
    }
}
