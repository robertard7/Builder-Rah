#nullable enable
using System;

namespace RahOllamaOnly.Tools.Handlers;

public sealed record CircuitBreakerStateChangedEventArgs(
    CircuitState Previous,
    CircuitState Current,
    DateTimeOffset Timestamp);
