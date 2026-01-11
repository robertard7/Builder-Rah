#nullable enable
using System;
using System.Diagnostics;
using System.Threading;

namespace RahBuilder.Metrics;

public static class TelemetryRegistry
{
    private static long _requestCount;
    private static long _errorCount;
    private static long _totalLatencyMs;
    private static readonly Stopwatch Uptime = Stopwatch.StartNew();

    public static void RecordRequest(long latencyMs, bool isError)
    {
        Interlocked.Increment(ref _requestCount);
        if (isError)
            Interlocked.Increment(ref _errorCount);
        Interlocked.Add(ref _totalLatencyMs, latencyMs);
    }

    public static object Snapshot()
    {
        var req = Interlocked.Read(ref _requestCount);
        var err = Interlocked.Read(ref _errorCount);
        var totalLatency = Interlocked.Read(ref _totalLatencyMs);
        var avg = req == 0 ? 0 : totalLatency / req;
        return new
        {
            uptimeSeconds = (long)Uptime.Elapsed.TotalSeconds,
            requestCount = req,
            errorCount = err,
            avgLatencyMs = avg
        };
    }
}
