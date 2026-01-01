#nullable enable
using System;
using System.IO;

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
    public bool TweakFirstMode { get; set; } = true;
    public string SandboxHostPath { get; set; } = "";
    public string SandboxContainerPath { get; set; } = "";
    public string InboxHostPath { get; set; } = DefaultInboxPath();

    public string ToolsPath { get; set; } = "";                 // tools.json
    public string ToolPromptsPath { get; set; } = "";           // folder: Tools/Prompt (files named by toolId)
    public string BlueprintTemplatesPath { get; set; } = "";    // folder: BlueprintTemplates (prompt library)
    public string ToolPlanPrompt { get; set; } = DefaultToolPlanPrompt();
    public string FinalAnswerPrompt { get; set; } = DefaultFinalAnswerPrompt();
    public string AcceptedAttachmentExtensions { get; set; } = ".txt,.md,.json,.png,.jpg,.jpeg,.pdf,.zip";
    public long MaxAttachmentBytes { get; set; } = 50 * 1024 * 1024;
    public long MaxTotalInboxBytes { get; set; } = 500 * 1024 * 1024;

    public bool GraphDriven { get; set; } = true;
    public bool ContainerOnly { get; set; } = true;
    public bool EnableGlobalClipboardShortcuts { get; set; } = true;
    public string ExecutionTarget { get; set; } = OperatingSystem.IsWindows() ? "WindowsHost" : "LinuxContainer";

    // SAVE POINT: global digest prompt lives here (JSON-only, no tools)
    public string JobSpecDigestPrompt { get; set; } =
        @"You are the JobSpec Digest. Respond with exactly one JSON object and nothing else.
Output shape (no code fences, no prose):
{
  ""request"": ""<brief restatement of the user's ask>"",
  ""state"": {
    ""ready"": <true|false>,
    ""missing"": [""field1"", ""field2""]
  }
}
Rules:
- Emit ONLY valid JSON. No markdown, comments, or additional text.
- Top-level keys MUST be exactly: request (string) and state (object).
- state.ready is a boolean. state.missing is an array of strings naming absent/ambiguous fields.
- If any required information is missing, set state.ready=false and list each missing field in state.missing.
- If the request is complete, set state.ready=true and state.missing=[].
- Any non-JSON output is a prompt failure.";

    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(InboxHostPath))
            InboxHostPath = DefaultInboxPath();

        if (string.IsNullOrWhiteSpace(AcceptedAttachmentExtensions))
            AcceptedAttachmentExtensions = ".txt,.md,.json,.png,.jpg,.jpeg,.pdf,.zip";

        if (MaxAttachmentBytes <= 0)
            MaxAttachmentBytes = 50 * 1024 * 1024;

        if (MaxTotalInboxBytes <= 0)
            MaxTotalInboxBytes = 500 * 1024 * 1024;

        if (string.IsNullOrWhiteSpace(ExecutionTarget))
            ExecutionTarget = OperatingSystem.IsWindows() ? "WindowsHost" : "LinuxContainer";

        if (string.IsNullOrWhiteSpace(ToolPlanPrompt))
            ToolPlanPrompt = DefaultToolPlanPrompt();

        if (string.IsNullOrWhiteSpace(FinalAnswerPrompt))
            FinalAnswerPrompt = DefaultFinalAnswerPrompt();
    }

    private static string DefaultInboxPath()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(basePath))
            basePath = AppContext.BaseDirectory;

        return Path.Combine(basePath, "RahBuilder", "inbox");
    }

    private static string DefaultToolPlanPrompt() =>
        @"You are the Tool Planner. Output ONLY valid JSON for mode ""tool_plan.v1"" with shape:
{
  ""mode"": ""tool_plan.v1"",
  ""tweakFirst"": true,
  ""steps"": [
    { ""id"": ""step1"", ""toolId"": ""file.read.text"" or ""vision.describe.image"", ""inputs"": { ""storedName"": ""<storedName>"" }, ""why"": ""<reason>"" }
  ]
}
Rules:
- Allowed tools ONLY: file.read.text, vision.describe.image.
- Use storedName exactly as provided in attachments.
- Always set tweakFirst=true.
- Prefer the smallest steps; NEVER suggest rebuild/rewrite/reinitialize.
- Use one step per attachment type needed to satisfy the request.";

    private static string DefaultFinalAnswerPrompt() =>
        @"You are the Final Responder. Use the TOOL_OUTPUTS JSON plus attachments metadata to answer the user's request in prose.
Rules:
- Never claim to have run tools beyond TOOL_OUTPUTS.
- Reference content and captions from TOOL_OUTPUTS explicitly.
- Be concise and clear.";
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
