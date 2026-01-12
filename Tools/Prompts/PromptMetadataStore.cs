#nullable enable
using System;
using System.IO;
using System.Text.Json;

namespace RahOllamaOnly.Tools.Prompts;

public sealed class PromptMetadataStore
{
    private static readonly Lazy<PromptMetadataStore> DefaultStore =
        new(() => LoadFromFile(Path.Combine("tools", "prompts_metadata.json")));

    public static PromptMetadataStore Default => DefaultStore.Value;

    public PromptMetadata Metadata { get; }

    private PromptMetadataStore(PromptMetadata metadata)
    {
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
    }

    public static PromptMetadataStore LoadDefault() => Default;

    public static PromptMetadataStore LoadFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Prompt metadata path is required.", nameof(path));
        if (!File.Exists(path))
            throw new FileNotFoundException($"Prompt metadata file not found: {path}");

        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var metadata = JsonSerializer.Deserialize<PromptMetadata>(json, options);
        if (metadata == null)
            throw new InvalidDataException("Prompt metadata could not be parsed.");

        PromptMetadataValidator.EnsureValid(metadata);
        return new PromptMetadataStore(metadata);
    }
}
