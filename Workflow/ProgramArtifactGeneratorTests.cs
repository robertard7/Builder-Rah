#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RahBuilder.Settings;
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

    [Fact]
    public async Task BuildProjectArtifacts_UsesCache()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "rah-artifacts-" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempRoot);

        var doc = JsonDocument.Parse(@"{""mode"":""jobspec.v2"",""request"":""test"",""goal"":""goal"",""context"":"""",""actions"":[""build""],""constraints"":[],""attachments"":[],""state"":{""ready"":true,""missing"":[]}}");
        var spec = JobSpec.FromJson(doc, "jobspec.v2", "test", "goal", "", new List<string> { "build" }, new List<string>(), new List<JobSpecAttachment>(), null, true, new List<string>());

        const string json = @"{ ""files"": [ { ""path"": ""src/Auth.cs"", ""content"": ""class C {}"", ""tags"": [""code""] } ] }";
        using var outputDoc = JsonDocument.Parse(json);
        var outputs = new List<JsonElement> { outputDoc.RootElement };

        var gen = new ProgramArtifactGenerator();
        var cfg = new AppConfig();

        var result = await gen.BuildProjectArtifacts(spec, outputs, cfg, new List<AttachmentInbox.AttachmentEntry>(), tempRoot, "session", CancellationToken.None);
        Assert.True(result.Ok);
        Assert.True(File.Exists(result.ZipPath));

        var cached = gen.TryGetCachedArtifacts(tempRoot, result.Hash);
        Assert.NotNull(cached);
        Assert.Equal(result.Hash, cached!.Hash);
    }
}
