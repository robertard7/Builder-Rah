#nullable enable
using System;

namespace RahOllamaOnly.Tools.Diagnostics;

public static class ToolingDiagnosticsHub
{
    private static ToolingDiagnostics _latest = ToolingDiagnostics.Empty;

    public static ToolingDiagnostics Latest => _latest;

    public static event Action<ToolingDiagnostics>? Updated;

    public static void Publish(ToolingDiagnostics diag)
    {
        _latest = diag ?? ToolingDiagnostics.Empty;
        Updated?.Invoke(_latest);
    }
}
