using System;

namespace DevSyncLib.Logger
{
    public class ConsoleLogger : BaseLogger
    {
        public ConsoleLogger(LogLevel level = DefaultLevel) : base(level)
        {
        }

        protected override void AddLog(string text, LogLevel level)
        {
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
    }
}
