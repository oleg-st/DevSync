namespace DevSyncLib.Logger
{
    public interface ILogger
    {
        void Log(string text, LogLevel level = LogLevel.Info);

        void Pause();

        void Resume();
    }
}
