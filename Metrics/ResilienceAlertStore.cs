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
    int RetryDelta,
    bool Acknowledged,
    DateTimeOffset? AcknowledgedAt);

public sealed class ResilienceAlertStore
{
    private readonly ConcurrentDictionary<string, ResilienceAlertRule> _rules = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<string> _eventIds = new();
    private readonly ConcurrentDictionary<string, ResilienceAlertEvent> _events = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _activeStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _maxEvents;
    public event Action<ResilienceAlertEvent>? AlertRaised;
    public event Action<ResilienceAlertEvent>? AlertAcknowledged;

    public ResilienceAlertStore(int maxEvents = 200)
    {
        _maxEvents = Math.Max(1, maxEvents);
    }

    public ResilienceAlertRule AddRule(string name, int openThreshold, int retryThreshold, int windowMinutes, string severity)
    {
        var rule = new ResilienceAlertRule(
            Guid.NewGuid().ToString("N"),
            string.IsNullOrWhiteSpace(name) ? "threshold" : name.Trim(),
            Math.Max(0, openThreshold),
            Math.Max(0, retryThreshold),
            Math.Max(1, windowMinutes),
            NormalizeSeverity(severity),
            true);
        _rules[rule.Id] = rule;
        _activeStates[rule.Id] = false;
        return rule;
    }

    public ResilienceAlertRule? UpdateRule(
        string ruleId,
        string? name,
        int? openThreshold,
        int? retryThreshold,
        int? windowMinutes,
        string? severity,
        bool? enabled)
    {
        if (string.IsNullOrWhiteSpace(ruleId))
            return null;
        if (!_rules.TryGetValue(ruleId, out var existing))
            return null;

        var updated = existing with
        {
            Name = string.IsNullOrWhiteSpace(name) ? existing.Name : name.Trim(),
            OpenThreshold = openThreshold.HasValue ? Math.Max(0, openThreshold.Value) : existing.OpenThreshold,
            RetryThreshold = retryThreshold.HasValue ? Math.Max(0, retryThreshold.Value) : existing.RetryThreshold,
            WindowMinutes = windowMinutes.HasValue ? Math.Max(1, windowMinutes.Value) : existing.WindowMinutes,
            Severity = string.IsNullOrWhiteSpace(severity) ? existing.Severity : NormalizeSeverity(severity),
            Enabled = enabled ?? existing.Enabled
        };
        _rules[ruleId] = updated;
        return updated;
    }

    public IReadOnlyList<ResilienceAlertRule> ListRules() =>
        _rules.Values.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();

    public IReadOnlyList<ResilienceAlertEvent> ListEvents(
        int limit = 50,
        string? severity = null,
        bool includeAcknowledged = true,
        string? ruleId = null)
    {
        if (limit <= 0) limit = 50;
        var normalizedSeverity = string.IsNullOrWhiteSpace(severity) ? null : severity.Trim().ToLowerInvariant();

        var ordered = _eventIds.Reverse()
            .Select(id => _events.TryGetValue(id, out var evt) ? evt : null)
            .Where(evt => evt != null)
            .Select(evt => evt!)
            .Where(evt => string.IsNullOrWhiteSpace(ruleId) || string.Equals(evt.RuleId, ruleId, StringComparison.OrdinalIgnoreCase))
            .Where(evt => normalizedSeverity == null || string.Equals(evt.Severity, normalizedSeverity, StringComparison.OrdinalIgnoreCase))
            .Where(evt => includeAcknowledged || !evt.Acknowledged)
            .Take(limit)
            .ToList();

        return ordered;
    }

    public bool RemoveRule(string ruleId)
    {
        if (string.IsNullOrWhiteSpace(ruleId))
            return false;
        _activeStates.TryRemove(ruleId, out _);
        return _rules.TryRemove(ruleId, out _);
    }

    public void Clear()
    {
        _rules.Clear();
        _activeStates.Clear();
        while (_eventIds.TryDequeue(out _)) { }
        _events.Clear();
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
                var alert = new ResilienceAlertEvent(
                    Guid.NewGuid().ToString("N"),
                    rule.Id,
                    message,
                    rule.Severity,
                    DateTimeOffset.UtcNow,
                    openDelta,
                    retryDelta,
                    false,
                    null);
                _events[alert.Id] = alert;
                _eventIds.Enqueue(alert.Id);
                TrimEvents();
                AlertRaised?.Invoke(alert);
            }
        }
    }

    public ResilienceAlertEvent? Acknowledge(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
            return null;
        if (!_events.TryGetValue(eventId, out var existing))
            return null;
        if (existing.Acknowledged)
            return existing;
        var updated = existing with { Acknowledged = true, AcknowledgedAt = DateTimeOffset.UtcNow };
        _events[eventId] = updated;
        AlertAcknowledged?.Invoke(updated);
        return updated;
    }

    public void Reset()
    {
        _rules.Clear();
        _activeStates.Clear();
        while (_eventIds.TryDequeue(out _)) { }
        _events.Clear();
    }

    private void TrimEvents()
    {
        while (_eventIds.Count > _maxEvents && _eventIds.TryDequeue(out var id))
            _events.TryRemove(id, out _);
    }

    private static string NormalizeSeverity(string? severity)
    {
        var normalized = string.IsNullOrWhiteSpace(severity) ? "warning" : severity.Trim().ToLowerInvariant();
        return normalized is "warning" or "critical" ? normalized : "warning";
    }
}
