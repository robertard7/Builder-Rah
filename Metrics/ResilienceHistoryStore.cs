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
        Add(snapshot, DateTimeOffset.UtcNow);
    }

    public void Add(CircuitMetricsSnapshot snapshot, DateTimeOffset timestamp)
    {
        _samples.Enqueue(new ResilienceMetricsSample(timestamp, snapshot));
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

    public IReadOnlyList<ResilienceMetricsSample> SnapshotRange(DateTimeOffset? start, DateTimeOffset? end, int? limit = null, int? bucketMinutes = null)
    {
        var effectiveEnd = end ?? DateTimeOffset.UtcNow;
        var effectiveStart = start ?? effectiveEnd - _window;
        if (effectiveEnd < effectiveStart)
        {
            var swap = effectiveStart;
            effectiveStart = effectiveEnd;
            effectiveEnd = swap;
        }

        var items = _samples.Where(s => s.Timestamp >= effectiveStart && s.Timestamp <= effectiveEnd)
            .OrderBy(s => s.Timestamp)
            .ToList();

        if (bucketMinutes.HasValue && bucketMinutes.Value > 0)
            items = Bucket(items, bucketMinutes.Value);

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

    private static List<ResilienceMetricsSample> Bucket(List<ResilienceMetricsSample> items, int bucketMinutes)
    {
        var buckets = new Dictionary<DateTimeOffset, ResilienceMetricsSample>();
        var bucketSpan = TimeSpan.FromMinutes(bucketMinutes);
        foreach (var item in items)
        {
            var bucketStartTicks = item.Timestamp.UtcTicks / bucketSpan.Ticks * bucketSpan.Ticks;
            var bucketStart = new DateTimeOffset(bucketStartTicks, TimeSpan.Zero);
            buckets[bucketStart] = item;
        }
        return buckets.OrderBy(kvp => kvp.Key).Select(kvp => new ResilienceMetricsSample(kvp.Key, kvp.Value.Metrics)).ToList();
    }
}
