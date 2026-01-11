#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RahBuilder.Workflow.Provider;

public sealed class ProviderManager
{
    private static readonly TimeSpan[] RetryBackoffSchedule =
    {
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromMinutes(1)
    };

    private readonly Func<DateTimeOffset> _now;
    private readonly ProviderMetricsTracker _metrics;
    private readonly Action<string>? _traceEmitter;
    private readonly IProviderTelemetrySink? _telemetrySink;
    private ProviderState _state;
    private Func<CancellationToken, Task<bool>>? _retryHandler;
    private CancellationTokenSource? _retryCts;
    private readonly TimeSpan _staleAfter = TimeSpan.FromMinutes(5);

    public ProviderManager(
        ProviderState initialState,
        Action<string>? traceEmitter = null,
        Func<DateTimeOffset>? nowProvider = null,
        IProviderTelemetrySink? telemetrySink = null)
    {
        _state = initialState ?? throw new ArgumentNullException(nameof(initialState));
        _traceEmitter = traceEmitter;
        _telemetrySink = telemetrySink;
        _now = nowProvider ?? (() => DateTimeOffset.UtcNow);
        _metrics = new ProviderMetricsTracker(_state.Enabled, _state.Reachable, _now());
        ProviderDiagnosticsHub.PublishMetrics(_metrics.Snapshot(_state, _now(), _staleAfter));
        ProviderDiagnosticsHub.MetricsResetRequested += ResetMetrics;
    }

    public ProviderState State => _state;

    public event Action<ProviderState>? StateChanged;

    public static IReadOnlyList<TimeSpan> RetryBackoff => RetryBackoffSchedule;

    public bool IsEnabled => _state.Enabled;

    public bool IsReachable => _state.Reachable;

    public ProviderMetricsSnapshot GetMetricsSnapshot() => _metrics.Snapshot(_state, _now(), _staleAfter);

    public void ConfigureRetryHandler(Func<CancellationToken, Task<bool>> retryHandler)
    {
        _retryHandler = retryHandler;
    }

    public void UpdateEnabled(bool enabled)
    {
        if (_state.Enabled == enabled) return;
        _state = new ProviderState(enabled, _state.Reachable);
        var stamp = _now();
        _metrics.UpdateEnabled(enabled, stamp);
        var evtType = enabled ? ProviderEventType.Enabled : ProviderEventType.Disabled;
        EmitDiagnostic(evtType, enabled ? "Provider enabled" : "Provider disabled", new Dictionary<string, object>
        {
            ["enabled"] = enabled
        });
        PublishMetrics();
        StateChanged?.Invoke(_state);
    }

    public void MarkReachable(bool reachable, string? detail = null)
    {
        if (_state.Reachable == reachable) return;
        _state.Reachable = reachable;
        var stamp = _now();
        _metrics.UpdateReachable(reachable, stamp);
        if (!reachable && _state.Enabled)
        {
            EmitDiagnostic(ProviderEventType.OfflineDetected, "Provider offline detected", new Dictionary<string, object>
            {
                ["reachable"] = reachable,
                ["detail"] = detail ?? ""
            });
            StartRetryLoop();
        }
        PublishMetrics();
        StateChanged?.Invoke(_state);
    }

    public async Task RetryAsync(Func<Task> refreshTooling, CancellationToken ct = default)
    {
        _metrics.RecordRetryAttempt(_now());
        EmitDiagnostic(ProviderEventType.RetryAttempt, "Provider retry requested", null);
        PublishMetrics();

        var success = await TryInvokeRetryAsync(refreshTooling, ct).ConfigureAwait(false);
        if (success)
        {
            _metrics.RecordRetrySuccess(_now());
            EmitDiagnostic(ProviderEventType.RetrySuccess, "Provider retry succeeded", null);
            MarkReachable(true, "retry");
        }
        else
        {
            _metrics.RecordRetryFailure(_now());
            EmitDiagnostic(ProviderEventType.RetryFail, "Provider retry failed", null);
        }
        PublishMetrics();
    }

    public void RecordSuccess(string provider)
    {
        var stamp = _now();
        _metrics.RecordSuccess(stamp);
        PublishMetrics();
    }

    public void ResetMetrics()
    {
        _metrics.Reset(_state.Enabled, _state.Reachable, _now());
        PublishMetrics();
    }

    private void PublishMetrics()
    {
        var snapshot = _metrics.Snapshot(_state, _now(), _staleAfter);
        ProviderDiagnosticsHub.PublishMetrics(snapshot);
        _telemetrySink?.RecordMetrics(snapshot);
    }

    private void EmitDiagnostic(ProviderEventType eventType, string message, IReadOnlyDictionary<string, object>? metadata)
    {
        var evt = new ProviderDiagnosticEvent(eventType, _now(), message, metadata);
        ProviderDiagnosticsHub.PublishEvent(evt);
        _telemetrySink?.RecordEvent(evt);
        if (!string.IsNullOrWhiteSpace(message))
            _traceEmitter?.Invoke($"[provider] {message}");
    }

    private void StartRetryLoop()
    {
        if (_retryHandler == null) return;
        _retryCts?.Cancel();
        _retryCts = new CancellationTokenSource();
        _ = Task.Run(() => RetryLoopAsync(_retryCts.Token));
    }

    private async Task RetryLoopAsync(CancellationToken ct)
    {
        foreach (var delay in RetryBackoffSchedule)
        {
            if (ct.IsCancellationRequested || _state.Reachable)
                return;
            try
            {
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (ct.IsCancellationRequested || _state.Reachable)
                return;

            _metrics.RecordRetryAttempt(_now());
            EmitDiagnostic(ProviderEventType.RetryAttempt, "Provider retry attempt", new Dictionary<string, object>
            {
                ["delaySeconds"] = delay.TotalSeconds
            });
            PublishMetrics();

            var success = false;
            try
            {
                success = await _retryHandler!.Invoke(ct).ConfigureAwait(false);
            }
            catch
            {
                success = false;
            }

            if (success)
            {
                _metrics.RecordRetrySuccess(_now());
                EmitDiagnostic(ProviderEventType.RetrySuccess, "Provider retry succeeded", null);
                MarkReachable(true, "retry");
                PublishMetrics();
                return;
            }

            _metrics.RecordRetryFailure(_now());
            EmitDiagnostic(ProviderEventType.RetryFail, "Provider retry failed", null);
            PublishMetrics();
        }
    }

    private static async Task<bool> TryInvokeRetryAsync(Func<Task> refreshTooling, CancellationToken ct)
    {
        try
        {
            if (refreshTooling != null)
                await refreshTooling().ConfigureAwait(false);
            return !ct.IsCancellationRequested;
        }
        catch
        {
            return false;
        }
    }
}
