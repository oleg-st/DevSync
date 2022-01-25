using System.Diagnostics;

namespace DevSyncLib
{
    public struct Timer
    {
        private const long NoTimestamp = -1;

        private long FireTimestamp;

        public static Timer Create(bool start = false, int timeout = 0)
        {
            var timer = new Timer();
            if (start)
            {
                timer.Start(timeout);
            }
            else
            {
                timer.Stop();
            }
            return timer;
        }

        public bool IsRunning => FireTimestamp != NoTimestamp;

        public void Stop()
        {
            FireTimestamp = NoTimestamp;
        }

        public void Start(int timeout)
        {
            FireTimestamp = Stopwatch.GetTimestamp() + timeout * Stopwatch.Frequency / 1000;
        }

        public bool IsFired => Stopwatch.GetTimestamp() >= FireTimestamp;
    }
}
