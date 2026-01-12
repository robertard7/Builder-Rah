#nullable enable
using System;
using System.Threading.Tasks;

namespace RahOllamaOnly.Tools.Handlers;

public sealed class NoOpPromptExecutor : IPromptExecutor
{
    public Task<ExecutionResult> ExecuteAsync(string prompt, ExecutionOptions options)
    {
        if (prompt == null)
            throw new ArgumentNullException(nameof(prompt));

        var result = new ExecutionResult(true, "Prompt execution skipped.");
        return Task.FromResult(result);
    }
}
