#nullable enable
using System;

namespace RahOllamaOnly.Tools;

public sealed class ProviderDisabledException : InvalidOperationException
{
    public ProviderDisabledException(string message) : base(message) { }
}

public sealed class ProviderUnavailableException : InvalidOperationException
{
    public ProviderUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}
