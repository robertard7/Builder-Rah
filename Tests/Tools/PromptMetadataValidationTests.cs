#nullable enable
using System;
using RahOllamaOnly.Tools.Prompts;
using Xunit;

namespace RahOllamaOnly.Tests.Tools;

public sealed class PromptMetadataValidationTests
{
    [Fact]
    public void EnsureValid_Throws_WhenMetadataIsNull()
    {
        var exception = Assert.ThrowsAny<Exception>(() => PromptMetadataValidator.EnsureValid(null!));

        Assert.Contains("Prompt metadata is null.", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureValid_AllowsValidMetadata()
    {
        var metadata = new PromptMetadata
        {
            Version = 1,
            Description = "Valid",
            Categories = new[] { "CLI" },
            RequiredFields = new[] { "version", "description", "tags", "category" },
            TagHints = new[] { "usageExamples" }
        };

        PromptMetadataValidator.EnsureValid(metadata);
    }

    [Fact]
    public void EnsureValid_RequiresTagHints()
    {
        var metadata = new PromptMetadata
        {
            Version = 1,
            Description = "Valid",
            Categories = new[] { "CLI" },
            RequiredFields = new[] { "version", "description", "tags", "category" },
            TagHints = Array.Empty<string>()
        };

        var exception = Assert.ThrowsAny<Exception>(() => PromptMetadataValidator.EnsureValid(metadata));

        Assert.Contains("tagHints must include at least one entry", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoadDefault_ReturnsSingletonInstance()
    {
        var first = PromptMetadataStore.LoadDefault();
        var second = PromptMetadataStore.LoadDefault();

        Assert.Same(first, second);
        Assert.Same(first, PromptMetadataStore.Default);
    }
}
