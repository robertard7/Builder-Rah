#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RahBuilder.Settings;
using RahBuilder.Tools.BlueprintTemplates;
using RahOllamaOnly.Tools;
using RahOllamaOnly.Tracing;

namespace RahBuilder.Workflow;

public sealed class WorkflowFacade
{
    private readonly RunTrace _trace;
    private readonly WorkflowRouter _router = new();
    private AppConfig _cfg;
    private IReadOnlyList<AttachmentInbox.AttachmentEntry> _attachments = Array.Empty<AttachmentInbox.AttachmentEntry>();

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
        try
        {
            if (!string.IsNullOrWhiteSpace(promptDir) && Directory.Exists(promptDir))
                promptFiles = Directory.GetFiles(promptDir).Length;
        }
        catch { }

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

        EmitAttachmentSuggestions();
        _trace.Emit("[planner] not wired: no execution performed.");
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

    private void EmitAttachmentSuggestions()
    {
        if (_attachments == null || _attachments.Count == 0)
        {
            _trace.Emit("[attachments] none");
            return;
        }

        var blueprint = LoadAttachmentBlueprint();
        if (blueprint == null)
        {
            _trace.Emit("[attachments] blueprint attachment.ingest.v1 missing or unreadable.");
            return;
        }

        _trace.Emit($"[attachments] {_attachments.Count} attachment(s) present. blueprint={blueprint.TemplatePath} visibility={blueprint.Visibility}");

        foreach (var a in _attachments)
        {
            var tools = new List<string>();
            var kind = (a.Kind ?? "").ToLowerInvariant();
            if (kind == "image")
                tools.AddRange(blueprint.ImageTools);
            else if (kind == "document")
                tools.AddRange(blueprint.DocumentTools);

            var toolText = tools.Count == 0 ? "none" : string.Join(", ", tools.Distinct(StringComparer.OrdinalIgnoreCase));
            _trace.Emit($"[attachments:suggest] {a.StoredName} ({kind}, {a.SizeBytes} bytes) -> {toolText}");
        }
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
}
