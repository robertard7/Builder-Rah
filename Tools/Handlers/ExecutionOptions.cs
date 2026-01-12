#nullable enable
using System;

namespace RahOllamaOnly.Tools.Handlers;

public sealed record ExecutionOptions(
    string? ToolId = null,
    string? Role = null,
    TimeSpan? Timeout = null,
    int MaxRetries = 0,
    bool ResilienceDebug = false,
    bool UseNoOp = false,
    System.Threading.CancellationToken CancellationToken = default);
