using System;
using System.IO;

namespace DevSyncLib.Logger;

public class FileLogger(string filename, LogLevel level = BaseLogger.DefaultLevel) : BaseLogger(level)
{
    protected override void AddLog(string text, LogLevel level)
    {
        lock (this)
        {
            File.AppendAllText(filename, $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] {level}: {text}\n");
        }
    }
}