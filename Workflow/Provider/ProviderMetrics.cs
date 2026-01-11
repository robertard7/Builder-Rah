#nullable enable
using System;

namespace RahBuilder.Workflow.Provider;

public sealed record ProviderMetricsSnapshot(
    bool Enabled,
    bool Reachable,
    bool IsStale,
    double TotalUptimeSeconds,
    double TotalDowntimeSeconds,
    int RetryAttempts,
    int RetrySuccesses,
    int RetryFailures,
    int EnableTransitions,
    int DisableTransitions,
    DateTimeOffset? LastEvent,
    DateTimeOffset? LastSuccess,
    double? StaleForSeconds)
{
    public static ProviderMetricsSnapshot Empty { get; } =
        new(false, false, false, 0, 0, 0, 0, 0, 0, 0, null, null, null);
}

public sealed class ProviderMetricsTracker
{
    private TimeSpan _totalUptime = TimeSpan.Zero;
    private TimeSpan _totalDowntime = TimeSpan.Zero;
    private DateTimeOffset _lastStateChange;
    private DateTimeOffset? _lastEvent;
    private DateTimeOffset? _lastSuccess;
    private bool _enabled;
    private bool _reachable;
    private int _retryAttempts;
    private int _retrySuccesses;
    private int _retryFailures;
    private int _enableTransitions;
    private int _disableTransitions;

    public ProviderMetricsTracker(bool enabled, bool reachable, DateTimeOffset now)
    {
        _enabled = enabled;
        _reachable = reachable;
        _lastStateChange = now;
        _lastEvent = now;
        _lastSuccess = now;
    }

    public void Reset(bool enabled, bool reachable, DateTimeOffset now)
    {
        _totalUptime = TimeSpan.Zero;
        _totalDowntime = TimeSpan.Zero;
        _retryAttempts = 0;
        _retrySuccesses = 0;
        _retryFailures = 0;
        _enableTransitions = 0;
        _disableTransitions = 0;
        _enabled = enabled;
        _reachable = reachable;
        _lastStateChange = now;
        _lastEvent = now;
        _lastSuccess = now;
    }

    public void UpdateEnabled(bool enabled, DateTimeOffset now)
    {
        AccumulateDurations(now);
        if (_enabled != enabled)
        {
            if (enabled)
                _enableTransitions++;
            else
                _disableTransitions++;
        }
        _enabled = enabled;
        _lastStateChange = now;
        _lastEvent = now;
    }

    public void UpdateReachable(bool reachable, DateTimeOffset now)
    {
        AccumulateDurations(now);
        _reachable = reachable;
        _lastStateChange = now;
        _lastEvent = now;
    }

    public void RecordRetryAttempt(DateTimeOffset now)
    {
        _retryAttempts++;
        _lastEvent = now;
    }

    public void RecordRetrySuccess(DateTimeOffset now)
    {
        _retrySuccesses++;
        _lastEvent = now;
    }

    public void RecordRetryFailure(DateTimeOffset now)
    {
        _retryFailures++;
        _lastEvent = now;
    }

    public void RecordSuccess(DateTimeOffset now)
    {
        _lastSuccess = now;
        _lastEvent = now;
    }

    public ProviderMetricsSnapshot Snapshot(ProviderState state, DateTimeOffset now, TimeSpan staleAfter)
    {
        var (uptime, downtime) = ComputeTotals(now);
        var lastSuccess = _lastSuccess;
        var isStale = false;
        double? staleFor = null;
        if (state.Enabled && state.Reachable && lastSuccess.HasValue)
        {
            var span = now - lastSuccess.Value;
            if (span > staleAfter)
            {
                isStale = true;
                staleFor = span.TotalSeconds;
            }
        }

        return new ProviderMetricsSnapshot(
            state.Enabled,
            state.Reachable,
            isStale,
            uptime.TotalSeconds,
            downtime.TotalSeconds,
            _retryAttempts,
            _retrySuccesses,
            _retryFailures,
            _enableTransitions,
            _disableTransitions,
            _lastEvent,
            _lastSuccess,
            staleFor);
    }

    private void AccumulateDurations(DateTimeOffset now)
    {
        var delta = now - _lastStateChange;
        if (_enabled)
        {
            if (_reachable)
                _totalUptime += delta;
            else
                _totalDowntime += delta;
        }
        else
        {
            _totalDowntime += delta;
        }
    }

    private (TimeSpan Uptime, TimeSpan Downtime) ComputeTotals(DateTimeOffset now)
    {
        var uptime = _totalUptime;
        var downtime = _totalDowntime;
        var delta = now - _lastStateChange;
        if (_enabled)
        {
            if (_reachable)
                uptime += delta;
            else
                downtime += delta;
        }
        else
        {
            downtime += delta;
        }

        return (uptime, downtime);
    }
}
