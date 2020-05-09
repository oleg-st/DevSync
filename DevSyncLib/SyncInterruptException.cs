using System;

namespace DevSyncLib
{
    public class SyncInterruptException : Exception
    {
        public SyncInterruptException() : base("Interrupted")
        {
        }
    }
}
