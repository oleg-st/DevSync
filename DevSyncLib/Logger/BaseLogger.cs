using System;
using System.Threading;

namespace DevSyncLib.Logger;

public abstract class BaseLogger(LogLevel level = BaseLogger.DefaultLevel) : ILogger
{
    protected readonly ManualResetEvent NoPauseEvent = new(true);

    public LogLevel Level { get; set; } = level;

    public const LogLevel DefaultLevel = LogLevel.Info;

    public virtual void Pause() => NoPauseEvent.Reset();

    public virtual void Resume() => NoPauseEvent.Set();

    public virtual void Dispose()
    {
        NoPauseEvent.Dispose();
        GC.SuppressFinalize(this);
    }

    protected abstract void AddLog(string text, LogLevel level);

    public void Log(string text, LogLevel level = LogLevel.Info)
    {
        if (level >= Level)
        {
            NoPauseEvent.WaitOne();
            AddLog(text, level);
        }
    }
}