#nullable enable
using System;

namespace RahOllamaOnly.Tests.Mocks;

public sealed class RecoveryProvider
{
    private readonly int _failuresBeforeSuccess;

    public int CallCount { get; private set; }

    public RecoveryProvider(int failuresBeforeSuccess = 1)
    {
        _failuresBeforeSuccess = Math.Max(0, failuresBeforeSuccess);
    }

    public void Invoke()
    {
        CallCount++;
        if (CallCount <= _failuresBeforeSuccess)
            throw new TimeoutException("Simulated transient failure.");
    }
}
