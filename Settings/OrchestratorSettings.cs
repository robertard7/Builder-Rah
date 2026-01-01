#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace RahBuilder.Settings;

public sealed class OrchestratorSettings
{
    // "single" | "multi"
    public string Mode { get; set; } = "single";

    public bool EnableOpenAiPool { get; set; } = true;
    public bool EnableHuggingFacePool { get; set; } = true;
    public bool EnableOllamaPool { get; set; } = true;

    // Per-role wiring: provider + model + blueprint prompt id
    public List<OrchestratorRoleConfig> Roles { get; set; } = new();

    public void EnsureDefaults()
    {
        Roles ??= new List<OrchestratorRoleConfig>();

        EnsureRole("Orchestrator");
        EnsureRole("Planner");
        EnsureRole("Executor");
        EnsureRole("Repair");
        EnsureRole("Embed");

        var order = new[] { "Orchestrator", "Planner", "Executor", "Repair", "Embed" };
        Roles = Roles
            .OrderBy(r => Array.IndexOf(order, r.Role ?? ""))
            .ThenBy(r => r.Role, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void EnsureRole(string role)
    {
        if (Roles.Any(r => string.Equals(r.Role, role, StringComparison.OrdinalIgnoreCase)))
            return;

        Roles.Add(new OrchestratorRoleConfig
        {
            Role = role,
            Provider = "Ollama",
            Model = "",
            PromptId = "default"
        });
    }
}
