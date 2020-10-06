using DevSyncLib.Logger;
using Moq;

namespace DevSyncTest.Command
{
    public static class LoggerHelper
    {
        public static ILogger DummyLogger;

        static LoggerHelper()
        {
            var logger = new Mock<ILogger>();
            logger.Setup(x => x.Log(It.IsAny<string>(), It.IsAny<LogLevel>()));
            DummyLogger = logger.Object;
        }
    }
}
