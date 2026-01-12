#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using RahOllamaOnly.Tests.Tools.Handlers;
using RahOllamaOnly.Tools.Handlers;
using RahOllamaOnly.Tools.Prompts;
using Xunit;

namespace RahOllamaOnly.Tests.Tools;

public sealed class PromptRunnerTests
{
    [Fact]
    public async Task RunAsync_SkipsExecution_WhenResolutionFails()
    {
        var resolver = new PromptTemplateResolver();
        var runner = new PromptRunner(resolver);
        var executor = new MockPromptExecutor();
        var options = new ExecutionOptions();

        var result = await runner.RunAsync(
            "Hello {{name}}",
            new Dictionary<string, string?>(),
            new[] { "name" },
            executor,
            options);

        Assert.False(result.Executed);
        Assert.False(result.Resolution.Success);
        Assert.Contains("name", result.Resolution.MissingRequiredInputs);
    }

    [Fact]
    public async Task RunAsync_Executes_WhenResolutionSucceeds()
    {
        var resolver = new PromptTemplateResolver();
        var runner = new PromptRunner(resolver);
        var executor = new MockPromptExecutor();
        var options = new ExecutionOptions();

        var result = await runner.RunAsync(
            "Hello {{name}}",
            new Dictionary<string, string?> { ["name"] = "Rah" },
            new[] { "name" },
            executor,
            options);

        Assert.True(result.Executed);
        Assert.True(result.Resolution.Success);
        Assert.NotNull(result.ExecutionResult);
        Assert.Equal("mock:Hello Rah", result.ExecutionResult?.Output);
    }
}
