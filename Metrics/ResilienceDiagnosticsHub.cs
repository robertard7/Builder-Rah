#nullable enable
using System;
using System.Collections.Concurrent;
using RahOllamaOnly.Tools.Handlers;

namespace RahOllamaOnly.Metrics;

public static class ResilienceDiagnosticsHub
{
    private static readonly CircuitMetricsStore Store = new();
    private static readonly ConcurrentDictionary<CircuitBreaker, EventHandler<CircuitBreakerStateChangedEventArgs>> Subscriptions = new();

    public static void Attach(CircuitBreaker breaker)
    {
        if (breaker == null)
            throw new ArgumentNullException(nameof(breaker));

        var handler = new EventHandler<CircuitBreakerStateChangedEventArgs>((_, args) =>
            Store.RecordStateChange(args.Previous, args.Current));

        if (Subscriptions.TryAdd(breaker, handler))
            breaker.StateChanged += handler;
    }

    public static void RecordRetryAttempt() => Store.RecordRetryAttempt();

    public static CircuitMetricsSnapshot Snapshot() => Store.Snapshot();
}
