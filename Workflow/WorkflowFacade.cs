#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RahBuilder.Settings;
using RahBuilder.Tools.BlueprintTemplates;
using RahOllamaOnly.Tools;
using RahOllamaOnly.Tools.Prompt;
using RahOllamaOnly.Tracing;

namespace RahBuilder.Workflow;

public sealed class WorkflowFacade
{
    private readonly RunTrace _trace;
    private readonly WorkflowRouter _router = new();
    private AppConfig _cfg;
    private IReadOnlyList<AttachmentInbox.AttachmentEntry> _attachments = Array.Empty<AttachmentInbox.AttachmentEntry>();
    private readonly WorkflowState _state = new();
    private ToolManifest _toolManifest = new();
    private ToolPromptRegistry _toolPrompts = new();

    private string _lastGraphHash = "";

    // If true, the ONLY allowed reply is: "edit <rewrite>"
    private bool _forceEditBecauseInvalidJson = false;

    public sealed record RunnerBuildSummary(
        bool Ok,
        long RunId,
        string Status,
        string Conclusion,
        string HtmlUrl,
        IReadOnlyList<string> Logs,
        string Error)
    {
        public static RunnerBuildSummary Success(long runId, string status, string conclusion, string htmlUrl, IReadOnlyList<string> logs) =>
            new(true, runId, status, conclusion, htmlUrl, logs, "");

        public static RunnerBuildSummary Failed(string error, string detail = "") =>
            new(false, 0, "", "", "", Array.Empty<string>(), string.IsNullOrWhiteSpace(detail) ? error : $"{error}: {detail}");
    }

    public event Action<string>? UserFacingMessage;

    public WorkflowFacade(RunTrace trace)
    {
        _trace = trace ?? throw new ArgumentNullException(nameof(trace));
        _cfg = ConfigStore.Load();
        SyncGraphToHub(_cfg);
        LogRepoBanner();
    }

    public WorkflowFacade(AppConfig cfg, RunTrace trace)
    {
        _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
        _trace = trace ?? throw new ArgumentNullException(nameof(trace));
        SyncGraphToHub(_cfg);
        LogRepoBanner();
    }

    public void RefreshTooling(AppConfig cfg)
    {
        if (cfg != null) _cfg = cfg;
        SyncGraphToHub(_cfg);
        RefreshTooling();
    }

    public void SetAttachments(IReadOnlyList<AttachmentInbox.AttachmentEntry> attachments)
    {
        _attachments = attachments ?? Array.Empty<AttachmentInbox.AttachmentEntry>();
    }

    public void RefreshTooling()
    {
        var toolsPath = (_cfg.General.ToolsPath ?? "").Trim();
        var promptDir = (_cfg.General.ToolPromptsPath ?? "").Trim();

        ToolManifest manifest;
        try
        {
            manifest = ToolManifestLoader.LoadFromFile(toolsPath);
        }
        catch (Exception ex)
        {
            _trace.Emit($"[tooling:error] failed to load tools manifest at '{toolsPath}': {ex.Message}");
            return;
        }

        int promptFiles = 0;
        var prompts = new ToolPromptRegistry();
        try
        {
            if (!string.IsNullOrWhiteSpace(promptDir) && Directory.Exists(promptDir))
            {
                prompts.LoadFromDirectory(promptDir);
                promptFiles = prompts.AllToolIds.Count;
            }
        }
        catch { }

        _toolManifest = manifest;
        _toolPrompts = prompts;
        _trace.Emit($"[tooling] tools={manifest.ToolsById.Count} toolPrompts(files)={promptFiles}");
    }

    public Task RouteUserInput(string text, CancellationToken ct) => RouteUserInput(_cfg, text, ct);

