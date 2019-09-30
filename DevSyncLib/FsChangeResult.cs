namespace DevSyncLib
{
    public class FsChangeResult
    {
        public FsChangeType ChangeType;
        public string Path;
        public FsChangeResultCode ResultCode;
        public string ErrorMessage;
        public string Key => Path;
        public bool IsEmpty => ChangeType == FsChangeType.EmptyMarker;
        public static readonly FsChangeResult Empty = new FsChangeResult { ChangeType = FsChangeType.EmptyMarker };
    }
}
