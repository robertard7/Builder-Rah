#nullable enable
using System;

namespace RahBuilder.Workflow.Provider;

public sealed class ProviderManager
{
    private ProviderState _state;
    private readonly Action<string>? _traceEmitter;

    public ProviderManager(ProviderState initialState, Action<string>? traceEmitter = null)
    {
        _state = initialState ?? throw new ArgumentNullException(nameof(initialState));
        _traceEmitter = traceEmitter;
    }

    public ProviderState State => _state;

    public event Action<ProviderState>? StateChanged;

    public bool IsEnabled => _state.Enabled;

    public bool IsReachable => _state.Reachable;

    public void UpdateEnabled(bool enabled)
    {
        if (_state.Enabled == enabled) return;
        _state = new ProviderState(enabled, _state.Reachable);
        var stamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var evt = enabled ? "ProviderEnabled" : "ProviderDisabled";
        Emit($"[provider] {evt} at {stamp}");
        StateChanged?.Invoke(_state);
    }

    public void MarkReachable(bool reachable, string? detail = null)
    {
        if (_state.Reachable == reachable) return;
        _state.Reachable = reachable;
        var stamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var evt = reachable ? "ProviderReachable" : "ProviderUnreachable";
        var message = string.IsNullOrWhiteSpace(detail) ? "" : $" ({detail})";
        Emit($"[provider] {evt} at {stamp}{message}");
        StateChanged?.Invoke(_state);
    }

    public void Retry(Action refreshTooling)
    {
        var stamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss");
        Emit($"[provider] ProviderRetryInitiated at {stamp}");
        refreshTooling?.Invoke();
        MarkReachable(true, "retry");
    }

    private void Emit(string message) => _traceEmitter?.Invoke(message);
}
