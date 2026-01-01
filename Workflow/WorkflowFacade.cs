#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RahBuilder.Settings;
using RahOllamaOnly.Tools;
using RahOllamaOnly.Tracing;

namespace RahBuilder.Workflow;

public sealed class WorkflowFacade
{
    private readonly RunTrace _trace;
    private readonly WorkflowRouter _router = new();
    private AppConfig _cfg;

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

        // Run digest.
        var spec = await RunJobSpecDigestAsync(text, ct).ConfigureAwait(true);

        // If digest failed JSON, we do TWO things:
        // 1) force edit next (prevents infinite loop)
        // 2) create a local fallback JobSpec so WAIT_USER can be useful
        if (spec == null)
        {
            _trace.Emit("[digest:error] Digest returned null JobSpec (unexpected).");
            _forceEditBecauseInvalidJson = true;

            var fallback = JobSpecFallbackFromUserText(text);
            _trace.Emit("[digest:fallback] local minimal JobSpec created (null spec).");
            EmitWaitUser("Model output was not valid JSON. Reply with: edit <rewrite your request>");

            return;
        }

        if (!spec.IsComplete)
        {
            var missing = spec.GetMissingFields();

            // Special invalid_json case.
            if (missing.Count == 1 && string.Equals(missing[0], "invalid_json", StringComparison.OrdinalIgnoreCase))
            {
                _forceEditBecauseInvalidJson = true;

                var fallback = JobSpecFallbackFromUserText(text);
                _trace.Emit("[digest:fallback] local minimal JobSpec created (invalid_json).");

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

        // Save-point honesty: planner not wired yet.
        _trace.Emit("[digest:ok] JobSpec complete. Handing off to Planner (not wired in this phase).");
        _trace.Emit("[route:graph_ok] Mermaid workflow graph validated. (Planner dispatch not wired yet.)");
    }

    private async Task<JobSpec?> RunJobSpecDigestAsync(string userText, CancellationToken ct)
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

    // Local minimal fallback so WAIT_USER isn't useless.
    // This does NOT hardcode app logic. It just preserves user intent and flags missing fields.
    private static JobSpec JobSpecFallbackFromUserText(string userText)
    {
        // This assumes your JobSpec has these members as described earlier:
        // intent, jobType, targets, project, features, constraints, acceptance, state.
        // If your actual JobSpec shape differs, this will fail to compile and you'll adjust it once.
        var spec = new JobSpec
        {
            intent = userText ?? "",
            jobType = "unknown",
            targets = Array.Empty<string>(),
            project = new(),
            features = Array.Empty<string>(),
            constraints = Array.Empty<string>(),
            acceptance = Array.Empty<string>(),
            state = new()
            {
                ready = false,
                missing = new[] { "jobType", "targets", "project" }
            }
        };

        return spec;
    }
}
