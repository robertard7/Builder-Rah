#nullable enable
using System;
using System.Collections.Generic;

namespace RahBuilder.Tests.Helpers;

public sealed class FaultInjectionProvider
{
    private readonly Queue<bool> _pattern;
    private readonly bool _defaultValue;

    public FaultInjectionProvider(IEnumerable<bool> pattern, bool defaultValue = true)
    {
        _pattern = new Queue<bool>(pattern ?? Array.Empty<bool>());
        _defaultValue = defaultValue;
    }

    public bool Next()
    {
        if (_pattern.Count == 0)
            return _defaultValue;
        return _pattern.Dequeue();
    }
}
