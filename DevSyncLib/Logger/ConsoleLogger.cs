using System;

namespace DevSyncLib.Logger
{
    public class ConsoleLogger : ILogger
    {
        private readonly ConditionVariable _isPausedConditionVariable;
        private volatile bool _isPaused;

        public ConsoleLogger()
        {
            _isPausedConditionVariable = new ConditionVariable();
        }

        public void Log(string text, LogLevel level)
        {
            _isPausedConditionVariable.WaitForCondition(() => !_isPaused);
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

        public void Pause()
        {
            lock (_isPausedConditionVariable)
            {
                _isPaused = true;
            }
        }

        public void Resume()
        {
            lock (_isPausedConditionVariable)
            {
                _isPaused = false;
            }
            _isPausedConditionVariable.Notify();
        }
    }
}
