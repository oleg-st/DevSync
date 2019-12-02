using System;
using System.Threading;

namespace DevSyncLib.Logger
{
    public class ConsoleLogger : ILogger
    {
        private readonly ManualResetEvent _noPauseEvent = new ManualResetEvent(true);

        public void Log(string text, LogLevel level)
        {
            _noPauseEvent.WaitOne();
            var toLog = $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] {level}: {text}";
            if (level == LogLevel.Error)
            {
                Console.Error.WriteLine(toLog);
            }
            else
            {
                Console.WriteLine(toLog);
            }
        }

        public void Pause()
        {
            _noPauseEvent.Reset();
        }

        public void Resume()
        {
            _noPauseEvent.Set();
        }
    }
}
