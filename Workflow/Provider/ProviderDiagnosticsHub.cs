#nullable enable
using System;
using System.Collections.Generic;

namespace RahBuilder.Workflow.Provider;

public static class ProviderDiagnosticsHub
{
    private const int MaxEvents = 100;
    private static readonly List<ProviderDiagnosticEvent> _events = new();
    private static ProviderMetricsSnapshot _latestMetrics = ProviderMetricsSnapshot.Empty;

    public static IReadOnlyList<ProviderDiagnosticEvent> Events => _events.AsReadOnly();

    public static ProviderMetricsSnapshot LatestMetrics => _latestMetrics;

    public static event Action<IReadOnlyList<ProviderDiagnosticEvent>>? EventsUpdated;

    public static event Action<ProviderMetricsSnapshot>? MetricsUpdated;

    public static event Action? MetricsResetRequested;

    public static void PublishEvent(ProviderDiagnosticEvent evt)
    {
        if (evt == null) return;
        _events.Add(evt);
        if (_events.Count > MaxEvents)
            _events.RemoveRange(0, _events.Count - MaxEvents);
        EventsUpdated?.Invoke(Events);
    }

    public static void PublishMetrics(ProviderMetricsSnapshot metrics)
    {
        _latestMetrics = metrics ?? ProviderMetricsSnapshot.Empty;
        MetricsUpdated?.Invoke(_latestMetrics);
    }

    public static void RequestMetricsReset() => MetricsResetRequested?.Invoke();

    public static void ResetEvents()
    {
        _events.Clear();
        EventsUpdated?.Invoke(Events);
    }
}
