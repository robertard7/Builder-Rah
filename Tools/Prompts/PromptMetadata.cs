#nullable enable
using System.Collections.Generic;

namespace RahOllamaOnly.Tools.Prompts;

public sealed class PromptMetadata
{
    public int Version { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public IReadOnlyList<string> Categories { get; init; } = new List<string>();
    public IReadOnlyList<string> RequiredFields { get; init; } = new List<string>();
    public IReadOnlyList<string> TagHints { get; init; } = new List<string>();
}
