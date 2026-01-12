#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RahOllamaOnly.Tools.Handlers;

namespace RahOllamaOnly.Tools.Prompts;

public sealed class PromptRunner
{
    private readonly PromptTemplateResolver _resolver;

    public PromptRunner(PromptTemplateResolver resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public async Task<PromptRunResult> RunAsync(
        string template,
        IReadOnlyDictionary<string, string?> inputs,
        IReadOnlyCollection<string>? requiredInputs,
        IPromptExecutor executor,
        ExecutionOptions options)
    {
        if (executor == null)
            throw new ArgumentNullException(nameof(executor));
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        var resolution = _resolver.Resolve(template, inputs, requiredInputs);
        if (!resolution.Success)
            return new PromptRunResult(resolution, null);

        var execution = await executor.ExecuteAsync(resolution.Prompt, options).ConfigureAwait(false);
        return new PromptRunResult(resolution, execution);
    }

    public Task<PromptRunResult> RunAsync(
        string template,
        IReadOnlyDictionary<string, string?> inputs,
        IReadOnlyCollection<string>? requiredInputs,
        PromptExecutorFactory executorFactory,
        ExecutionOptions options)
    {
        if (executorFactory == null)
            throw new ArgumentNullException(nameof(executorFactory));

        var executor = executorFactory.Create(options);
        return RunAsync(template, inputs, requiredInputs, executor, options);
    }
}

public sealed record PromptRunResult(
    PromptResolutionResult Resolution,
    ExecutionResult? ExecutionResult)
{
    public bool Executed => ExecutionResult != null;
}
