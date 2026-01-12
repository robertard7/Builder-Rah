#nullable enable
using System;

namespace RahOllamaOnly.Tools.Handlers;

public sealed record ExecutionOptions(
    string? ToolId = null,
    TimeSpan? Timeout = null,
    int MaxRetries = 0);