    public async Task RouteUserInput(AppConfig cfg, string text, CancellationToken ct)
    {
        if (cfg != null) _cfg = cfg;
        text ??= "";

        _trace.Emit($"[chat] {text}");

        var action = UserReplyInterpreter.Interpret(_cfg, _state, text);

        // If last attempt produced invalid_json, DO NOT spin. Only accept "edit ...".
        if (_forceEditBecauseInvalidJson)
        {
            if (action.Action != ReplyActionType.NewRequest && action.Action != ReplyActionType.EditPlan)
            {
                EmitWaitUser("I didn’t catch that. Please restate what you want.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(action.Payload))
                text = action.Payload;

            _trace.Emit($"[chat:edit] {text}");
            _forceEditBecauseInvalidJson = false;
        }

        if (_state.PendingToolPlan != null)
        {
            var approvedAll = action.Action == ReplyActionType.ApproveAllSteps;
            if (action.Action == ReplyActionType.EditPlan)
            {
                var edited = action.Payload;
                if (string.IsNullOrWhiteSpace(edited))
                {
                    EmitToolPlanWait();
                    return;
                }

                _trace.Emit("[plan:edit] user provided new text, clearing pending tool plan.");
                _state.ClearPlan();
                text = edited;
            }
            else if (action.Action == ReplyActionType.RejectPlan)
            {
                _trace.Emit("[plan:reject] user rejected pending tool plan.");
                _state.ClearPlan();
                EmitWaitUser("Okay, I’ll stop. Tell me what you’d like instead.");
                return;
            }
            else if (action.Action is ReplyActionType.ApproveNextStep or ReplyActionType.ApproveAllSteps)
            {
                _state.AutoApproveAll = approvedAll;
                await ExecuteCurrentStepAsync(ct, approvedAll).ConfigureAwait(true);
                return;
            }
            else
            {
                EmitToolPlanWait();
                return;
            }
        }

        if (action.Action == ReplyActionType.AnswerClarification && !string.IsNullOrWhiteSpace(action.Payload))
        {
            _state.ClarificationAnswers.Add(action.Payload);
            _state.PendingQuestion = null;
            text = $"{_state.PendingUserRequest}\n\nAdditional detail: {string.Join("\n", _state.ClarificationAnswers)}";
            _trace.Emit("[clarify] received answer, re-running digest.");
        }
        else
        {
            _state.PendingUserRequest = text;
            _state.ClearClarifications();
            _state.PendingQuestion = null;
        }

        var repoScope = RepoScope.Resolve(_cfg);
        _trace.Emit(repoScope.Message);
        if (!repoScope.Ok)
        {
            EmitWaitUser("Set RepoRoot in Settings -> General.RepoRoot, then retry.");
            return;
        }

        if (!EnsureProvidersConfigured() || !EnsureToolPromptsReady())
            return;

        RepoScope.CheckGit(repoScope, _trace);

        SyncGraphToHub(_cfg);

        // No silent fallbacks.
        if (!_cfg.General.GraphDriven)
        {
            _trace.Emit("[route:disabled] GraphDriven is OFF (General.GraphDriven=false). No routing performed.");
            return;
        }

        // Validate graph first.
        var mermaid = _cfg.WorkflowGraph.MermaidText ?? "";
        var gv = _router.ValidateGraph(mermaid);
        if (!gv.Ok)
        {
            _trace.Emit($"[route:error] Invalid Mermaid workflow graph: {gv.Message}");
            _trace.Emit("[route:halt] Fix Workflow Graph settings page content, then retry.");
            return;
        }

        if (!OperatingSystem.IsWindows() || string.Equals(_cfg.General.ExecutionTarget, "LinuxContainer", StringComparison.OrdinalIgnoreCase))
        {
            if (IsWindowsDesktopProject())
            {
                _trace.Emit("[env:error] WinForms build requires WindowsDesktop SDK. Switch ExecutionTarget to WindowsHost.");
                _trace.Emit("[build:skip] WinForms requires WindowsHost; skipping build/run in container.");
                EmitWaitUser("WinForms build requires WindowsDesktop SDK. Switch ExecutionTarget to WindowsHost.");
                return;
            }
        }

        // Run digest.
        var userTextWithAttachments = AppendAttachmentMetadata(text);
        var spec = await RunJobSpecDigestAsync(userTextWithAttachments, ct).ConfigureAwait(true);

        if (!spec.IsComplete)
        {
            var missing = spec.GetMissingFields();

            // Special invalid_json case.
            if (missing.Count == 1 && string.Equals(missing[0], "invalid_json", StringComparison.OrdinalIgnoreCase))
            {
                _forceEditBecauseInvalidJson = true;

                EmitWaitUser("I had trouble reading the model output. Please restate what you want.");
                return;
            }

            // Normal WAIT_USER (missing real fields)
            _forceEditBecauseInvalidJson = false;

            var qs = ClarificationQuestionBank.PickQuestions(missing, 1);
            _state.PendingQuestions = qs;
            _state.PendingQuestion = qs.FirstOrDefault();
            _trace.Emit("[route:wait_user] JobSpec incomplete; asking clarifications.");
            EmitWaitUser(_state.PendingQuestion ?? "Could you share a bit more detail?", _state.PendingQuestion ?? "Could you share a bit more detail?");
            return;
        }

        _forceEditBecauseInvalidJson = false;

        var rawJson = spec.Raw?.RootElement.GetRawText() ?? "";
        var routeDecision = _router.Decide(mermaid, text);

        _trace.Emit("[digest:ok] JobSpec complete. Handing off to Planner (not wired in this phase).");
        _trace.Emit("[route:graph_ok] Mermaid workflow graph validated. (Planner dispatch not wired yet.)");
        if (routeDecision.Ok)
            _trace.Emit($"[route:selected] {routeDecision.SelectedRoute} (routes={routeDecision.RouteCount}) {routeDecision.Why}");
        else
            _trace.Emit("[route:error] " + routeDecision.Message);

        if (!string.IsNullOrWhiteSpace(rawJson))
            _trace.Emit("[jobspec] " + Trunc(rawJson, 800));

        if (_attachments == null || _attachments.Count == 0)
        {
            _trace.Emit("[attachments:none] Add at least one attachment to proceed to tool plan.");
            EmitWaitUser("Attach at least one file or image so I can help.");
            return;
        }

        var docAttachments = _attachments.Where(a => string.Equals(a.Kind, "document", StringComparison.OrdinalIgnoreCase)).ToList();
        if (docAttachments.Count > 1 && string.IsNullOrWhiteSpace(_state.PendingQuestion) && _state.PendingToolPlan == null && _state.ClarificationAnswers.Count == 0)
        {
            var names = string.Join(", ", docAttachments.Select(a => a.StoredName));
            _state.PendingQuestion = "Which file should I start with? " + names;
            EmitWaitUser("Which file should I start with? " + names);
            return;
        }

        var planOk = await BuildToolPlanAsync(text, rawJson, ct).ConfigureAwait(true);
        if (planOk)
            EmitToolPlanWait();
    }

    public async Task<RunnerBuildSummary> RunFromCodexAsync(string userText, CancellationToken ct)
    {
        var repoScope = RepoScope.Resolve(_cfg);
        _trace.Emit(repoScope.Message);
        if (!repoScope.Ok)
            return RunnerBuildSummary.Failed("repo_invalid", repoScope.Message);

        if (!EnsureProvidersConfigured() || !EnsureToolPromptsReady())
            return RunnerBuildSummary.Failed("validation_failed");

        var mermaid = _cfg.WorkflowGraph.MermaidText ?? "";
        var gv = _router.ValidateGraph(mermaid);
        if (!gv.Ok)
            return RunnerBuildSummary.Failed("graph_invalid", gv.Message);

        var digest = await RunJobSpecDigestAsync(AppendAttachmentMetadata(userText), ct).ConfigureAwait(false);
        if (!digest.IsComplete)
            return RunnerBuildSummary.Failed("jobspec_incomplete", string.Join(", ", digest.GetMissingFields()));

        var repo = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") ?? "";
        if (string.IsNullOrWhiteSpace(repo))
            return RunnerBuildSummary.Failed("missing_repository", "Set GITHUB_REPOSITORY=owner/repo");

        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? Environment.GetEnvironmentVariable("GH_TOKEN") ?? "";
        if (string.IsNullOrWhiteSpace(token))
            return RunnerBuildSummary.Failed("missing_token", "Set GITHUB_TOKEN or GH_TOKEN for workflow_dispatch.");

        var client = new GitHubActionsClient(repo, token, _trace);

        if (!await client.DispatchAsync("windows-runner-build.yml", "main", ct).ConfigureAwait(false))
            return RunnerBuildSummary.Failed("dispatch_failed");

        var since = DateTimeOffset.UtcNow.AddMinutes(-1);
        WorkflowRunInfo? run = null;
        for (var attempt = 0; attempt < 12 && run == null; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            run = await client.FindLatestRunAsync("windows-runner-build.yml", since, ct).ConfigureAwait(false);
            if (run == null)
                await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
        }

        if (run == null)
            return RunnerBuildSummary.Failed("run_not_found");

        _trace.Emit($"[gh:run] tracking run {run.Id} ({run.HtmlUrl})");

        while (!run.IsCompleted)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
            run = await client.GetRunAsync(run.Id, ct).ConfigureAwait(false) ?? run;
        }

        _trace.Emit($"[gh:run:done] status={run.Status} conclusion={run.Conclusion}");

        IReadOnlyList<string> logs = Array.Empty<string>();
        try
        {
            logs = await client.DownloadLogsAsync(run.Id, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _trace.Emit("[gh:logs:warn] " + ex.Message);
        }

        return RunnerBuildSummary.Success(run.Id, run.Status, run.Conclusion, run.HtmlUrl, logs);
    }

    private async Task<JobSpec> RunJobSpecDigestAsync(string userText, CancellationToken ct)
    {
        var prompt = (_cfg.General.JobSpecDigestPrompt ?? "").Trim();
        if (prompt.Length == 0)
        {
            _trace.Emit("[digest:error] JobSpecDigestPrompt is empty (General Settings).");
            return JobSpec.Invalid("job_spec_prompt_missing");
        }

        _trace.Emit("[digest] running Job Spec Digest");

        try
        {
            var result = await LlmInvoker.InvokeChatAsync(_cfg, "Orchestrator", prompt, userText, ct).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(result))
            {
                _trace.Emit("[digest:error] Digest returned empty response.");
                return JobSpec.Invalid("empty_digest_response");
            }

            // ALWAYS log raw BEFORE parsing. This is non-negotiable.
            _trace.Emit("[digest:raw] " + Trunc(result, 1200));

            var spec = JobSpecParser.TryParse(result);
            if (spec == null)
            {
                _trace.Emit("[digest:retry] Digest invalid JSON; requesting repair.");
                var retryPrompt = prompt + "\n\nYou returned invalid JSON. Output ONLY valid JSON for the schema.";
                result = await LlmInvoker.InvokeChatAsync(_cfg, "Orchestrator", retryPrompt, userText, ct).ConfigureAwait(false);
                _trace.Emit("[digest:raw] " + Trunc(result, 1200));
                spec = JobSpecParser.TryParse(result);
                if (spec == null)
                {
                    _trace.Emit("[digest:error] Digest did not return valid JSON.");
                    return JobSpec.Invalid("invalid_json");
                }
            }

            return spec;
        }
        catch (Exception ex)
        {
            _trace.Emit("[digest:error] Digest invocation failed: " + ex.Message);
            return JobSpec.Invalid("digest_invoke_failed");
        }
    }

    private void EmitWaitUser(string reason, string? friendly = null, string? preview = null)
    {
        var msg = WaitUserGate.GateMessage(reason, preview, _cfg.General.ConversationMode);
        _trace.Emit(msg);
        var chat = friendly ?? reason;
        if (!string.IsNullOrWhiteSpace(chat))
            UserFacingMessage?.Invoke(chat);
    }

    private void SyncGraphToHub(AppConfig cfg)
    {
        if (cfg == null) return;

        var graph = cfg.WorkflowGraph.MermaidText ?? "";
        var currentHash = ComputeHash(graph);

        if (string.Equals(currentHash, _lastGraphHash, StringComparison.OrdinalIgnoreCase))
            return;

        _lastGraphHash = currentHash;
        MermaidGraphHub.Publish(graph);

        if (string.IsNullOrWhiteSpace(graph))
            _trace.Emit("[graph] MermaidText is empty (WorkflowGraph.MermaidText).");
        else
            _trace.Emit($"[graph] published hash={MermaidGraphHub.CurrentHash}");
    }

    private static string ComputeHash(string s)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(s ?? "");
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }

