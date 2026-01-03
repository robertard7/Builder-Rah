#nullable enable
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace RahBuilder.Workflow;

public sealed class ProgramArtifactGeneratorTests
{
    [Fact]
    public void BuildTreePreview_FormatsHierarchy()
    {
        var files = new List<ProgramArtifact>
        {
            new("src/Controller.cs", "", new List<string>()),
            new("tests/TestAuth.cs", "", new List<string>()),
            new("README.md", "", new List<string>())
        };

        var tree = ProgramArtifactGenerator.BuildTreePreview(files);

        Assert.Contains("src/", tree);
        Assert.Contains("  Controller.cs", tree);
        Assert.Contains("tests/", tree);
        Assert.Contains("  TestAuth.cs", tree);
        Assert.Contains("README.md", tree);
    }

    [Fact]
    public void ExtractArtifacts_ReadsFilesArray()
    {
        const string json = @"{ ""files"": [ { ""path"": ""src/Auth.cs"", ""content"": ""class C {}"", ""tags"": [""code""] } ] }";
        using var doc = JsonDocument.Parse(json);
        var outputs = new List<JsonElement> { doc.RootElement };

        var artifacts = ProgramArtifactGenerator.ExtractArtifacts(outputs);

        Assert.Single(artifacts);
        Assert.Equal("src/Auth.cs", artifacts[0].Path);
        Assert.Contains("code", artifacts[0].Tags);
    }
}
