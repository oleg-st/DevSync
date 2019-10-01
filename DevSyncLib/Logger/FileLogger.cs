using System;
using System.IO;

namespace DevSyncLib.Logger
{
    public class FileLogger : ILogger
    {
        private readonly string _filename;

        public FileLogger(string filename)
        {
            _filename = filename;
        }

        public void Log(string text, LogLevel level)
        {
            lock (this)
            {
                File.AppendAllText(_filename, $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] {level}: {text}\n");
            }
        }
    }
}
