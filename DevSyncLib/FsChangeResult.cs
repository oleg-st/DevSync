namespace DevSyncLib
{
    public class FsChangeResult
    {
        public FsChangeType ChangeType;
        public string Path;
        public FsChangeResultCode ResultCode;
        public string Error;
        public string Key => Path;
    }
}
