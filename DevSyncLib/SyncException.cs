using System;

namespace DevSyncLib
{
    public class SyncException : Exception
    {
        public bool Recoverable { get; }
        public bool NeedToWait { get; }

        public SyncException(string message, bool recoverable = false, bool needToWait = true) : base(message)
        {
            Recoverable = recoverable;
            NeedToWait = needToWait;
        }
    }
}
