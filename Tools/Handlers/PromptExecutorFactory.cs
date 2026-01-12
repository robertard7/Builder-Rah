#nullable enable
using System;
using RahBuilder.Settings;

namespace RahOllamaOnly.Tools.Handlers;

public sealed class PromptExecutorFactory
{
    private readonly AppConfig _cfg;
    private readonly string _defaultRole;

    public PromptExecutorFactory(AppConfig cfg, string defaultRole = "Orchestrator")
    {
        _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
        _defaultRole = string.IsNullOrWhiteSpace(defaultRole) ? "Orchestrator" : defaultRole;
    }

    public IPromptExecutor Create(ExecutionOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        if (options.UseNoOp)
            return new NoOpPromptExecutor();

        return new LLMPromptExecutor(_cfg, _defaultRole);
    }
}
