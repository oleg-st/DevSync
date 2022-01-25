using System.Threading;

namespace DevSyncLib.Logger
{
    public abstract class BaseLogger : ILogger
    {
        protected readonly ManualResetEvent NoPauseEvent = new(true);

        public LogLevel Level { get; set; }

        public const LogLevel DefaultLevel = LogLevel.Info;

        protected BaseLogger(LogLevel level = DefaultLevel)
        {
            Level = level;
        }

        public virtual void Pause()
        {
            NoPauseEvent.Reset();
        }

        public virtual void Resume()
        {
            NoPauseEvent.Set();
        }

        public virtual void Dispose()
        {
            NoPauseEvent.Dispose();
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
}
