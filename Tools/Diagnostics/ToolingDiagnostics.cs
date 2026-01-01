#nullable enable
using System;
using System.Collections.Generic;

namespace RahOllamaOnly.Tools.Diagnostics;

public sealed record ToolingDiagnostics(
    int ToolCount,
    int PromptCount,
    int ActiveToolCount,
    IReadOnlyList<string> MissingPrompts,
    string State)
{
    public static ToolingDiagnostics Empty { get; } =
        new ToolingDiagnostics(0, 0, 0, Array.Empty<string>(), "tools inactive");
}
