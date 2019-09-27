namespace DevSyncLib
{
    public class FsChangeResult
    {
        public FsChangeType ChangeType;
        public string Path;
        public FsChangeResultCode ResultCode;
        public string Error;
        public string Key => Path;
        public bool IsEndMarker => ChangeType == FsChangeType.EndMarker;
        public static readonly FsChangeResult EndChangeMarker = new FsChangeResult { ChangeType = FsChangeType.EndMarker };
    }
}
