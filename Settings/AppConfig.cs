#nullable enable
using System;

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
    public string ExecutionTarget { get; set; } = OperatingSystem.IsWindows() ? "WindowsHost" : "LinuxContainer";

    // SAVE POINT: global digest prompt lives here (JSON-only, no tools)
    public string JobSpecDigestPrompt { get; set; } =
        @"You are the JobSpec Digest. Reply with exactly one JSON object that matches this shape:
{
  ""request"": ""<briefly restate the user's ask>"",
  ""state"": {
    ""ready"": true,
    ""missing"": []
  }
}
Rules:
- Output must be valid JSON (no prose, markdown, code fences, or commentary).
- The top-level object MUST contain state.ready (boolean) and state.missing (array of strings).
- Do not invent fields. Only include keys explicitly provided by the user plus the required state object.
- If any required information is missing or ambiguous, set state.ready=false and list the missing field names in state.missing.
- If everything is present, set state.ready=true and state.missing=[].
- Any non-JSON output is a prompt failure.";
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
