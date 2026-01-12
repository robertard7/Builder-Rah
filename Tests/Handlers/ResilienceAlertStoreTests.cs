#nullable enable
using System;
using System.Collections.Generic;
using RahOllamaOnly.Metrics;
using Xunit;

namespace RahOllamaOnly.Tests.Handlers;

public sealed class ResilienceAlertStoreTests
{
    [Fact]
    public void Evaluate_TriggersAlertOncePerBreach()
    {
        var store = new ResilienceAlertStore();
        store.AddRule("open-spike", openThreshold: 2, retryThreshold: 0, windowMinutes: 60);

        var now = DateTimeOffset.UtcNow;
        var history = new List<ResilienceMetricsSample>
        {
            new(now.AddMinutes(-10), new CircuitMetricsSnapshot(0, 0, 0, 0)),
            new(now.AddMinutes(-5), new CircuitMetricsSnapshot(3, 0, 0, 0))
        };

        store.Evaluate(new CircuitMetricsSnapshot(3, 0, 0, 0), history);
        var first = store.ListEvents(10);
        Assert.Single(first);

        store.Evaluate(new CircuitMetricsSnapshot(4, 0, 0, 0), history);
        var second = store.ListEvents(10);
        Assert.Single(second);
    }
}
