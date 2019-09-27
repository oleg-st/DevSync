using System;
using System.Collections.Generic;
using System.Text;

namespace DevSyncLib.Logger
{
    public interface ILogger
    {
        void Log(string text, LogLevel level = LogLevel.Info);
    }
}
