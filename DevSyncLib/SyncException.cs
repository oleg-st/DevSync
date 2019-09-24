using System;

namespace DevSyncLib
{
    public class SyncException : Exception
    {
        public bool Recoverable { get; }

        public SyncException(string message, bool recoverable = false) : base(message)
        {
            Recoverable = recoverable;
        }
    }
}
