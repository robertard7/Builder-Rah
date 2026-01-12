#nullable enable
using System;

namespace RahOllamaOnly.Tools.Handlers;

public sealed class CircuitBreaker
{
    private readonly object _lock = new();
    private readonly int _failureThreshold;
    private readonly TimeSpan _breakDuration;
    private readonly Func<DateTimeOffset> _timeProvider;
    private int _failureCount;
    private DateTimeOffset? _openedUntil;
    private CircuitState _state = CircuitState.Closed;
    private bool _halfOpenInFlight;

    public event EventHandler<CircuitBreakerStateChangedEventArgs>? StateChanged;

    public CircuitBreaker(int failureThreshold = 3, TimeSpan? breakDuration = null, Func<DateTimeOffset>? timeProvider = null)
    {
        _failureThreshold = Math.Max(1, failureThreshold);
        _breakDuration = breakDuration ?? TimeSpan.FromSeconds(30);
        _timeProvider = timeProvider ?? (() => DateTimeOffset.UtcNow);
    }

    public bool CanExecute()
    {
        lock (_lock)
        {
            if (_state == CircuitState.Closed)
                return true;

            if (_state == CircuitState.Open)
            {
                if (_openedUntil != null && _openedUntil <= _timeProvider())
                    TransitionTo(CircuitState.HalfOpen);
                else
                    return false;
            }

            if (_state == CircuitState.HalfOpen)
            {
                if (_halfOpenInFlight)
                    return false;

                _halfOpenInFlight = true;
                return true;
            }

            return false;
        }
    }

    public void RecordSuccess()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _openedUntil = null;
            _halfOpenInFlight = false;
            TransitionTo(CircuitState.Closed);
        }
    }

    public void RecordFailure()
    {
        lock (_lock)
        {
            _halfOpenInFlight = false;
            if (_state == CircuitState.HalfOpen)
            {
                OpenCircuit();
                return;
            }

            _failureCount++;
            if (_failureCount >= _failureThreshold)
                OpenCircuit();
        }
    }

    private void OpenCircuit()
    {
        _openedUntil = _timeProvider().Add(_breakDuration);
        TransitionTo(CircuitState.Open);
    }

    private void TransitionTo(CircuitState next)
    {
        if (_state == next)
            return;

        var previous = _state;
        _state = next;
        StateChanged?.Invoke(this, new CircuitBreakerStateChangedEventArgs(previous, next, _timeProvider()));
    }
}
