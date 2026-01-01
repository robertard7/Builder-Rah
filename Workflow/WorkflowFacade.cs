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

    public event Action<string>? UserFacingMessage;

    public WorkflowFacade(RunTrace trace)
    {
        _trace = trace ?? throw new ArgumentNullException(nameof(trace));
        _cfg = ConfigStore.Load();
        SyncGraphToHub(_cfg);
    }

    public WorkflowFacade(AppConfig cfg, RunTrace trace)
    {
        _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
        _trace = trace ?? throw new ArgumentNullException(nameof(trace));
        SyncGraphToHub(_cfg);
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

        // If last attempt produced invalid_json, DO NOT spin. Only accept "edit ...".
        if (_forceEditBecauseInvalidJson)
        {
            var t = text.Trim();
            if (!t.StartsWith("edit", StringComparison.OrdinalIgnoreCase))
            {
                EmitWaitUser("Model output was not valid JSON. Reply with: edit <rewrite your request>");
                return;
            }

            var edited = t.Length > 4 ? t[4..].Trim() : "";
            if (edited.Length == 0)
            {
                EmitWaitUser("Reply with: edit <rewrite your request>");
                return;
            }

            text = edited;
            _trace.Emit($"[chat:edit] {text}");

            // We have new input, try digest again.
            _forceEditBecauseInvalidJson = false;
        }

        if (_state.PendingToolPlan != null)
        {
            var resp = WaitUserGate.ParseResponse(text);
            if (resp.Action == WaitUserAction.Edit)
            {
                var edited = resp.EditText ?? "";
                if (string.IsNullOrWhiteSpace(edited))
                {
                    EmitToolPlanWait();
                    return;
                }

                _trace.Emit("[plan:edit] user provided new text, clearing pending tool plan.");
                _state.ClearPlan();
                text = edited;
            }
            else if (resp.Action == WaitUserAction.Reject)
            {
                _trace.Emit("[plan:reject] user rejected pending tool plan.");
                _state.ClearPlan();
                EmitWaitUser("Tool plan rejected. Provide a new request.");
                return;
            }
            else if (resp.Action is WaitUserAction.Accept or WaitUserAction.AcceptStep)
            {
                var expected = _state.PendingStepIndex;
                var requested = resp.StepIndex ?? expected;
                if (requested != expected)
                {
                    EmitWaitUser($"Next step is {expected + 1}. Reply with: accept step {expected + 1}");
                    return;
                }

                await ExecuteCurrentStepAsync(ct).ConfigureAwait(true);
                return;
            }
            else
            {
                EmitToolPlanWait();
                return;
            }
        }

        var repoScope = RepoScope.Resolve(_cfg);
        _trace.Emit(repoScope.Message);
        if (!repoScope.Ok)
        {
            EmitWaitUser("Set RepoRoot in Settings -> General.RepoRoot, then retry.");
            return;
        }

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

                EmitWaitUser("Model output was not valid JSON. Reply with: edit <rewrite your request>");
                return;
            }

            // Normal WAIT_USER (missing real fields)
            _forceEditBecauseInvalidJson = false;

            var joined = missing.Count == 0 ? "unknown" : string.Join(", ", missing);
            _trace.Emit("[route:wait_user] JobSpec incomplete.");
            EmitWaitUser($"Missing fields: {joined}");
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
            EmitWaitUser("Attach at least one file, then reply with: accept (after plan appears).");
            return;
        }

        var planOk = await BuildToolPlanAsync(text, rawJson, ct).ConfigureAwait(true);
        if (planOk)
            EmitToolPlanWait();
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
                _trace.Emit("[digest:error] Digest did not return valid JSON.");
                return JobSpec.Invalid("invalid_json");
            }

            return spec;
        }
        catch (Exception ex)
        {
            _trace.Emit("[digest:error] Digest invocation failed: " + ex.Message);
            return JobSpec.Invalid("digest_invoke_failed");
        }
    }

    private void EmitWaitUser(string reason)
    {
        var msg = WaitUserGate.GateMessage(reason);
        _trace.Emit(msg);
        UserFacingMessage?.Invoke(msg);
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
            _forceEditBecauseInvalidJson = true;
            EmitWaitUser("Model output was not valid JSON. Reply with: edit <rewrite your request>");
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

        var sb = new StringBuilder();
        sb.AppendLine("ToolPlan Steps:");
        for (int i = 0; i < _state.PendingToolPlan.Steps.Count; i++)
        {
            var step = _state.PendingToolPlan.Steps[i];
            var prefix = i == _state.PendingStepIndex ? "->" : "  ";
            sb.AppendLine($"{prefix} Step {i + 1}: {step.ToolId} ({step.Id}) inputs: {string.Join(", ", step.Inputs.Select(kv => $"{kv.Key}={kv.Value}"))} why: {step.Why}");
        }

        var msg = WaitUserGate.GateMessage("Approve tool plan to continue.", sb.ToString().TrimEnd());
        _state.PendingQuestion = msg;
        _trace.Emit(msg);
        UserFacingMessage?.Invoke(msg);
    }

    private async Task ExecuteCurrentStepAsync(CancellationToken ct)
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
            EmitWaitUser(result.Message);
            return;
        }

        if (result.Output.HasValue)
            _state.ToolOutputs.Add(result.Output.Value);

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
}
