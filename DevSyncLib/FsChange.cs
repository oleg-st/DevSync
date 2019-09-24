namespace DevSyncLib
{
    public class FsChange
    {
        public FsChangeType ChangeType;
        public FsEntry FsEntry;
        public FsEntry OldFsEntry;
        public bool HasBody => ChangeType == FsChangeType.Change && FsEntry.Length > 0;
        // change is expired -> ignore it
        public bool Expired;
        public string Key => FsEntry.Path;

        public override string ToString()
        {
            return $"{ChangeType} {(ChangeType == FsChangeType.Rename ? $"{OldFsEntry.Path} -> " : "")}{FsEntry.Path}{(ChangeType == FsChangeType.Change && FsEntry.Length >= 0 ? $", {FsEntry.Length}" : "")}";
        }
    }
}
