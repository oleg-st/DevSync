using System;
using System.Threading;

namespace DevSyncLib.Logger
{
    public class ConsoleLogger : ILogger
    {
        private volatile bool _isPaused;
        private readonly object _syncPaused = new object();

        public void Log(string text, LogLevel level)
        {
            Wait();

            string toLog = $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] {level}: {text}";
            if (level == LogLevel.Error)
            {
                Console.Error.WriteLine(toLog);
            }
            else
            {
                Console.WriteLine(toLog);
            }
        }

        private void Wait()
        {
            while (_isPaused)
            {
                lock (_syncPaused)
                {
                    Monitor.Wait(_syncPaused);
                }
            }
        }

        private void Notify()
        {
            lock (_syncPaused)
            {
                Monitor.Pulse(_syncPaused);
            }
        }
        public void Pause()
        {
            _isPaused = true;
            Notify();
        }

        public void Resume()
        {
            _isPaused = false;
            Notify();
        }
    }
}
