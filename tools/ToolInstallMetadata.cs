#nullable enable

namespace RahOllamaOnly.Tools;

public sealed class ToolInstallMetadata
{
    public string Method { get; init; } = string.Empty;
    public string Command { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public string? SourceUrl { get; init; }
}
