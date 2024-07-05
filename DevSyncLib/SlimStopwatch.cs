using System;
using System.Diagnostics;

namespace DevSyncLib;

public struct SlimStopwatch
{
    private const long NoTimestamp = -1;

    public long StartTimestamp;

    public static SlimStopwatch Create(bool start = false) => 
        new() { StartTimestamp = start  ? Stopwatch.GetTimestamp() : NoTimestamp };

    public static SlimStopwatch StartNew() => Create(true);

    public bool IsRunning => StartTimestamp != NoTimestamp;

    public void Stop()
    {
        StartTimestamp = NoTimestamp;
    }

    public long ElapsedMilliseconds => StartTimestamp != NoTimestamp ? (Stopwatch.GetTimestamp() - StartTimestamp) * 1000 / Stopwatch.Frequency : 0;

    public TimeSpan Elapsed => TimeSpan.FromMilliseconds(ElapsedMilliseconds);

    public void Start()
    {
        StartTimestamp = Stopwatch.GetTimestamp();
    }
}