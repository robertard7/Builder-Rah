#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RahOllamaOnly.Metrics;

namespace RahBuilder.Ui;

public static class ResilienceMetricsSnapshotFormatter
{
    public static string BuildSnapshot(
        CircuitMetricsSnapshot metrics,
        IReadOnlyDictionary<string, CircuitMetricsSnapshot> byTool,
        int maxTools)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"CapturedAt: {DateTimeOffset.UtcNow:O}");
        sb.AppendLine($"Open={metrics.OpenCount} HalfOpen={metrics.HalfOpenCount} Closed={metrics.ClosedCount} Retry={metrics.RetryAttempts}");
        sb.AppendLine("ByTool:");

        foreach (var entry in byTool.OrderByDescending(kvp => kvp.Value.OpenCount)
                     .ThenByDescending(kvp => kvp.Value.RetryAttempts)
                     .Take(Math.Max(0, maxTools)))
        {
            sb.AppendLine($"- {entry.Key}: open={entry.Value.OpenCount} retry={entry.Value.RetryAttempts}");
        }

        return sb.ToString().TrimEnd();
    }
}
