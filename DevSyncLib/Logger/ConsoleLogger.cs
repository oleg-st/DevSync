using System;

namespace DevSyncLib.Logger;

public class ConsoleLogger(LogLevel level = BaseLogger.DefaultLevel) : BaseLogger(level)
{
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