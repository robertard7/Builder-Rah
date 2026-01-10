// Settings/OrchestratorSettings.cs
#nullable enable
using System;
using System.Collections.Generic;

namespace RahBuilder.Settings;

public sealed class OrchestratorSettings
{
    public string Mode { get; set; } = "single";

    public bool EnableOpenAiPool { get; set; }
    public bool EnableHuggingFacePool { get; set; }
    public bool EnableOllamaPool { get; set; } = true;

    public List<OrchestratorRoleConfig> Roles { get; set; } = new();

    public void EnsureDefaults()
    {
        Roles ??= new List<OrchestratorRoleConfig>();

        if (Roles.Count == 0)
        {
            Roles.AddRange(new[]
            {
                new OrchestratorRoleConfig { Role = "chat",     Provider = "Ollama", Model = "qwen2.5:7b-instruct" },
                new OrchestratorRoleConfig { Role = "router",   Provider = "Ollama", Model = "qwen2.5:7b-instruct" },
                new OrchestratorRoleConfig { Role = "planner",  Provider = "Ollama", Model = "qwen2.5:7b-instruct" },
                new OrchestratorRoleConfig { Role = "executor", Provider = "Ollama", Model = "qwen2.5:7b-instruct" },
                new OrchestratorRoleConfig { Role = "reviewer", Provider = "Ollama", Model = "qwen2.5:7b-instruct" },
                new OrchestratorRoleConfig { Role = "embed",    Provider = "Ollama", Model = "nomic-embed-text:latest" },
                new OrchestratorRoleConfig { Role = "vision",   Provider = "Ollama", Model = "moondream:latest" },
            });
        }

        foreach (var r in Roles)
        {
            r.Role ??= "";
            r.Provider ??= "";
            r.Model ??= "";

            // legacy support
            r.PromptId ??= "default";

            // what you actually use now
            r.PromptText ??= "";
            r.RolePurpose ??= "";
        }

        Mode ??= "single";
        if (string.IsNullOrWhiteSpace(Mode)) Mode = "single";
    }
}
