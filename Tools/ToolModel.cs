#nullable enable
using System.Collections.Generic;

namespace RahOllamaOnly.Tools;

public sealed class ToolModel
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<string> Categories { get; init; } = new List<string>();
    public string Os { get; init; } = string.Empty;
    public ToolInstallMeta Install { get; init; } = new();
    public IReadOnlyList<string> Tags { get; init; } = new List<string>();
}

public sealed class ToolInstallMeta
{
    public string Method { get; init; } = string.Empty;
    public string Command { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public string? Url { get; init; }
}
