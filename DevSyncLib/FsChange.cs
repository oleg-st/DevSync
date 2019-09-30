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
    }
}
