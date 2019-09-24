using System.IO;

namespace DevSyncLib
{
    // TODO: remove or refactor in future
    public static class Logger
    {
        public static void Log(string text)
        {
            File.AppendAllText("devsync.log", text + "\n\n");
        }
    }
}
