// Settings/OrchestratorRoleConfig.cs
#nullable enable
namespace RahBuilder.Settings;

/// <summary>
/// Per-role wiring (provider + model + optional inline prompt + role purpose).
/// </summary>
public sealed class OrchestratorRoleConfig
{
    public string Role { get; set; } = "";       // chat/router/planner/executor/reviewer/embed/vision/...
    public string Provider { get; set; } = "";   // OpenAI / HuggingFace / Ollama
    public string Model { get; set; } = "";      // provider-specific model id

    // Legacy support (blueprint id) - NOT shown in UI anymore.
    public string PromptId { get; set; } = "default";

    // NEW: inline prompt text you can type/paste directly.
    public string PromptText { get; set; } = "";

    // Free-text role purpose for humans (and later for behavior shaping).
    public string RolePurpose { get; set; } = "";
}
