#nullable enable
using System.Collections.Generic;

namespace RahOllamaOnly.Tools;

public sealed class ToolModel
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<string> Categories { get; init; } = new List<string>();
    public string Os { get; init; } = string.Empty;
    public ToolInstallMetadata Install { get; init; } = new();
    public IReadOnlyList<string> Tags { get; init; } = new List<string>();
}
