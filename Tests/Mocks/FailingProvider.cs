#nullable enable
using System;

namespace RahOllamaOnly.Tests.Mocks;

public sealed class FailingProvider
{
    public int CallCount { get; private set; }

    public void Invoke()
    {
        CallCount++;
        throw new TimeoutException("Simulated transient failure.");
    }
}
