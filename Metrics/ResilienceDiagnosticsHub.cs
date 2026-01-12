#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using RahOllamaOnly.Tools.Handlers;

namespace RahOllamaOnly.Metrics;

public static class ResilienceDiagnosticsHub
{
    private static readonly CircuitMetricsStore Store = new();
    private static readonly ConcurrentDictionary<CircuitBreaker, EventHandler<CircuitBreakerStateChangedEventArgs>> Subscriptions = new();
    private static readonly ConcurrentDictionary<CircuitBreaker, CircuitState> BreakerStates = new();
    private static readonly ConcurrentDictionary<string, CircuitMetricsStore> ToolStores = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ResilienceHistoryStore History = new();
    private static readonly ResilienceAlertStore Alerts = new();

    public static void Attach(CircuitBreaker breaker)
    {
        if (breaker == null)
            throw new ArgumentNullException(nameof(breaker));

        var handler = new EventHandler<CircuitBreakerStateChangedEventArgs>((_, args) =>
        {
            Store.RecordStateChange(args.Previous, args.Current);
            BreakerStates[breaker] = args.Current;
        });

        if (Subscriptions.TryAdd(breaker, handler))
            breaker.StateChanged += handler;
    }

    public static void RecordRetryAttempt() => RecordRetryAttempt(null);

    public static void RecordRetryAttempt(string? toolId)
    {
        Store.RecordRetryAttempt();
        if (!string.IsNullOrWhiteSpace(toolId))
            ToolStores.GetOrAdd(toolId, _ => new CircuitMetricsStore()).RecordRetryAttempt();
    }

    public static void RecordCircuitOpen(string? toolId)
    {
        if (string.IsNullOrWhiteSpace(toolId))
            return;
        ToolStores.GetOrAdd(toolId, _ => new CircuitMetricsStore()).RecordOpenEvent();
    }

    public static CircuitMetricsSnapshot Snapshot()
    {
        var snapshot = Store.Snapshot();
        History.Add(snapshot);
        Alerts.Evaluate(snapshot, History.Snapshot());
        return snapshot;
    }

    public static IReadOnlyDictionary<string, CircuitMetricsSnapshot> SnapshotByTool()
    {
        return ToolStores.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Snapshot(),
            StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<ResilienceMetricsSample> SnapshotHistory(TimeSpan? window = null, int? limit = null)
    {
        return History.Snapshot(window, limit);
    }

    public static bool IsCircuitOpen()
    {
        return BreakerStates.Values.Any(state => state == CircuitState.Open);
    }

    public static ResilienceAlertRule AddAlertRule(string name, int openThreshold, int retryThreshold, int windowMinutes)
    {
        return Alerts.AddRule(name, openThreshold, retryThreshold, windowMinutes);
    }

    public static IReadOnlyList<ResilienceAlertRule> ListAlertRules()
    {
        return Alerts.ListRules();
    }

    public static IReadOnlyList<ResilienceAlertEvent> ListAlertEvents(int limit = 50)
    {
        return Alerts.ListEvents(limit);
    }

    public static void Reset()
    {
        Store.Reset();
        ToolStores.Clear();
        History.Reset();
        Alerts.Reset();
        BreakerStates.Clear();
    }
}
