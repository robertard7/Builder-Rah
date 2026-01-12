#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace RahOllamaOnly.Metrics;

public sealed record ResilienceMetricsSample(DateTimeOffset Timestamp, CircuitMetricsSnapshot Metrics);

public sealed class ResilienceHistoryStore
{
    private readonly ConcurrentQueue<ResilienceMetricsSample> _samples = new();
    private readonly int _maxSamples;
    private readonly TimeSpan _window;

    public ResilienceHistoryStore(int maxSamples = 300, TimeSpan? window = null)
    {
        _maxSamples = Math.Max(1, maxSamples);
        _window = window ?? TimeSpan.FromHours(1);
    }

    public void Add(CircuitMetricsSnapshot snapshot)
    {
        _samples.Enqueue(new ResilienceMetricsSample(DateTimeOffset.UtcNow, snapshot));
        Trim();
    }

    public IReadOnlyList<ResilienceMetricsSample> Snapshot(TimeSpan? window = null, int? limit = null)
    {
        var now = DateTimeOffset.UtcNow;
        var windowSpan = window ?? _window;
        var items = _samples.Where(s => now - s.Timestamp <= windowSpan)
            .OrderBy(s => s.Timestamp)
            .ToList();

        if (limit.HasValue && limit.Value > 0 && items.Count > limit.Value)
            items = items.Skip(items.Count - limit.Value).ToList();

        return items;
    }

    public void Reset()
    {
        while (_samples.TryDequeue(out _)) { }
    }

    private void Trim()
    {
        var now = DateTimeOffset.UtcNow;
        while (_samples.TryPeek(out var head) && now - head.Timestamp > _window)
            _samples.TryDequeue(out _);

        while (_samples.Count > _maxSamples && _samples.TryDequeue(out _)) { }
    }
}
