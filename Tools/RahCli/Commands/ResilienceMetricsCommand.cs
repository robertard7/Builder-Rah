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
        var format = ResolveFormat(args, jsonOutput);
        var writeCsv = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase);
        var csvHeaderWritten = false;

        if (watch)
        {
            while (true)
            {
                var metrics = ResilienceDiagnosticsHub.Snapshot();
                WriteMetrics(metrics, jsonOutput, writeCsv, includeTimestamp: true, ref csvHeaderWritten);
                Thread.Sleep(DefaultWatchIntervalMs);
            }
        }

        var snapshot = ResilienceDiagnosticsHub.Snapshot();
        WriteMetrics(snapshot, jsonOutput, writeCsv, includeTimestamp: false, ref csvHeaderWritten);
        return ExitCodes.Success;
    }

    private static string ResolveFormat(string[] args, bool jsonOutput)
    {
        var formatArg = GetArgValue(args, "--format");
        if (!string.IsNullOrWhiteSpace(formatArg))
            return formatArg;

        if (args.Any(a => string.Equals(a, "--csv", StringComparison.OrdinalIgnoreCase)))
            return "csv";

        return jsonOutput ? "json" : "text";
    }

    private static string? GetArgValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }

    private static void WriteMetrics(
        CircuitMetricsSnapshot metrics,
        bool jsonOutput,
        bool writeCsv,
        bool includeTimestamp,
        ref bool csvHeaderWritten)
    {
        if (jsonOutput)
        {
            if (includeTimestamp)
                Output.JsonOutput.Write(new { capturedAt = DateTimeOffset.UtcNow, metrics });
            else
                Output.JsonOutput.Write(metrics);
            return;
        }

        if (writeCsv)
        {
            if (!csvHeaderWritten)
            {
                Console.WriteLine("capturedAt,openCount,halfOpenCount,closedCount,retryAttempts");
                csvHeaderWritten = true;
            }
            var timestamp = DateTimeOffset.UtcNow.ToString("O");
            Console.WriteLine($"{timestamp},{metrics.OpenCount},{metrics.HalfOpenCount},{metrics.ClosedCount},{metrics.RetryAttempts}");
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
