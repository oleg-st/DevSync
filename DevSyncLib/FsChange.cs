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
        public bool HasBody => ChangeType == FsChangeType.Change && !IsDirectory;

        public bool IsEmpty => ChangeType == FsChangeType.EmptyMarker;
        public static readonly FsChange Empty = new FsChange(FsChangeType.EmptyMarker, null);

        public override string ToString()
        {
            return $"{ChangeType} {(ChangeType == FsChangeType.Rename ? $"{OldPath} -> " : "")}{Path}{(ChangeType == FsChangeType.Change && Length >= 0 ? $", {Length}" : "")}";
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
