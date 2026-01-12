#nullable enable
using System.Collections.Generic;

namespace RahOllamaOnly.Tools.Handlers;

public sealed record ExecutionResult(
    bool Success,
    string Output,
    string? ErrorMessage = null,
    IReadOnlyDictionary<string, string?>? Metadata = null);
