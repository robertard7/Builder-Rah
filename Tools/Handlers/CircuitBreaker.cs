#nullable enable
using System;

namespace RahOllamaOnly.Tools.Handlers;

public sealed class CircuitBreaker
{
    private readonly object _lock = new();
    private readonly int _failureThreshold;
    private readonly TimeSpan _breakDuration;
    private int _failureCount;
    private DateTimeOffset? _openedUntil;

    public CircuitBreaker(int failureThreshold = 3, TimeSpan? breakDuration = null)
    {
        _failureThreshold = Math.Max(1, failureThreshold);
        _breakDuration = breakDuration ?? TimeSpan.FromSeconds(30);
    }

    public bool CanExecute()
    {
        lock (_lock)
        {
            if (_openedUntil == null)
                return true;

            if (_openedUntil <= DateTimeOffset.UtcNow)
            {
                _openedUntil = null;
                _failureCount = 0;
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
        }
    }

    public void RecordFailure()
    {
        lock (_lock)
        {
            _failureCount++;
            if (_failureCount >= _failureThreshold)
                _openedUntil = DateTimeOffset.UtcNow.Add(_breakDuration);
        }
    }
}
