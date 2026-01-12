#nullable enable
using System.Collections.Generic;
using RahBuilder.Ui;
using RahOllamaOnly.Metrics;
using Xunit;

namespace RahBuilder.Tests.Ui;

public sealed class ResilienceMetricsSnapshotFormatterTests
{
    [Fact]
    public void BuildSnapshot_FormatsMetricsAndTools()
    {
        var metrics = new CircuitMetricsSnapshot(2, 1, 5, 9);
        var byTool = new Dictionary<string, CircuitMetricsSnapshot>
        {
            ["tool.alpha"] = new CircuitMetricsSnapshot(1, 0, 0, 4),
            ["tool.beta"] = new CircuitMetricsSnapshot(3, 0, 0, 2)
        };

        var snapshot = ResilienceMetricsSnapshotFormatter.BuildSnapshot(metrics, byTool, maxTools: 2);

        Assert.Contains("Open=2", snapshot);
        Assert.Contains("HalfOpen=1", snapshot);
        Assert.Contains("Closed=5", snapshot);
        Assert.Contains("Retry=9", snapshot);
        Assert.Contains("tool.beta", snapshot);
        Assert.Contains("tool.alpha", snapshot);
    }
}
