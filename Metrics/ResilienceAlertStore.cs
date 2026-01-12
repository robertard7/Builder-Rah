#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace RahOllamaOnly.Metrics;

public sealed record ResilienceAlertRule(
    string Id,
    string Name,
    int OpenThreshold,
    int RetryThreshold,
    int WindowMinutes,
    string Severity,
    bool Enabled);

public sealed record ResilienceAlertEvent(
    string Id,
    string RuleId,
    string Message,
    string Severity,
    DateTimeOffset TriggeredAt,
    int OpenDelta,
    int RetryDelta);

public sealed class ResilienceAlertStore
{
    private readonly ConcurrentDictionary<string, ResilienceAlertRule> _rules = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<ResilienceAlertEvent> _events = new();
    private readonly ConcurrentDictionary<string, bool> _activeStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _maxEvents;

    public ResilienceAlertStore(int maxEvents = 200)
    {
        _maxEvents = Math.Max(1, maxEvents);
    }

    public ResilienceAlertRule AddRule(string name, int openThreshold, int retryThreshold, int windowMinutes, string severity)
    {
        var normalizedSeverity = string.IsNullOrWhiteSpace(severity) ? "warning" : severity.Trim().ToLowerInvariant();
        if (normalizedSeverity != "warning" && normalizedSeverity != "critical")
            normalizedSeverity = "warning";
        var rule = new ResilienceAlertRule(
            Guid.NewGuid().ToString("N"),
            string.IsNullOrWhiteSpace(name) ? "threshold" : name.Trim(),
            Math.Max(0, openThreshold),
            Math.Max(0, retryThreshold),
            Math.Max(1, windowMinutes),
            normalizedSeverity,
            true);
        _rules[rule.Id] = rule;
        _activeStates[rule.Id] = false;
        return rule;
    }

    public IReadOnlyList<ResilienceAlertRule> ListRules() =>
        _rules.Values.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();

    public IReadOnlyList<ResilienceAlertEvent> ListEvents(int limit = 50)
    {
        if (limit <= 0) limit = 50;
        return _events.Reverse().Take(limit).ToList();
    }

    public void Evaluate(CircuitMetricsSnapshot current, IReadOnlyList<ResilienceMetricsSample> history)
    {
        if (history.Count == 0)
            return;

        foreach (var rule in _rules.Values.Where(r => r.Enabled))
        {
            var window = TimeSpan.FromMinutes(rule.WindowMinutes);
            var cutoff = DateTimeOffset.UtcNow - window;
            var baseline = history.OrderBy(sample => sample.Timestamp).FirstOrDefault(sample => sample.Timestamp >= cutoff);
            if (baseline == null)
                continue;

            var openDelta = Math.Max(0, current.OpenCount - baseline.Metrics.OpenCount);
            var retryDelta = Math.Max(0, current.RetryAttempts - baseline.Metrics.RetryAttempts);
            var triggered = (rule.OpenThreshold > 0 && openDelta > rule.OpenThreshold)
                || (rule.RetryThreshold > 0 && retryDelta > rule.RetryThreshold);

            var wasActive = _activeStates.TryGetValue(rule.Id, out var active) && active;
            _activeStates[rule.Id] = triggered;

            if (triggered && !wasActive)
            {
                var message = $"Alert '{rule.Name}' triggered: open/hr={openDelta}, retry/hr={retryDelta}.";
                var alert = new ResilienceAlertEvent(Guid.NewGuid().ToString("N"), rule.Id, message, rule.Severity, DateTimeOffset.UtcNow, openDelta, retryDelta);
                _events.Enqueue(alert);
                TrimEvents();
            }
        }
    }

    public void Reset()
    {
        _rules.Clear();
        _activeStates.Clear();
        while (_events.TryDequeue(out _)) { }
    }

    private void TrimEvents()
    {
        while (_events.Count > _maxEvents && _events.TryDequeue(out _)) { }
    }
}
