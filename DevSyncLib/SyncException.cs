using System;

namespace DevSyncLib;

public class SyncException(string message, bool recoverable = false, bool needToWait = true)
    : Exception(message)
{
    public bool Recoverable { get; } = recoverable;
    public bool NeedToWait { get; } = needToWait;
}