#nullable enable
using System;
using System.Collections.Concurrent;
using RahOllamaOnly.Tools.Handlers;

namespace RahOllamaOnly.Metrics;

public sealed class CircuitMetricsStore
{
    private readonly ConcurrentDictionary<CircuitState, int> _stateCounts = new();
    private int _retryAttempts;

    public void RecordStateChange(CircuitState previous, CircuitState current)
    {
        _stateCounts.AddOrUpdate(current, 1, (_, count) => count + 1);
    }

    public void RecordRetryAttempt() => System.Threading.Interlocked.Increment(ref _retryAttempts);

    public CircuitMetricsSnapshot Snapshot()
    {
        _stateCounts.TryGetValue(CircuitState.Open, out var openCount);
        _stateCounts.TryGetValue(CircuitState.HalfOpen, out var halfOpenCount);
        _stateCounts.TryGetValue(CircuitState.Closed, out var closedCount);

        return new CircuitMetricsSnapshot(openCount, halfOpenCount, closedCount, _retryAttempts);
    }
}

public sealed record CircuitMetricsSnapshot(
    int OpenCount,
    int HalfOpenCount,
    int ClosedCount,
    int RetryAttempts);
