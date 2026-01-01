#nullable enable
namespace RahBuilder.Settings;

public sealed class AppConfig
{
    public GeneralSettings General { get; set; } = new();
    public ProvidersSettings Providers { get; set; } = new();
    public OrchestratorSettings Orchestrator { get; set; } = new();
    public WorkflowGraphSettings WorkflowGraph { get; set; } = new();
    public ToolchainSettings Toolchain { get; set; } = new();

    // placeholders (must not break build)
    public UsageSettings Usage { get; set; } = new();
    public MemorySettings Memory { get; set; } = new();
}

public sealed class UsageSettings { }
public sealed class MemorySettings { }

public sealed class GeneralSettings
{
    public string RepoRoot { get; set; } = "";
    public string SandboxHostPath { get; set; } = "";
    public string SandboxContainerPath { get; set; } = "";

    public string ToolsPath { get; set; } = "";                 // tools.json
    public string ToolPromptsPath { get; set; } = "";           // folder: Tools/Prompt (files named by toolId)
    public string BlueprintTemplatesPath { get; set; } = "";    // folder: BlueprintTemplates (prompt library)

    public bool GraphDriven { get; set; } = true;
    public bool ContainerOnly { get; set; } = true;
    public bool EnableGlobalClipboardShortcuts { get; set; } = true;

    // SAVE POINT: global digest prompt lives here (JSON-only, no tools)
    public string JobSpecDigestPrompt { get; set; } =
        @"You are the JobSpec Digest. Convert the user request into a single JSON object that matches this exact shape:
{
  ""request"": ""<summarize the user's ask without adding details>"",
  ""state"": {
    ""ready"": false,
    ""missing"": [""field"", ""field""]
  }
}
Rules:
- Emit only the JSON object. No prose, no markdown, no code fences, no commentary.
- Do not invent extra fields beyond request and state.
- Keep request concise and factual, paraphrasing the user's ask without guessing details.
- If any required information is missing or ambiguous, set state.ready=false and list every missing field name in state.missing.
- When everything is present, set state.ready=true and state.missing=[].
- Any output that is not valid JSON is a failure of this prompt.";
}

public sealed class ProvidersSettings
{
    public OpenAiSettings OpenAI { get; set; } = new();
    public HuggingFaceSettings HuggingFace { get; set; } = new();
    public OllamaSettings Ollama { get; set; } = new();
}

public sealed class OpenAiSettings
{
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string ApiKey { get; set; } = "";
    public string Organization { get; set; } = "";
    public string Project { get; set; } = "";
}

public sealed class HuggingFaceSettings
{
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "https://api-inference.huggingface.co";
    public string ApiKey { get; set; } = "";
}

public sealed class OllamaSettings
{
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "http://localhost:11434";
}

public sealed class WorkflowGraphSettings
{
    public string MermaidText { get; set; } = "";
}
