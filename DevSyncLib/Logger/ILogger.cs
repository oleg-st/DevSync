using System;

namespace DevSyncLib.Logger;

public interface ILogger : IDisposable
{
    bool IsDebug => Level <= LogLevel.Debug;

    LogLevel Level { get; set; }

    void Log(string text, LogLevel level = LogLevel.Info);

    void Pause();

    void Resume();
}