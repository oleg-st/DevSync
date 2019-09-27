using System;

namespace DevSyncLib.Logger
{
    public class ConsoleLogger : ILogger
    {
        public void Log(string text, LogLevel level)
        {
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
    }
}
