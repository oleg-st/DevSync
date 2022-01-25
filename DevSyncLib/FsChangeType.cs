namespace DevSyncLib
{
    public enum FsChangeType
    {
        Remove,
        Change,
        Rename,
        // Change + Rename combined: file -> change, directory -> change + rename
        ChangeAndRename,
        // End of changes marker
        EmptyMarker
    }
}
