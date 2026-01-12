#nullable enable
using System;
using RahOllamaOnly.Metrics;
using Xunit;

namespace RahBuilder.Tests.Metrics;

public sealed class ResilienceHistoryStoreTests
{
    [Fact]
    public void BucketAndLimit_WorkAsExpected()
    {
        var store = new ResilienceHistoryStore();
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-10);
        for (var i = 0; i < 6; i++)
        {
            var sampleTime = baseTime.AddMinutes(i);
            store.Add(new CircuitMetricsSnapshot(i, 0, 0, 0), sampleTime);
        }

        var range = store.SnapshotRange(baseTime, baseTime.AddMinutes(5), limit: 3, bucketMinutes: 2);
        Assert.True(range.Count <= 3);
        Assert.True(range.Count > 0);
    }
}
