#nullable enable
using System;
using System.Collections.Generic;
using RahOllamaOnly.Tools.Prompts;
using Xunit;

namespace RahOllamaOnly.Tests.Tools;

public sealed class PromptTemplateResolverTests
{
    [Fact]
    public void Resolve_ReplacesTokens_WithInputs()
    {
        var resolver = new PromptTemplateResolver();
        var template = "Run {{command}} in {{directory}}.";
        var inputs = new Dictionary<string, string?>
        {
            ["command"] = "dotnet test",
            ["directory"] = "./src"
        };

        var result = resolver.Resolve(template, inputs, new[] { "command", "directory" });

        Assert.True(result.Success);
        Assert.Equal("Run dotnet test in ./src.", result.Prompt);
        Assert.Empty(result.MissingRequiredInputs);
        Assert.Empty(result.UnresolvedTokens);
        Assert.Null(result.FallbackMessage);
    }

    [Fact]
    public void Resolve_ReportsMissingRequiredInputs()
    {
        var resolver = new PromptTemplateResolver();
        var template = "Run {{command}}.";
        var inputs = new Dictionary<string, string?> { ["command"] = null };

        var result = resolver.Resolve(template, inputs, new[] { "command", "directory" });

        Assert.False(result.Success);
        Assert.Contains("command", result.UnresolvedTokens, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("command", result.MissingRequiredInputs, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("directory", result.MissingRequiredInputs, StringComparer.OrdinalIgnoreCase);
        Assert.NotNull(result.FallbackMessage);
    }

    [Fact]
    public void Resolve_TracksOptionalMissingTokens()
    {
        var resolver = new PromptTemplateResolver();
        var template = "Hello {{name}} from {{team}}.";
        var inputs = new Dictionary<string, string?> { ["name"] = "Rah" };

        var result = resolver.Resolve(template, inputs, new[] { "name" });

        Assert.False(result.Success);
        Assert.Contains("team", result.UnresolvedTokens, StringComparer.OrdinalIgnoreCase);
        Assert.Empty(result.MissingRequiredInputs);
        Assert.Null(result.FallbackMessage);
    }
}
