using System;

namespace DevSyncLib
{
    public class FsChangeResult : IEquatable<FsChangeResult>
    {
        public FsChangeType ChangeType;
        public string Path;
        public FsChangeResultCode ResultCode;
        public string ErrorMessage;
        public string Key => Path;
        public bool IsEmpty => ChangeType == FsChangeType.EmptyMarker;
        public static readonly FsChangeResult Empty = new FsChangeResult { ChangeType = FsChangeType.EmptyMarker };

        public bool Equals(FsChangeResult other)
        {
            return other != null && ChangeType == other.ChangeType && Path == other.Path && ResultCode == other.ResultCode && ErrorMessage == other.ErrorMessage;
        }
    }
}
