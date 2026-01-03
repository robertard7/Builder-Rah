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
    private readonly ExecutionOrchestrator _executor;
    private readonly ProgramArtifactGenerator _artifactGenerator = new();
    private readonly ProviderApiHost _apiHost;
    private AppConfig _cfg;
    private IReadOnlyList<AttachmentInbox.AttachmentEntry> _attachments = Array.Empty<AttachmentInbox.AttachmentEntry>();
    private readonly WorkflowState _state = new();
    private ToolManifest _toolManifest = new();
    private ToolPromptRegistry _toolPrompts = new();

    private string _lastGraphHash = "";
    private bool _recentlyCompleted;

    // If true, the ONLY allowed reply is: "edit <rewrite>"
    private bool _forceEditBecauseInvalidJson = false;
    private bool _planNeedsArtifacts;

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

    public sealed record WorkflowStatusSnapshot(
        string Mode,
        string Repo,
        int AttachmentsTotal,
        int AttachmentsActive,
        string Workflow,
        int StepIndex,
        int StepCount);

    public event Action<string>? UserFacingMessage;
    public event Action<WorkflowStatusSnapshot>? StatusChanged;
    public event Action<string?>? PendingQuestionChanged;
    public event Action<string?, bool>? PendingStepChanged;
    public event Action? TraceAttentionRequested;
    public event Action<OutputCard>? OutputCardProduced;
    public event Action<PlanDefinition>? PlanReady;

    public WorkflowFacade(RunTrace trace)
    {
        _trace = trace ?? throw new ArgumentNullException(nameof(trace));
        _cfg = ConfigStore.Load();
        _executor = new ExecutionOrchestrator(_state, _trace);
        _executor.OutputCardProduced += card => OutputCardProduced?.Invoke(card);
        _executor.UserFacingMessage += msg =>
        {
            RecordAssistantMessage(msg);
            UserFacingMessage?.Invoke(msg);
        };
        _apiHost = new ProviderApiHost(this);
        SyncGraphToHub(_cfg);
        LogRepoBanner();
        StartApiHostIfEnabled();
    }

    public WorkflowFacade(AppConfig cfg, RunTrace trace)
    {
        _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
        _trace = trace ?? throw new ArgumentNullException(nameof(trace));
        _executor = new ExecutionOrchestrator(_state, _trace);
        _executor.OutputCardProduced += card => OutputCardProduced?.Invoke(card);
        _executor.UserFacingMessage += msg =>
        {
            RecordAssistantMessage(msg);
            UserFacingMessage?.Invoke(msg);
        };
        _apiHost = new ProviderApiHost(this);
        SyncGraphToHub(_cfg);
        LogRepoBanner();
        StartApiHostIfEnabled();
    }

    private void StartApiHostIfEnabled()
    {
        try
        {
            if (_cfg.General.EnableProviderApi)
                _apiHost.Start(_cfg.General.ProviderApiPort);
        }
        catch (Exception ex)
        {
            _trace.Emit("[api:warn] " + ex.Message);
        }
    }

    public void RefreshTooling(AppConfig cfg)
    {
        if (cfg != null) _cfg = cfg;
        SyncGraphToHub(_cfg);
        RefreshTooling();
    }

    public Task ApproveNextStepAsync(CancellationToken ct) => ExecuteCurrentStepAsync(ct);
    public Task ApproveAllStepsAsync(CancellationToken ct)
    {
        _state.AutoApproveAll = true;
        return ExecuteCurrentStepAsync(ct, true);
    }

    public void StopPlan()
    {
        _recentlyCompleted = false;
        _state.ClearPlan();
        _state.PendingUserRequest = "";
        _state.ClearClarifications();
        PendingStepChanged?.Invoke("", false);
        NotifyStatus();
        _planNeedsArtifacts = false;
    }

    public void RequestPlanEdit()
    {
        _recentlyCompleted = false;
        _state.ClearPlan();
        _state.PendingUserRequest = "";
        _state.ClearClarifications();
        PendingStepChanged?.Invoke("", false);
        EmitWaitUser("Plan cleared. Tell me what you’d like instead.");
        NotifyStatus();
    }

    public void SetAttachments(IReadOnlyList<AttachmentInbox.AttachmentEntry> attachments)
    {
        _attachments = (attachments ?? Array.Empty<AttachmentInbox.AttachmentEntry>()).ToList();
        NotifyStatus();
    }

    public ToolPlan? GetPendingPlan() => _state.PendingToolPlan;

    public void ApplyEditedPlan(ToolPlan? plan)
    {
        if (plan == null)
        {
            EmitWaitUser("Plan edit cancelled.");
            return;
        }

        if (!ValidateToolPlan(plan))
        {
            EmitWaitUser("Edited plan is invalid. Please adjust and try again.");
            return;
        }

        var planJson = JsonSerializer.Serialize(new
        {
            mode = "tool_plan.v1",
            tweakFirst = plan.TweakFirst,
            steps = plan.Steps.Select(s => new { s.Id, s.ToolId, inputs = s.Inputs, s.Why })
        });

        _planNeedsArtifacts = ShouldGenerateArtifacts(_state.LastIntent);
        _state.GenerateArtifacts = _planNeedsArtifacts;
        _executor.ResetPlan(plan, planJson, _state.LastJobSpecJson ?? "", _state.PendingUserRequest);
        _state.PendingQuestion = null;
        _state.PendingStepIndex = 0;
        _state.PlanConfirmed = true;
        PendingStepChanged?.Invoke("", true);
        EmitToolPlanWait();
    }

    public void ConfirmPlan()
    {
        _state.PlanConfirmed = true;
        EmitToolPlanWait();
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
        _state.Memory.Add("user", text);
        _recentlyCompleted = false;

        var action = UserReplyInterpreter.Interpret(_cfg, _state, text);
        NotifyStatus("Digesting intent");

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
                PendingStepChanged?.Invoke("", false);
                NotifyStatus();
                text = edited;
            }
            else if (action.Action == ReplyActionType.RejectPlan)
            {
                _trace.Emit("[plan:reject] user rejected pending tool plan.");
                _state.ClearPlan();
                PendingStepChanged?.Invoke("", false);
                NotifyStatus();
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
            PendingQuestionChanged?.Invoke(null);
            text = $"{_state.PendingUserRequest}\n\nAdditional detail: {string.Join("\n", _state.ClarificationAnswers)}";
            _trace.Emit("[clarify] received answer, re-running digest.");
        }
        else
        {
            _state.PendingUserRequest = text;
            _state.ClearClarifications();
            _state.PendingQuestion = null;
            PendingQuestionChanged?.Invoke(null);
        }

        var repoScope = RepoScope.Resolve(_cfg);
        _trace.Emit(repoScope.Message);
        if (!repoScope.Ok)
        {
            EmitWaitUser("Set RepoRoot in Settings -> General.RepoRoot, then retry.");
            NotifyStatus("Repo invalid");
            return;
        }

        if (!EnsureProvidersConfigured() || !EnsureToolPromptsReady())
            return;

        RepoScope.CheckGit(repoScope, _trace);

        SyncGraphToHub(_cfg);

        await RunIntentExtractionAsync(text, ct).ConfigureAwait(true);
        await RunSemanticIntentAsync(text, ct).ConfigureAwait(true);

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
            NotifyStatus("Graph invalid");
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
        spec = ValidateJobSpec(spec);

        if (!spec.IsComplete)
        {
            var missing = spec.GetMissingFields();
            missing = missing.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (_state.ClarificationAnswers.Count > 0)
            {
                missing.RemoveAll(m => string.Equals(m, "context", StringComparison.OrdinalIgnoreCase));
            }

            // Special invalid_json case.
            if (missing.Count == 1 && string.Equals(missing[0], "invalid_json", StringComparison.OrdinalIgnoreCase))
            {
                _forceEditBecauseInvalidJson = true;

                EmitWaitUser("I had trouble reading the model output. Please restate what you want.");
                return;
            }

            // Normal WAIT_USER (missing real fields)
            _forceEditBecauseInvalidJson = false;

            var unanswered = missing.Where(m => !_state.ClarificationAsked.ContainsKey(m)).ToList();
            if (unanswered.Count == 0)
            {
                var reminder = _state.PendingQuestion;
                if (string.IsNullOrWhiteSpace(reminder))
                    reminder = "I’m waiting on your answer so I can finish the plan.";

                _trace.Emit("[route:wait_user] JobSpec incomplete; awaiting prior answers for: " + string.Join(",", missing));
                EmitWaitUser(reminder, reminder);
                NotifyStatus("Waiting clarification");
                return;
            }

            var nextConcept = unanswered[0];
            var question = spec.Clarification;
            if (string.IsNullOrWhiteSpace(question) || !_state.AskedMissingFields.Add(nextConcept))
            {
                var qs = ClarificationQuestionBank.PickQuestions(new List<string> { nextConcept }, 1);
                question = qs.FirstOrDefault();
            }

            if (string.IsNullOrWhiteSpace(question))
                question = "Could you share a bit more detail so I can finish the plan?";

            _state.PendingQuestions = new List<string> { question };
            _state.ClarificationAsked[nextConcept] = question;

            _state.PendingQuestion = question;
            PendingQuestionChanged?.Invoke(_state.PendingQuestion);
            _trace.Emit("[route:wait_user] JobSpec incomplete; asking clarifications. missing=" + string.Join(",", missing));
            EmitWaitUser(question, question);
            NotifyStatus("Waiting clarification");
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

        var activeAttachments = GetActiveAttachments();
        if (activeAttachments.Count == 0)
        {
            if (_planNeedsArtifacts && await HandleProgramOnlyFlowAsync(text, rawJson, ct).ConfigureAwait(true))
                return;

            _trace.Emit("[attachments:none] Add at least one attachment to proceed to tool plan.");
            EmitWaitUser("Attach at least one file or image so I can help.");
            NotifyStatus("Waiting attachments");
            return;
        }

        var docAttachments = activeAttachments.Where(a => IsTextualAttachment(a.Kind)).ToList();
        if (docAttachments.Count > 1 && string.IsNullOrWhiteSpace(_state.PendingQuestion) && _state.PendingToolPlan == null && _state.ClarificationAnswers.Count == 0)
        {
            var names = string.Join(", ", docAttachments.Select(a => a.StoredName));
            _state.PendingQuestion = "Which file should I start with? " + names;
            PendingQuestionChanged?.Invoke(_state.PendingQuestion);
            EmitWaitUser("Which file should I start with? " + names);
            return;
        }

        var planOk = await BuildToolPlanAsync(text, rawJson, ct).ConfigureAwait(true);
        if (planOk)
        {
            _state.PlanConfirmed = false;
            EmitToolPlanWait();
        }
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
        NotifyStatus("Running build");

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
        List<string> errorLines = new();
        try
        {
            logs = await client.DownloadLogsAsync(run.Id, ct).ConfigureAwait(false);
            errorLines = ExtractErrorLines(logs);
        }
        catch (Exception ex)
        {
            _trace.Emit("[gh:logs:warn] " + ex.Message);
        }

        var ok = string.Equals(run.Conclusion, "success", StringComparison.OrdinalIgnoreCase);
        var errorMsg = ok
            ? ""
            : (errorLines.Count > 0
                ? string.Join(Environment.NewLine, errorLines.Take(5))
                : $"Build failed: status={run.Status} conclusion={run.Conclusion}");

        NotifyStatus(ok ? "Build succeeded" : "Build failed");
        if (!ok)
            TraceAttentionRequested?.Invoke();
        return new RunnerBuildSummary(ok, run.Id, run.Status, run.Conclusion, run.HtmlUrl, logs, errorMsg);
    }

    public IReadOnlyList<OutputCard> GetOutputCards()
    {
        return _state.OutputCards.Select(c => OutputCard.Truncate(c)).ToList();
    }

    public IReadOnlyList<ProgramArtifactResult> GetArtifacts()
    {
        return _state.ProgramArtifacts.ToList();
    }

    public IReadOnlyList<string> GetArtifactPackages()
    {
        return _state.ArtifactPackages.ToList();
    }

    public object GetPlanSnapshot()
    {
        var steps = _state.PendingToolPlan?.Steps?.Select(s => new
        {
            s.Id,
            s.ToolId,
            Inputs = s.Inputs,
            s.Why
        }).ToList() ?? new List<object>();

        return new
        {
            session = _state.SessionToken,
            steps,
            ready = steps.Count > 0
        };
    }

    public object GetPublicSnapshot()
    {
        return new
        {
            status = ComputeWorkflowPhase(),
            stepIndex = _state.PendingStepIndex,
            steps = _state.PendingToolPlan?.Steps?.Count ?? 0,
            pendingQuestion = _state.PendingQuestion,
            ready = _state.PendingToolPlan != null && _state.PendingStepIndex < (_state.PendingToolPlan?.Steps?.Count ?? 0),
            session = _state.SessionToken
        };
    }

    public string GetSessionToken() => _state.SessionToken;

    public void OverrideSession(string session)
    {
        if (string.IsNullOrWhiteSpace(session)) return;
        _state.SessionToken = session.Trim();
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

    private async Task<IntentExtraction?> RunIntentExtractionAsync(string userText, CancellationToken ct)
    {
        try
        {
            var result = await IntentExtractor.ExtractAsync(_cfg, userText, GetActiveAttachments(), _state.Memory, ct).ConfigureAwait(false);
            if (!result.Ok || result.Extraction == null)
            {
                _trace.Emit("[intent:error] " + result.Error);
                return null;
            }

            _trace.Emit("[intent:raw] " + Trunc(result.Raw, 800));
            _state.LastIntent = result.Extraction;
            _planNeedsArtifacts = ShouldGenerateArtifacts(result.Extraction);
            return result.Extraction;
        }
        catch (Exception ex)
        {
            _trace.Emit("[intent:error] " + ex.Message);
            return null;
        }
    }

    private async Task<SemanticIntent> RunSemanticIntentAsync(string userText, CancellationToken ct)
    {
        try
        {
            var semantic = await SemanticIntentParser.ParseAsync(_cfg, userText, GetActiveAttachments(), _state.Memory, ct).ConfigureAwait(false);
            if (semantic.Ready && semantic.OrderedActions.Count > 0)
            {
                _state.LastIntent = new IntentExtraction("intent.v1", userText, semantic.Goal, semantic.Context, semantic.OrderedActions.ToList(), semantic.Constraints.ToList(), semantic.Clarification, new List<string>(), true);
            }
            return semantic;
        }
        catch (Exception ex)
        {
            _trace.Emit("[intent:error] " + ex.Message);
            return new SemanticIntent("", Array.Empty<string>(), Array.Empty<string>(), "", ex.Message, false);
        }
    }

    private void RecordAssistantMessage(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        _state.Memory.Add("assistant", text);
    }

    private void EmitWaitUser(string reason, string? friendly = null, string? preview = null)
    {
        var msg = WaitUserGate.GateMessage(reason, preview, _cfg.General.ConversationMode);
        _trace.Emit(msg);
        var chat = friendly ?? reason;
        if (!string.IsNullOrWhiteSpace(chat))
        {
            RecordAssistantMessage(chat);
            UserFacingMessage?.Invoke(chat);
        }
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

    private static string NormalizeAttachmentKind(string kind)
    {
        var k = (kind ?? "").ToLowerInvariant();
        return k switch
        {
            "image" => "image",
            "document" => "document",
            "code" => "code",
            _ => "other"
        };
    }

    private static bool IsTextualAttachment(string kind)
    {
        var k = NormalizeAttachmentKind(kind);
        return k is "document" or "code";
    }

    private static string SuggestToolForKind(string kind)
    {
        var k = NormalizeAttachmentKind(kind);
        return k switch
        {
            "image" => "vision.describe.image",
            "document" => "file.read.text",
            "code" => "file.read.text",
            _ => ""
        };
    }

    private static string Trunc(string s, int max)
    {
        s ??= "";
        s = s.Replace("\r", "");
        return s.Length <= max ? s : s.Substring(0, max) + "...";
    }

    private static bool IsWindowsDesktopProject() => true; // RahBuilder.csproj uses Microsoft.NET.Sdk.WindowsDesktop

    private List<AttachmentInbox.AttachmentEntry> GetActiveAttachments()
    {
        return (_attachments ?? Array.Empty<AttachmentInbox.AttachmentEntry>())
            .Where(a => a?.Active != false)
            .Select(a => a!)
            .ToList();
    }

    private void NotifyStatus(string? phaseOverride = null)
    {
        try
        {
            var active = GetActiveAttachments();
            var total = _attachments?.Count ?? 0;
            var stepIndex = _state.PendingStepIndex + 1;
            var stepCount = _state.PendingToolPlan?.Steps?.Count ?? 0;
            var phase = phaseOverride ?? ComputeWorkflowPhase();
            var repo = ShortRepo(_cfg.General.RepoRoot);
            var mode = _cfg.General.ConversationMode.ToString();
            StatusChanged?.Invoke(new WorkflowStatusSnapshot(mode, repo, total, active.Count, phase, stepIndex, stepCount));
        }
        catch { }
    }

    private string ComputeWorkflowPhase()
    {
        if (_recentlyCompleted)
            return "Done";

        if (_state.PendingToolPlan != null)
        {
            var idx = Math.Max(0, _state.PendingStepIndex);
            var count = _state.PendingToolPlan.Steps.Count;
            if (idx >= count)
                return "Done";
            if (_state.ToolOutputs.Count > 0 || idx > 0 || _state.AutoApproveAll)
                return "Tool execution";
            return "Planning";
        }

        if (!string.IsNullOrWhiteSpace(_state.PendingQuestion))
            return "Waiting clarification";

        if (!string.IsNullOrWhiteSpace(_state.PendingUserRequest))
            return "Digesting intent";

        return "Idle";
    }

    private static string ShortRepo(string repoRoot)
    {
        if (string.IsNullOrWhiteSpace(repoRoot)) return "n/a";
        var trimmed = repoRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrWhiteSpace(name) ? repoRoot : name;
    }

    private static string BuildAttachmentSummary(AttachmentInbox.AttachmentEntry att)
    {
        var kind = NormalizeAttachmentKind(att.Kind);
        return kind switch
        {
            "image" => "Image to describe with vision",
            "document" => "Document to read or summarize",
            "code" => "Code file to inspect or summarize",
            _ => "Supporting attachment"
        };
    }

    private static List<string> BuildAttachmentTags(AttachmentInbox.AttachmentEntry att)
    {
        var tags = new List<string>();
        var kind = NormalizeAttachmentKind(att.Kind);
        if (!string.IsNullOrWhiteSpace(kind))
            tags.Add(kind);

        var stem = Path.GetFileNameWithoutExtension(att.OriginalName) ?? "";
        foreach (var token in stem.Split(new[] { ' ', '_', '-', '.', '/' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var t = token.Trim();
            if (t.Length > 0 && !tags.Contains(t, StringComparer.OrdinalIgnoreCase))
                tags.Add(t);
        }

        foreach (var t in SplitCamel(stem))
        {
            if (t.Length > 0 && !tags.Contains(t, StringComparer.OrdinalIgnoreCase))
                tags.Add(t);
        }

        var ext = Path.GetExtension(att.OriginalName);
        if (!string.IsNullOrWhiteSpace(ext))
        {
            var cleaned = ext.Trim('.').ToLowerInvariant();
            if (cleaned.Length > 0 && !tags.Contains(cleaned, StringComparer.OrdinalIgnoreCase))
                tags.Add(cleaned);
        }

        if (!string.IsNullOrWhiteSpace(att.StoredName) && tags.Count == 0)
            tags.Add(att.StoredName);

        return tags;
    }

    private string AppendAttachmentMetadata(string userText)
    {
        var activeAttachments = GetActiveAttachments();
        var sb = new StringBuilder();
        sb.AppendLine("ATTACHMENTS:");
        if (activeAttachments.Count == 0)
        {
            sb.AppendLine("- none");
        }
        else
        {
            foreach (var a in activeAttachments)
            {
                var kind = NormalizeAttachmentKind(a.Kind);
                var suggestedTool = SuggestToolForKind(kind);
                var toolText = string.IsNullOrWhiteSpace(suggestedTool) ? "" : suggestedTool;
                var tags = BuildAttachmentTags(a);
                var summary = BuildAttachmentSummary(a);
                sb.AppendLine($"- storedName: {a.StoredName}, originalName: {a.OriginalName}, kind: {kind}, status: present, summary: {summary}, tags: [{string.Join(", ", tags)}], tools: [{toolText}]");
            }
        }
        sb.AppendLine();
        sb.AppendLine("REQUEST:");
        sb.Append(userText ?? "");

        _trace.Emit($"[digest:attachments] {activeAttachments.Count} attached (active)");
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

    private static List<string> ExtractErrorLines(IReadOnlyList<string> logs)
    {
        var results = new List<string>();
        if (logs == null) return results;

        foreach (var blob in logs)
        {
            if (string.IsNullOrWhiteSpace(blob)) continue;
            var lines = blob.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                if (line.IndexOf(" error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf(":error", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    results.Add(line.Trim());
                    if (results.Count >= 20)
                        return results;
                }
            }
        }

        return results;
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
        var activeAttachments = GetActiveAttachments();
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
        foreach (var a in activeAttachments)
        {
            var kind = NormalizeAttachmentKind(a.Kind);
            var suggestedTool = SuggestToolForKind(kind);
            var toolText = string.IsNullOrWhiteSpace(suggestedTool) ? "n/a" : suggestedTool;
            sb.AppendLine($"- storedName: {a.StoredName}, kind: {kind}, sizeBytes: {a.SizeBytes}, sha256: {a.Sha256}, suggestedTool: {toolText}");
        }

        var constraints = _state.LastIntent?.Constraints ?? new List<string>();
        if (constraints.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("CONSTRAINTS:" + string.Join(" | ", constraints));
        }

        var orderedActions = _state.LastIntent?.Actions ?? new List<string>();
        if (orderedActions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("ORDERED_ACTIONS:" + string.Join(" -> ", orderedActions));
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
        _state.GenerateArtifacts = _planNeedsArtifacts;
        _state.PendingQuestion = null;
        _state.PlanConfirmed = false;
        PlanReady?.Invoke(PlanDefinition.FromToolPlan(_state.SessionToken, plan));
        NotifyStatus("Planning");
        return true;
    }

    private bool ValidateToolPlan(ToolPlan plan)
    {
        if (plan == null || plan.Steps == null || plan.Steps.Count == 0)
            return false;

        var activeAttachments = GetActiveAttachments();
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

            var att = activeAttachments.FirstOrDefault(a => string.Equals(a.StoredName, stored, StringComparison.OrdinalIgnoreCase));
            if (att == null)
            {
                _trace.Emit($"[toolplan:error] attachment not found for storedName: {stored}");
                return false;
            }
        }

        var expectedTools = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var att in activeAttachments)
        {
            var expected = SuggestToolForKind(att.Kind);
            if (!string.IsNullOrWhiteSpace(expected))
                expectedTools[att.StoredName] = expected;
        }

        foreach (var step in plan.Steps)
        {
            if (!step.Inputs.TryGetValue("storedName", out var stored) || string.IsNullOrWhiteSpace(stored))
                continue;

            if (expectedTools.TryGetValue(stored, out var expectedTool) &&
                !step.ToolId.Equals(expectedTool, StringComparison.OrdinalIgnoreCase))
            {
                _trace.Emit($"[toolplan:error] tool mismatch for attachment {stored}: expected {expectedTool}, got {step.ToolId}.");
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

        var planSummary = BuildPlanSummary(_state.PendingToolPlan, _state.PendingStepIndex);
        if (!string.IsNullOrWhiteSpace(planSummary) && _state.PendingStepIndex == 0)
            UserFacingMessage?.Invoke(planSummary);

        var mode = _cfg.General.ConversationMode;
        string stepSummary = "";

        if (mode == ConversationMode.Strict)
        {
            var sb = new StringBuilder();
            sb.AppendLine("ToolPlan Steps:");
            for (int i = 0; i < _state.PendingToolPlan.Steps.Count; i++)
            {
                var step = _state.PendingToolPlan.Steps[i];
                var prefix = i == _state.PendingStepIndex ? "->" : "  ";
                if (i == _state.PendingStepIndex)
                    stepSummary = $"Step {i + 1}/{_state.PendingToolPlan.Steps.Count}: {step.ToolId}";
                sb.AppendLine($"{prefix} Step {i + 1}: {step.ToolId} ({step.Id}) inputs: {string.Join(", ", step.Inputs.Select(kv => $"{kv.Key}={kv.Value}"))} why: {step.Why}");
            }
            EmitWaitUser("Approve tool plan to continue.", "Ready to proceed with the plan?", sb.ToString().TrimEnd());
        }
        else
        {
            var remaining = _state.PendingToolPlan.Steps.Skip(_state.PendingStepIndex).ToList();
            var summary = string.Join(", then ", remaining.Select(DescribeStep));
            if (remaining.Count > 0)
                stepSummary = $"Step {_state.PendingStepIndex + 1}/{_state.PendingToolPlan.Steps.Count}: {DescribeStep(remaining[0])}";
            var question = string.IsNullOrWhiteSpace(summary)
                ? "Ready to proceed with the next step?"
                : $"I can {summary}. Want me to proceed?";
            EmitWaitUser(question, question, string.IsNullOrWhiteSpace(planSummary) ? null : planSummary);
        }

        _state.PendingQuestion = null;
        var panelSummary = string.IsNullOrWhiteSpace(planSummary) ? stepSummary : planSummary;
        if (!string.IsNullOrWhiteSpace(panelSummary))
            PendingStepChanged?.Invoke(panelSummary, true);
        NotifyStatus("Planning");
    }

    private async Task ExecuteCurrentStepAsync(CancellationToken ct, bool approveAll = false)
    {
        if (_state.PendingToolPlan == null)
        {
            EmitWaitUser("No tool plan to execute.");
            NotifyStatus();
            return;
        }
        if (!_state.PlanConfirmed)
        {
            EmitWaitUser("Please confirm the plan before execution.");
            return;
        }

        _state.PlanPaused = false;
        while (true)
        {
            if (_state.PendingStepIndex < 0 || _state.PendingStepIndex >= _state.PendingToolPlan.Steps.Count)
            {
                EmitWaitUser("Tool plan step index invalid.");
                _state.ClearPlan();
                NotifyStatus();
                PendingStepChanged?.Invoke("", false);
                return;
            }

            var step = _state.PendingToolPlan.Steps[_state.PendingStepIndex];
            NotifyStatus("Tool execution");
            var result = await _executor.RunNextAsync(_cfg, _attachments, _toolManifest, _toolPrompts, ct).ConfigureAwait(false);
            if (!result.Ok)
            {
                var errorCard = new OutputCard
                {
                    Kind = OutputCardKind.Error,
                    Title = "Step failed",
                    Summary = result.Message,
                    Preview = result.Message,
                    FullContent = result.Message,
                    Tags = new[] { "error" },
                    ToolId = step.ToolId,
                    CreatedUtc = DateTimeOffset.UtcNow
                };
                _state.OutputCards.Add(errorCard);
                OutputCardProduced?.Invoke(errorCard);
                EmitWaitUser(result.Message, "Something went wrong. Want me to retry this step?");
                NotifyStatus("Tool execution");
                TraceAttentionRequested?.Invoke();
                return;
            }

            SendChatStepSummary(result);

            if (_state.PendingToolPlan == null || _state.PendingStepIndex >= _state.PendingToolPlan.Steps.Count)
            {
                await ProduceFinalResponseAsync(ct).ConfigureAwait(false);
                _state.ClearPlan();
                _planNeedsArtifacts = false;
                NotifyStatus();
                PendingStepChanged?.Invoke("", false);
                return;
            }

            if (!(approveAll || _state.AutoApproveAll))
            {
                NotifyStatus();
                EmitToolPlanWait();
                return;
            }
        }
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
        var activeAttachments = GetActiveAttachments();
        foreach (var a in activeAttachments)
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

        await MaybeGenerateArtifactsAsync(ct).ConfigureAwait(false);

        resp = EnsureNarrative(resp);
        _trace.Emit("[final] " + Trunc(resp, 1200));
        RecordAssistantMessage(resp);
        UserFacingMessage?.Invoke(resp);
        var finalCard = new OutputCard
        {
            Kind = OutputCardKind.Final,
            Title = "Final answer",
            Summary = Trunc(resp, 200),
            Preview = Trunc(resp, 600),
            FullContent = resp,
            Tags = new[] { "final" },
            CreatedUtc = DateTimeOffset.UtcNow,
            ToolId = "orchestrator"
        };
        _state.OutputCards.Add(finalCard);
        OutputCardProduced?.Invoke(finalCard);
        _recentlyCompleted = true;
        _state.PendingUserRequest = "";
        _state.ClearClarifications();
        NotifyStatus("Done");
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

    private static bool ShouldGenerateArtifacts(IntentExtraction? intent)
    {
        if (intent == null) return false;
        var goal = (intent.Goal ?? "").ToLowerInvariant();
        if (goal.Contains("build") || goal.Contains("app") || goal.Contains("program"))
            return true;

        foreach (var action in intent.Actions ?? Array.Empty<string>())
        {
            var a = (action ?? "").ToLowerInvariant();
            if (a.Contains("build") || a.Contains("create") || a.Contains("generate"))
                return true;
        }

        return false;
    }

    private async Task MaybeGenerateArtifactsAsync(CancellationToken ct)
    {
        if (!_state.GenerateArtifacts) return;
        _state.GenerateArtifacts = false;

        var intent = _state.LastIntent ?? new IntentExtraction("program_artifacts.v1", _state.PendingUserRequest, _state.PendingUserRequest, "", new List<string>(), new List<string>(), "", new List<string>(), true);
        var repo = RepoScope.Resolve(_cfg);
        if (!repo.Ok)
        {
            _trace.Emit("[program:skip] repo invalid for artifact generation.");
            return;
        }

        var spec = _state.LastJobSpecJson != null ? JobSpecParser.TryParse(_state.LastJobSpecJson) : null;
        spec ??= JobSpec.Invalid("missing_jobspec");

        var result = await _artifactGenerator.BuildProjectArtifacts(spec, _state.ToolOutputs, _cfg, GetActiveAttachments(), repo.RepoRoot, _state.SessionToken, ct).ConfigureAwait(false);
        if (!result.Ok)
        {
            _trace.Emit("[program:error] " + result.Message);
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.ZipPath))
            _state.ArtifactPackages.Add(result.ZipPath);
        _state.ProgramArtifacts.Add(result);

        if (_state.ProgramArtifacts.Any(p => string.Equals(p.Hash, result.Hash, StringComparison.OrdinalIgnoreCase)))
            return;

        EmitArtifactCards(result);
    }

    private void EmitArtifactCards(ProgramArtifactResult result)
    {
        if (result == null) return;

        var summaryCard = new OutputCard
        {
            Kind = OutputCardKind.Program,
            Title = "Program artifacts",
            Summary = $"Generated {result.Files.Count} files (hash {result.Hash[..Math.Min(12, result.Hash.Length)]})",
            Preview = string.Join("\n", result.Files.Take(5).Select(f => $"- {f.Path}")),
            FullContent = $"Folder: {result.Folder}\nZip: {result.ZipPath}\nHash: {result.Hash}\n\nFiles:\n{result.TreePreview}",
            Tags = new[] { "program", "artifact" },
            CreatedUtc = DateTimeOffset.UtcNow,
            Metadata = result.ZipPath,
            DownloadUrl = BuildDownloadUrl(result.Hash)
        };
        _state.OutputCards.Add(summaryCard);
        OutputCardProduced?.Invoke(summaryCard);

        var treeCard = new OutputCard
        {
            Kind = OutputCardKind.ProgramTree,
            Title = "Project tree",
            Summary = $"Files: {result.Files.Count}",
            Preview = result.TreePreview,
            FullContent = result.TreePreview,
            Tags = new[] { "tree", "artifact" },
            CreatedUtc = DateTimeOffset.UtcNow,
            Metadata = result.ZipPath,
            DownloadUrl = BuildDownloadUrl(result.Hash)
        };
        _state.OutputCards.Add(treeCard);
        OutputCardProduced?.Invoke(treeCard);

        foreach (var preview in result.Previews)
        {
            var fileCard = new OutputCard
            {
                Kind = OutputCardKind.ProgramFile,
                Title = preview.Path,
                Summary = $"{preview.LineCount} lines ({preview.ContentType})",
                Preview = preview.Preview,
                FullContent = preview.Preview,
                Tags = preview.Tags.Count > 0 ? preview.Tags : new[] { "code", "generated" },
                CreatedUtc = DateTimeOffset.UtcNow,
                Metadata = result.ZipPath,
                DownloadUrl = BuildDownloadUrl(result.Hash)
            };
            _state.OutputCards.Add(fileCard);
            OutputCardProduced?.Invoke(fileCard);
        }

        if (!string.IsNullOrWhiteSpace(result.ZipPath))
        {
            var zipCard = new OutputCard
            {
                Kind = OutputCardKind.ProgramZip,
                Title = "Download ZIP",
                Summary = Path.GetFileName(result.ZipPath),
                Preview = "Download generated artifacts.",
                FullContent = $"Path: {result.ZipPath}",
                Tags = new[] { "zip", "artifact" },
                CreatedUtc = DateTimeOffset.UtcNow,
                Metadata = result.ZipPath,
                DownloadUrl = BuildDownloadUrl(result.Hash)
            };
            _state.OutputCards.Add(zipCard);
            OutputCardProduced?.Invoke(zipCard);
        }

        _state.ProgramArtifacts.Add(result);
        if (_state.OutputCards.All(c => c.Kind != OutputCardKind.Final))
        {
            var summary = $"Generated {result.Files.Count} files across {result.Files.Select(f => Path.GetDirectoryName(f.Path) ?? \"\").Distinct(StringComparer.OrdinalIgnoreCase).Count()} folders; zip ready at {result.ZipPath}.";
            var summaryCardFinal = new OutputCard
            {
                Kind = OutputCardKind.Final,
                Title = "Artifact summary",
                Summary = summary,
                Preview = summary,
                FullContent = summary,
                Tags = new[] { "summary", "artifact" },
                CreatedUtc = DateTimeOffset.UtcNow,
                Metadata = result.ZipPath,
                DownloadUrl = BuildDownloadUrl(result.Hash)
            };
            _state.OutputCards.Add(summaryCardFinal);
            OutputCardProduced?.Invoke(summaryCardFinal);
        }
    }

    private string BuildDownloadUrl(string hash) => $"/api/artifacts/download?session={_state.SessionToken}&hash={hash}";

    private async Task<bool> HandleProgramOnlyFlowAsync(string userText, string jobSpecJson, CancellationToken ct)
    {
        if (!_planNeedsArtifacts)
            return false;

        _trace.Emit("[program] No attachments required; generating program artifacts.");
        _state.LastJobSpecJson = jobSpecJson;
        _state.OriginalUserText = userText;
        _state.ToolOutputs = new List<JsonElement>();
        _state.GenerateArtifacts = true;
        await MaybeGenerateArtifactsAsync(ct).ConfigureAwait(false);
        await ProduceFinalResponseAsync(ct).ConfigureAwait(false);
        _state.ClearPlan();
        _planNeedsArtifacts = false;
        PendingStepChanged?.Invoke("", false);
        NotifyStatus("Done");
        return true;
    }

    private static string EnsureNarrative(string resp)
    {
        var text = (resp ?? "").Trim();
        if (text.StartsWith("```"))
        {
            var trimmed = text.Trim('`', '\n', '\r');
            text = trimmed;
        }

        if (text.StartsWith("{", StringComparison.Ordinal) && text.EndsWith("}", StringComparison.Ordinal))
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                var sb = new System.Text.StringBuilder();
                foreach (var prop in doc.RootElement.EnumerateObject())
                    sb.AppendLine($"- {prop.Name}: {prop.Value.GetRawText()}");
                text = sb.ToString().TrimEnd();
            }
            catch
            {
                // keep raw text
            }
        }

        return text;
    }

    private JobSpec ValidateJobSpec(JobSpec spec)
    {
        if (spec == null) return JobSpec.Invalid("invalid_spec");

        var intent = _state.LastIntent;
        var missing = spec.GetMissingFields();
        var request = (spec.Request ?? intent?.Request ?? "").Trim();
        var goal = (spec.Goal ?? intent?.Goal ?? "").Trim();
        var context = string.IsNullOrWhiteSpace(spec.Context) ? intent?.Context ?? "" : spec.Context;
        if (string.IsNullOrWhiteSpace(context))
        {
            var mem = _state.Memory.Summarize(4);
            if (!string.IsNullOrWhiteSpace(mem))
                context = mem;
        }
        var actions = spec.Actions?.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).ToList() ?? new List<string>();
        if (actions.Count == 0 && intent?.Actions != null)
            actions = intent.Actions.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).ToList();
        var constraints = spec.Constraints?.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList() ?? new List<string>();
        if (constraints.Count == 0 && intent?.Constraints != null)
            constraints = intent.Constraints.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList();
        var attachments = spec.Attachments?.Where(a => a != null && !string.IsNullOrWhiteSpace(a.StoredName)).ToList() ?? new List<JobSpecAttachment>();

        if (string.IsNullOrWhiteSpace(request))
            AddMissing("request", missing);
        else
            _state.ConfirmedFields.Add("request");

        if (string.IsNullOrWhiteSpace(goal))
            AddMissing("goal", missing);
        else
            _state.ConfirmedFields.Add("goal");

        if (actions.Count == 0)
            AddMissing("actions", missing);
        else
            _state.ConfirmedFields.Add("actions");

        if (constraints.Count == 0)
            AddMissing("constraints", missing);
        else
            _state.ConfirmedFields.Add("constraints");

        if (attachments.Count == 0)
            AddMissing("attachments", missing);

        foreach (var att in attachments)
        {
            var kind = NormalizeAttachmentKind(att.Kind);
            if (string.IsNullOrWhiteSpace(att.StoredName) || string.IsNullOrWhiteSpace(kind))
            {
                AddMissing("attachments", missing);
                continue;
            }

            if (att.Tools == null || att.Tools.Count == 0)
            {
                AddMissing("attachments", missing);
                continue;
            }

            var expectedTool = SuggestToolForKind(kind);
            if (string.IsNullOrWhiteSpace(expectedTool))
                AddMissing("attachments", missing);
            else if (!att.Tools.Contains(expectedTool, StringComparer.OrdinalIgnoreCase))
                AddMissing("attachments", missing);
        }

        var ready = spec.Ready && missing.Count == 0;

        return JobSpec.FromJson(
            spec.Raw,
            string.IsNullOrWhiteSpace(spec.Mode) ? "jobspec.v2" : spec.Mode,
            request,
            goal,
            context,
            actions,
            constraints,
            attachments,
            spec.Clarification,
            ready,
            missing);
    }

    private static void AddMissing(string field, List<string> missing)
    {
        if (missing == null) return;
        if (missing.Contains(field, StringComparer.OrdinalIgnoreCase)) return;
        missing.Add(field);
    }

    private string BuildPlanSummary(ToolPlan plan, int currentStepIndex)
    {
        if (plan == null || plan.Steps == null || plan.Steps.Count == 0)
            return "";

        var sb = new StringBuilder();
        sb.AppendLine("Plan ready. I will:");
        for (int i = 0; i < plan.Steps.Count; i++)
        {
            var marker = i == currentStepIndex ? "->" : "-";
            sb.AppendLine($"{marker} {StepPhrase(i, plan.Steps[i])}");
        }
        sb.AppendLine("Use Run All to execute, Run Next for one step, Modify Plan to edit, or Stop to cancel.");
        return sb.ToString().TrimEnd();
    }

    private static string StepPhrase(int index, ToolPlanStep step)
    {
        var action = DescribeStep(step);
        return $"Step {index + 1}: {action}";
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
                var preview = content.Length <= 240 ? content : content.Substring(0, 240) + "...";
                var full = content.Length > 1200 ? content.Substring(0, 1200) + "…" : content;
                var bullets = content.Replace("\r", "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Take(3).Select(l => "- " + l).ToList();
                var card = new StringBuilder();
                card.AppendLine("┌─ File result");
                card.AppendLine($"│ Name: {name}");
                card.AppendLine($"│ Characters: {content.Length}");
                if (bullets.Count > 0)
                {
                    card.AppendLine("├─ Summary");
                    foreach (var b in bullets)
                        card.AppendLine("│ " + b);
                }
                card.AppendLine("├─ Preview");
                card.AppendLine(IndentMultiline(preview, "│ "));
                card.AppendLine("├─ Full content");
                card.AppendLine(IndentMultiline(full, "│ "));
                card.AppendLine("└ Use Run Next to continue or Edit Plan to adjust the plan.");
                var msg = card.ToString().TrimEnd();
                RecordAssistantMessage(msg);
                UserFacingMessage?.Invoke(msg);
            }
            else if (toolId.Equals("vision.describe.image", StringComparison.OrdinalIgnoreCase))
            {
                var name = root.TryGetProperty("storedName", out var n) ? n.GetString() : "";
                var caption = root.TryGetProperty("caption", out var cap) ? cap.GetString() : "";
                var tags = root.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array
                    ? string.Join(", ", tagsEl.EnumerateArray().Where(t => t.ValueKind == JsonValueKind.String).Select(t => t.GetString()).Where(s => !string.IsNullOrWhiteSpace(s)))
                    : "";
                var card = new StringBuilder();
                card.AppendLine("┌─ Vision result");
                card.AppendLine($"│ Image: {name}");
                if (!string.IsNullOrWhiteSpace(caption))
                    card.AppendLine("│ Caption: " + caption);
                if (!string.IsNullOrWhiteSpace(tags))
                    card.AppendLine("│ Tags: " + tags);
                card.AppendLine("└ Use Run Next to continue or Edit Plan to adjust the plan.");
                var msg = card.ToString().TrimEnd();
                RecordAssistantMessage(msg);
                UserFacingMessage?.Invoke(msg);
            }
        }
        catch { }
    }

    private static string IndentMultiline(string text, string prefix)
    {
        var lines = (text ?? "").Replace("\r", "").Split('\n');
        var sb = new StringBuilder();
        foreach (var line in lines)
            sb.AppendLine(prefix + line);
        return sb.ToString().TrimEnd();
    }

    private static List<string> SplitCamel(string text)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return list;

        var sb = new StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsUpper(ch) && sb.Length > 0)
            {
                list.Add(sb.ToString());
                sb.Clear();
            }
            sb.Append(ch);
        }

        if (sb.Length > 0)
            list.Add(sb.ToString());

        return list;
    }
}