    private static string Trunc(string s, int max)
    {
        s ??= "";
        s = s.Replace("\r", "");
        return s.Length <= max ? s : s.Substring(0, max) + "...";
    }

    private static bool IsWindowsDesktopProject() => true; // RahBuilder.csproj uses Microsoft.NET.Sdk.WindowsDesktop

    private string AppendAttachmentMetadata(string userText)
    {
        if (_attachments == null || _attachments.Count == 0)
            return userText ?? "";

        var sb = new StringBuilder();
        sb.AppendLine("ATTACHMENTS:");
        foreach (var a in _attachments)
        {
            sb.AppendLine($"- storedName: {a.StoredName}, kind: {a.Kind}, sizeBytes: {a.SizeBytes}, sha256: {a.Sha256}");
        }
        sb.AppendLine();
        sb.Append(userText ?? "");

        _trace.Emit($"[digest:attachments] {_attachments.Count} attached");
        return sb.ToString();
    }

    private AttachmentBlueprintInfo? LoadAttachmentBlueprint()
    {
        var folder = (_cfg.General.BlueprintTemplatesPath ?? "").Trim();
        if (string.IsNullOrWhiteSpace(folder))
            return null;

        try
        {
            var catalog = BlueprintCatalog.Load(folder);
            var entry = catalog.FirstOrDefault(e => string.Equals(e.Id, "attachment.ingest.v1", StringComparison.OrdinalIgnoreCase));
            if (entry == null)
                return null;

            var path = ResolveTemplatePath(folder, entry.File);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;

            var suggest = root.TryGetProperty("suggest", out var s) && s.ValueKind == JsonValueKind.Object ? s : default;
            var imageTools = ReadStringArray(suggest, "imageTools");
            var documentTools = ReadStringArray(suggest, "documentTools");

            if (imageTools.Count == 0 && documentTools.Count == 0)
            {
                imageTools.Add("vision.describe.image");
                documentTools.Add("file.read.text");
            }

            return new AttachmentBlueprintInfo
            {
                TemplatePath = path,
                Visibility = entry.Visibility,
                ImageTools = imageTools,
                DocumentTools = documentTools
            };
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveTemplatePath(string blueprintTemplatesFolder, string fileFromManifest)
    {
        if (string.IsNullOrWhiteSpace(fileFromManifest)) return "";

        var f = fileFromManifest.Replace('\\', '/').Trim();
        if (f.StartsWith("BlueprintTemplates/", StringComparison.OrdinalIgnoreCase))
            f = f.Substring("BlueprintTemplates/".Length);

        if (Path.IsPathRooted(f))
            return f;

        return Path.Combine(blueprintTemplatesFolder, f.Replace('/', Path.DirectorySeparatorChar));
    }

    private bool EnsureProvidersConfigured()
    {
        var missing = new List<string>();
        var roles = _cfg.Orchestrator?.Roles ?? new List<OrchestratorRoleConfig>();

        foreach (var role in roles)
        {
            var name = role?.Role ?? "(unknown)";
            if (string.IsNullOrWhiteSpace(role?.Provider) || string.IsNullOrWhiteSpace(role?.Model))
                missing.Add(name);
        }

        if (missing.Count == 0)
            return true;

        var msg = "Missing provider/model for roles: " + string.Join(", ", missing);
        _trace.Emit("[providers:error] " + msg);
        EmitWaitUser(msg, "Update provider/model settings and retry.");
        return false;
    }

    private bool EnsureToolPromptsReady()
    {
        if (_toolManifest == null || _toolManifest.ToolsById.Count == 0)
        {
            _trace.Emit("[tooling:error] Tools manifest is empty or failed to load.");
            EmitWaitUser("Tools manifest not loaded. Set ToolsPath in Settings.");
            return false;
        }

        var missingPrompts = new List<string>();
        foreach (var id in _toolManifest.ToolsById.Keys)
        {
            if (!_toolPrompts.Has(id))
                missingPrompts.Add(id);
        }

        if (missingPrompts.Count == 0)
            return true;

        var msg = "Missing tool prompts: " + string.Join(", ", missingPrompts);
        _trace.Emit("[tooling:error] " + msg);
        EmitWaitUser(msg, "Add prompt files for missing tools.");
        return false;
    }

    private static List<string> ReadStringArray(JsonElement obj, string property)
    {
        var list = new List<string>();
        if (obj.ValueKind != JsonValueKind.Object)
            return list;

        if (!obj.TryGetProperty(property, out var el) || el.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var s = item.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    list.Add(s.Trim());
            }
        }

        return list;
    }

    private void LogRepoBanner()
    {
        var scope = RepoScope.Resolve(_cfg);
        _trace.Emit(scope.Message);
        if (!string.IsNullOrWhiteSpace(scope.GitTopLevel))
            _trace.Emit($"[repo] GitTopLevel={scope.GitTopLevel}");
    }

    private static string DescribeStep(ToolPlanStep step)
    {
        var name = step.ToolId.ToLowerInvariant();
        var stored = step.Inputs.TryGetValue("storedName", out var s) ? s : "";
        if (name == "file.read.text")
            return $"read the file {stored}";
        if (name == "vision.describe.image")
            return $"describe the image {stored}";
        return $"{step.ToolId} ({stored})";
    }

    private sealed class AttachmentBlueprintInfo
    {
        public string TemplatePath { get; init; } = "";
        public string Visibility { get; init; } = "";
        public List<string> ImageTools { get; init; } = new();
        public List<string> DocumentTools { get; init; } = new();
    }

    private async Task<bool> BuildToolPlanAsync(string userText, string jobSpecJson, CancellationToken ct)
    {
        var prompt = (_cfg.General.ToolPlanPrompt ?? "").Trim();
        if (prompt.Length == 0)
        {
            _trace.Emit("[toolplan:error] ToolPlanPrompt is empty (General.ToolPlanPrompt).");
            EmitWaitUser("ToolPlan prompt is empty. Update Settings -> General -> Tool Plan Prompt.");
            return false;
        }

        var blueprint = LoadAttachmentBlueprint();
        var sb = new StringBuilder();
        sb.AppendLine("SCHEMA: tool_plan.v1 (mode,tweakFirst,steps[id,toolId,inputs{storedName},why])");
        sb.AppendLine("ALLOWED_TOOLS: file.read.text, vision.describe.image");
        sb.AppendLine("STRICT: JSON only, no markdown, no prose, no invented tools.");
        sb.AppendLine("CHANGE_STRATEGY: " + ComputeChangeStrategy(_state.PendingUserRequest));
        sb.AppendLine();
        sb.AppendLine("USER REQUEST:");
        sb.AppendLine(userText ?? "");
        sb.AppendLine();
        sb.AppendLine("JOB_SPEC_JSON:");
        sb.AppendLine(jobSpecJson ?? "");
        sb.AppendLine();
        sb.AppendLine("ATTACHMENTS:");
        foreach (var a in _attachments)
        {
            sb.AppendLine($"- storedName: {a.StoredName}, kind: {a.Kind}, sizeBytes: {a.SizeBytes}, sha256: {a.Sha256}");
        }

        if (blueprint != null)
        {
            sb.AppendLine();
            sb.AppendLine("SUGGESTED_TOOLS:");
            sb.AppendLine("imageTools: " + string.Join(", ", blueprint.ImageTools));
            sb.AppendLine("documentTools: " + string.Join(", ", blueprint.DocumentTools));
        }

        string toolPlanText;
        try
        {
            toolPlanText = await LlmInvoker.InvokeChatAsync(_cfg, "Orchestrator", prompt, sb.ToString(), ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _trace.Emit("[toolplan:error] invoke failed: " + ex.Message);
            EmitWaitUser("ToolPlan invocation failed. Reply with: edit <rewrite your request>");
            return false;
        }

        _trace.Emit("[toolplan:raw] " + Trunc(toolPlanText, 1200));

        var plan = ToolPlanParser.TryParse(toolPlanText);
        if (plan == null)
        {
            _trace.Emit("[toolplan:retry] invalid JSON, requesting repair.");
            try
            {
                var repairPrompt = prompt + "\n\nYou returned invalid JSON. Output ONLY valid tool_plan.v1 JSON.";
                toolPlanText = await LlmInvoker.InvokeChatAsync(_cfg, "Orchestrator", repairPrompt, sb.ToString(), ct).ConfigureAwait(false);
                _trace.Emit("[toolplan:raw] " + Trunc(toolPlanText, 1200));
                plan = ToolPlanParser.TryParse(toolPlanText);
            }
            catch (Exception ex)
            {
                _trace.Emit("[toolplan:error] retry failed: " + ex.Message);
            }
        }

        if (plan == null)
        {
            EmitWaitUser("I couldn't build a tool plan. Could you restate what you want?");
            return false;
        }

        if (_cfg.General.TweakFirstMode && !plan.TweakFirst)
        {
            _trace.Emit("[toolplan:error] tweakFirst required.");
            EmitWaitUser("Tool plan must set tweakFirst=true. Reply with: edit <rewrite your request>");
            return false;
        }

        if (!ValidateToolPlan(plan))
        {
            EmitWaitUser("Tool plan invalid. Reply with: edit <rewrite your request>");
            return false;
        }

        _state.PendingToolPlanJson = toolPlanText;
        _state.PendingToolPlan = plan;
        _state.PendingStepIndex = 0;
        _state.ToolOutputs = new List<System.Text.Json.JsonElement>();
        _state.LastJobSpecJson = jobSpecJson;
        _state.OriginalUserText = userText;
        _state.PendingQuestion = null;
        return true;
    }

    private bool ValidateToolPlan(ToolPlan plan)
    {
        if (plan == null || plan.Steps == null || plan.Steps.Count == 0)
            return false;

        foreach (var step in plan.Steps)
        {
            if (!_toolManifest.Has(step.ToolId))
            {
                _trace.Emit($"[toolplan:error] step tool not in manifest: {step.ToolId}");
                return false;
            }
            if (!_toolPrompts.Has(step.ToolId))
            {
                _trace.Emit($"[toolplan:error] step tool missing prompt: {step.ToolId}");
                return false;
            }

            if (!step.Inputs.TryGetValue("storedName", out var stored) || string.IsNullOrWhiteSpace(stored))
            {
                _trace.Emit("[toolplan:error] step missing storedName input.");
                return false;
            }

            var att = _attachments.FirstOrDefault(a => string.Equals(a.StoredName, stored, StringComparison.OrdinalIgnoreCase));
            if (att == null)
            {
                _trace.Emit($"[toolplan:error] attachment not found for storedName: {stored}");
                return false;
            }
        }

        return true;
    }

    private void EmitToolPlanWait()
    {
        if (_state.PendingToolPlan == null)
        {
            EmitWaitUser("No pending tool plan. Reply with: edit <rewrite your request>");
            return;
        }

        var mode = _cfg.General.ConversationMode;
        string msg;

        if (mode == ConversationMode.Strict)
        {
            var sb = new StringBuilder();
            sb.AppendLine("ToolPlan Steps:");
            for (int i = 0; i < _state.PendingToolPlan.Steps.Count; i++)
            {
                var step = _state.PendingToolPlan.Steps[i];
                var prefix = i == _state.PendingStepIndex ? "->" : "  ";
                sb.AppendLine($"{prefix} Step {i + 1}: {step.ToolId} ({step.Id}) inputs: {string.Join(", ", step.Inputs.Select(kv => $"{kv.Key}={kv.Value}"))} why: {step.Why}");
            }
            EmitWaitUser("Approve tool plan to continue.", "Ready to proceed with the plan?", sb.ToString().TrimEnd());
        }
        else
        {
            var remaining = _state.PendingToolPlan.Steps.Skip(_state.PendingStepIndex).ToList();
            var summary = string.Join(", then ", remaining.Select(DescribeStep));
            var question = string.IsNullOrWhiteSpace(summary)
                ? "Ready to proceed with the next step?"
                : $"I can {summary}. Want me to proceed?";
            EmitWaitUser(question, question);
        }

        _state.PendingQuestion = null;
    }

    private async Task ExecuteCurrentStepAsync(CancellationToken ct, bool approveAll = false)
    {
        if (_state.PendingToolPlan == null)
        {
            EmitWaitUser("No tool plan to execute.");
            return;
        }

        if (_state.PendingStepIndex < 0 || _state.PendingStepIndex >= _state.PendingToolPlan.Steps.Count)
        {
            EmitWaitUser("Tool plan step index invalid.");
            _state.ClearPlan();
            return;
        }

        var step = _state.PendingToolPlan.Steps[_state.PendingStepIndex];
        var result = await ToolStepRunner.RunAsync(step, _cfg, _trace, _attachments, _toolManifest, _toolPrompts, ct).ConfigureAwait(false);
        if (!result.Ok)
        {
            EmitWaitUser(result.Message, "Something went wrong. Want me to retry this step?");
            return;
        }

        if (result.Output.HasValue)
            _state.ToolOutputs.Add(result.Output.Value);
        SendChatStepSummary(result);

        _state.PendingStepIndex++;
        if (_state.PendingStepIndex >= _state.PendingToolPlan.Steps.Count)
        {
            await ProduceFinalResponseAsync(ct).ConfigureAwait(false);
            _state.ClearPlan();
            return;
        }

        EmitToolPlanWait();
    }

    private async Task ProduceFinalResponseAsync(CancellationToken ct)
    {
        var prompt = (_cfg.General.FinalAnswerPrompt ?? "").Trim();
        if (prompt.Length == 0)
        {
            _trace.Emit("[final:error] FinalAnswerPrompt is empty.");
            EmitWaitUser("FinalAnswerPrompt is empty. Update settings.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("USER REQUEST:");
        sb.AppendLine(_state.OriginalUserText ?? "");
        sb.AppendLine();
        sb.AppendLine("JOB_SPEC_JSON:");
        sb.AppendLine(_state.LastJobSpecJson ?? "");
        sb.AppendLine();
        sb.AppendLine("ATTACHMENTS:");
        foreach (var a in _attachments)
        {
            sb.AppendLine($"- storedName: {a.StoredName}, kind: {a.Kind}, sizeBytes: {a.SizeBytes}, sha256: {a.Sha256}");
        }
        sb.AppendLine();
        sb.AppendLine("TOOL_OUTPUTS:");
        sb.AppendLine(JsonSerializer.Serialize(_state.ToolOutputs));

        string resp;
        try
        {
            resp = await LlmInvoker.InvokeChatAsync(_cfg, "Orchestrator", prompt, sb.ToString(), ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _trace.Emit("[final:error] " + ex.Message);
            EmitWaitUser("Final response failed. Reply with: edit <rewrite your request>");
            return;
        }

        _trace.Emit("[final] " + Trunc(resp, 1200));
        UserFacingMessage?.Invoke(resp);
    }

    private static string ComputeChangeStrategy(string text)
    {
        var t = (text ?? "").ToLowerInvariant();
        if (t.Contains("rebuild") || t.Contains("rewrite everything"))
            return "Rebuild";
        if (t.Contains("patch") || t.Contains("fix"))
            return "Patch";
        return "Tweak";
    }

    private void SendChatStepSummary(ToolRunResult result)
    {
        if (!result.Output.HasValue) return;
        try
        {
            var root = result.Output.Value;
            if (!root.TryGetProperty("toolId", out var toolEl) || toolEl.ValueKind != JsonValueKind.String)
                return;
            var toolId = toolEl.GetString() ?? "";
            if (toolId.Equals("file.read.text", StringComparison.OrdinalIgnoreCase))
            {
                var name = root.TryGetProperty("storedName", out var n) ? n.GetString() : "";
                var content = root.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                var preview = content.Length <= 200 ? content : content.Substring(0, 200) + "...";
                UserFacingMessage?.Invoke($"I read {name}. Quick take: {preview}");
            }
            else if (toolId.Equals("vision.describe.image", StringComparison.OrdinalIgnoreCase))
            {
                var name = root.TryGetProperty("storedName", out var n) ? n.GetString() : "";
                var caption = root.TryGetProperty("caption", out var cap) ? cap.GetString() : "";
                var tags = root.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array
                    ? string.Join(", ", tagsEl.EnumerateArray().Where(t => t.ValueKind == JsonValueKind.String).Select(t => t.GetString()).Where(s => !string.IsNullOrWhiteSpace(s)))
                    : "";
                var msg = $"I described {name}: {caption}";
                if (!string.IsNullOrWhiteSpace(tags))
                    msg += $" (tags: {tags})";
                UserFacingMessage?.Invoke(msg);
            }
        }
        catch { }
    }
}
