#nullable enable
using System.Threading.Tasks;
using RahOllamaOnly.Tools.Handlers;

namespace RahOllamaOnly.Tests.Tools.Handlers;

public sealed class MockPromptExecutor : IPromptExecutor
{
    public Task<ExecutionResult> ExecuteAsync(string prompt, ExecutionOptions options)
    {
        var result = new ExecutionResult(true, $"mock:{prompt}");
        return Task.FromResult(result);
    }
}
