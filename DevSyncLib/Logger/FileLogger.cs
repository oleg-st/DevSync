using System;
using System.IO;

namespace DevSyncLib.Logger
{
    public class FileLogger : BaseLogger
    {
        private readonly string _filename;

        public FileLogger(string filename, LogLevel level = DefaultLevel) : base(level)
        {
            _filename = filename;
        }

        protected override void AddLog(string text, LogLevel level)
        {
            lock (this)
            {
                File.AppendAllText(_filename, $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] {level}: {text}\n");
            }
        }
    }
}
