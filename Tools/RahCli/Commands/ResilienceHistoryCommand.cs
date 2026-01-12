#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RahBuilder.Headless.Api;
using RahOllamaOnly.Metrics;

namespace RahCli.Commands;

public static class ResilienceHistoryCommand
{
    public static int Execute(CommandContext context, string[] args, bool jsonOutput)
    {
        var startArg = GetArgValue(args, "--start");
        var endArg = GetArgValue(args, "--end");
        var bucketMinutes = ParseInt(GetArgValue(args, "--bucketMinutes"));
        var limit = ParseInt(GetArgValue(args, "--limit"));

        DateTimeOffset? start = ParseDateTime(startArg);
        DateTimeOffset? end = ParseDateTime(endArg);

        if (start.HasValue && end.HasValue && start.Value > end.Value)
        {
            WriteError(context, jsonOutput, ApiError.BadRequest("invalid_date_range"));
            return ExitCodes.UserError;
        }

        var history = ResilienceDiagnosticsHub.SnapshotHistoryRange(start, end, limit, bucketMinutes);
        if (jsonOutput)
        {
            Output.JsonOutput.Write(new { start, end, items = history });
            return ExitCodes.Success;
        }

        WriteTable(history);
        return ExitCodes.Success;
    }

    private static void WriteTable(IReadOnlyList<ResilienceMetricsSample> history)
    {
        Console.WriteLine(string.Format("{0,25} {1,8} {2,10} {3,8} {4,13}", "Timestamp", "Open", "HalfOpen", "Closed", "RetryAttempts"));
        foreach (var sample in history)
        {
            Console.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0,25} {1,8} {2,10} {3,8} {4,13}",
                sample.Timestamp.ToString("O"),
                sample.Metrics.OpenCount,
                sample.Metrics.HalfOpenCount,
                sample.Metrics.ClosedCount,
                sample.Metrics.RetryAttempts));
        }
        if (history.Count == 0)
            Console.WriteLine("No history samples.");
    }

    private static int? ParseInt(string? value)
    {
        if (int.TryParse(value, out var parsed))
            return parsed;
        return null;
    }

    private static DateTimeOffset? ParseDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (DateTimeOffset.TryParse(value, out var parsed))
            return parsed;
        return null;
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

    private static void WriteError(CommandContext context, bool jsonOutput, ApiError error)
    {
        if (!jsonOutput)
            return;
        Output.JsonOutput.WriteError(error);
    }
}
