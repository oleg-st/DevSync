using System.Diagnostics;

namespace DevSyncLib;

public struct Timer
{
    private const long NoTimestamp = -1;

    private long _fireTimestamp;

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

    public bool IsRunning => _fireTimestamp != NoTimestamp;

    public void Stop()
    {
        _fireTimestamp = NoTimestamp;
    }

    public void Start(int timeout)
    {
        _fireTimestamp = Stopwatch.GetTimestamp() + timeout * Stopwatch.Frequency / 1000;
    }

    public bool IsFired => Stopwatch.GetTimestamp() >= _fireTimestamp;
}