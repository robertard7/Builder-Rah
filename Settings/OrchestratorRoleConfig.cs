#nullable enable
namespace RahBuilder.Settings;

/// <summary>
/// Per-role wiring (provider + model + blueprint template selection + role purpose).
/// </summary>
public sealed class OrchestratorRoleConfig
{
    public string Role { get; set; } = "";              // e.g. Orchestrator / Planner / Executor / Repair / Embed
    public string Provider { get; set; } = "";          // e.g. OpenAI / HuggingFace / Ollama
    public string Model { get; set; } = "";             // provider-specific model id

    // BlueprintTemplates template id (must come from BlueprintTemplates/manifest.json)
    public string PromptId { get; set; } = "default";

    // Free-text role purpose (used to describe what this role does, for intent matching/ranking).
    // This is NOT a tool prompt and NOT a blueprint template id.
    public string RolePurpose { get; set; } = "";
}
