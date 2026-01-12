#nullable enable
using System;
using System.Linq;
using System.Threading;
using RahOllamaOnly.Metrics;

namespace RahCli.Commands;

public static class ResilienceMetricsCommand
{
    private const int DefaultWatchIntervalMs = 2000;

    public static int Execute(CommandContext context, string[] args, bool jsonOutput)
    {
        var watch = args.Any(a => string.Equals(a, "--watch", StringComparison.OrdinalIgnoreCase));

        if (watch)
        {
            while (true)
            {
                var metrics = ResilienceDiagnosticsHub.Snapshot();
                WriteMetrics(metrics, jsonOutput, includeTimestamp: true);
                Thread.Sleep(DefaultWatchIntervalMs);
            }
        }

        var snapshot = ResilienceDiagnosticsHub.Snapshot();
        WriteMetrics(snapshot, jsonOutput, includeTimestamp: false);
        return ExitCodes.Success;
    }

    private static void WriteMetrics(CircuitMetricsSnapshot metrics, bool jsonOutput, bool includeTimestamp)
    {
        if (jsonOutput)
        {
            if (includeTimestamp)
                Output.JsonOutput.Write(new { capturedAt = DateTimeOffset.UtcNow, metrics });
            else
                Output.JsonOutput.Write(metrics);
            return;
        }

        if (includeTimestamp)
            Console.WriteLine($"Captured at: {DateTimeOffset.UtcNow:O}");

        var header = string.Format("{0,8} {1,10} {2,8} {3,13}", "Open", "HalfOpen", "Closed", "RetryAttempts");
        var values = string.Format("{0,8} {1,10} {2,8} {3,13}", metrics.OpenCount, metrics.HalfOpenCount, metrics.ClosedCount, metrics.RetryAttempts);
        Console.WriteLine(header);
        Console.WriteLine(values);
        Console.WriteLine();
    }
}
