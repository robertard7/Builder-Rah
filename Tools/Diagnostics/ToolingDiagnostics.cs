#nullable enable
using System;
using System.Collections.Generic;

namespace RahOllamaOnly.Tools.Diagnostics;

public sealed record ToolingDiagnostics(
    int ToolCount,
    int PromptCount,
    int ActiveToolCount,
    IReadOnlyList<string> MissingPrompts,
    string State,
    int BlueprintTotal,
    int BlueprintSelectable,
    IReadOnlyList<string> SelectedBlueprints)
{
    public static ToolingDiagnostics Empty { get; } =
        new ToolingDiagnostics(0, 0, 0, Array.Empty<string>(), "tools inactive", 0, 0, Array.Empty<string>());
}
