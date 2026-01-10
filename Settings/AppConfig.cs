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
    public ConversationMode ConversationMode { get; set; } = ConversationMode.Conversational;
    public string SandboxHostPath { get; set; } = "";
    public string SandboxContainerPath { get; set; } = "";
    public string InboxHostPath { get; set; } = DefaultInboxPath();

    public string BlueprintTemplatesPath { get; set; } = "";    // folder: BlueprintTemplates (prompt library)
    public string ToolPlanPrompt { get; set; } = DefaultToolPlanPrompt();
    public string IntentExtractorPrompt { get; set; } = DefaultIntentExtractorPrompt();
    public string FinalAnswerPrompt { get; set; } = DefaultFinalAnswerPrompt();
    public string ProgramBuilderPrompt { get; set; } = DefaultProgramBuilderPrompt();
    public string AcceptedAttachmentExtensions { get; set; } = ".txt,.md,.json,.png,.jpg,.jpeg,.pdf,.zip";
    public long MaxAttachmentBytes { get; set; } = 50 * 1024 * 1024;
    public long MaxTotalInboxBytes { get; set; } = 500 * 1024 * 1024;

    public bool GraphDriven { get; set; } = true;
    public bool ContainerOnly { get; set; } = true;
    public bool EnableGlobalClipboardShortcuts { get; set; } = true;
    public string ExecutionTarget { get; set; } = OperatingSystem.IsWindows() ? "WindowsHost" : "LinuxContainer";
    public bool EnableProviderApi { get; set; } = true;
    public int ProviderApiPort { get; set; } = 5050;

    // SAVE POINT: global digest prompt lives here (JSON-only, no tools)
    public string JobSpecDigestPrompt { get; set; } =
        @"You are the JobSpec Digest. Respond with ONE JSON object only (no prose, no code fences).
Summarize intent as: {Request, Goal, Attachments, Actions, Context, Constraints}
Output shape:
{
  ""request"": ""<verbatim user request text>"",
  ""mode"": ""jobspec.v2"",
  ""goal"": ""<plain goal>"",
  ""context"": ""<helpful background or empty>"",
  ""actions"": [""describe image"", ""summarize document"", ""combine findings""],
  ""constraints"": [""<constraint>"", ""<constraint>""],
  ""attachments"": [
    {
      ""storedName"": ""<storedName from ATTACHMENTS block>"",
      ""kind"": ""image|document|code|other"",
      ""status"": ""present|missing|pending"",
      ""summary"": ""<how to use this attachment>"",
      ""tags"": [""<keywords from filename or ATTACHMENTS>"", ""...""],
      ""tools"": [""vision.describe.image"" or ""file.read.text""]
    }
  ],
  ""clarification"": ""<one short natural question if anything important is missing; empty when ready>"",
  ""state"": { ""ready"": <true|false>, ""missing"": [""request"", ""goal"", ""actions"", ""attachments"", ""context"", ""constraints""] }
}
Rules:
- ALWAYS emit valid JSON; no markdown or comments.
- Always include an attachments array (can be empty).
- Use the ATTACHMENTS section to pre-fill storedName, kind, status (default present), summary, tags (keywords from filename/description), and tools (vision.describe.image for images; file.read.text for documents/code).
- Base actions on attachments and intent: e.g., describe images, read/summarize documents/code, then combine results when requested.
- If any required field is missing/ambiguous, set state.ready=false and list the missing concepts in state.missing. Otherwise, set ready=true and missing=[].
- Ask at most ONE clarifying question in ""clarification"" when ready=false; leave it empty when ready=true.
- Do NOT invent tools beyond vision.describe.image or file.read.text.";

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

        if (string.IsNullOrWhiteSpace(IntentExtractorPrompt))
            IntentExtractorPrompt = DefaultIntentExtractorPrompt();

        if (string.IsNullOrWhiteSpace(ProgramBuilderPrompt))
            ProgramBuilderPrompt = DefaultProgramBuilderPrompt();

        if (!Enum.IsDefined(typeof(ConversationMode), ConversationMode))
            ConversationMode = ConversationMode.Conversational;

        if (ProviderApiPort <= 0)
            ProviderApiPort = 5050;
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
- Use one step per attachment type needed to satisfy the request.
- Do NOT output prose or markdown; ONLY JSON.";

    private static string DefaultIntentExtractorPrompt() =>
        @"You are the Intent Extractor. Produce ONE JSON object only (no prose).
Shape:
{
  ""mode"": ""intent.v1"",
  ""request"": ""<verbatim user text>"",
  ""goal"": ""<plain goal>"",
  ""context"": ""<important background>"",
  ""actions"": [""action 1"", ""action 2""],
  ""constraints"": [""constraint"", ""examples""],
  ""clarification"": ""<one short question if needed else empty>"",
  ""missing"": [""goal|actions|constraints|context|attachments""],
  ""ready"": true|false
}
Rules:
- Detect multi-step requests signaled by words like 'then', 'after', 'also', 'next'; split them into separate entries in actions.
- Reflect attachments and history if provided.
- Ask at most ONE clarification for a missing concept.
- ready=true only when goal + actions are clear.";

    private static string DefaultFinalAnswerPrompt() =>
        @"You are the Final Responder. Use the TOOL_OUTPUTS JSON plus attachments metadata to answer the user's request in natural, concise language.
Rules:
- Do not mention internal systems or planner details.
- Reference content and captions from TOOL_OUTPUTS explicitly.
- Keep it brief and helpful.";

    private static string DefaultProgramBuilderPrompt() =>
        @"You are the Program Builder. Output JSON ONLY with shape:
{
  ""mode"": ""program_artifacts.v1"",
  ""files"": [
    { ""path"": ""README.md"", ""content"": ""..."", ""tags"": [""readme""] },
    { ""path"": ""Tests/sample.test"", ""content"": ""..."", ""tags"": [""test""] }
  ]
}
Rules:
- Include README, at least one test file, and main source files.
- Respect constraints like ""short summary"" or ""include code examples"".
- Do not emit markdown fences; JSON only.";
}

public enum ConversationMode
{
    Conversational,
    Strict
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
