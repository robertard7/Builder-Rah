#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RahBuilder.Settings;
using RahOllamaOnly.Tools;
using RahOllamaOnly.Tools.Prompt;
using RahOllamaOnly.Tracing;

namespace RahBuilder.Workflow;

public sealed class ExecutionOrchestrator
{
    private readonly WorkflowState _state;
    private readonly RunTrace _trace;

    public event Action<OutputCard>? OutputCardProduced;
    public event Action<string>? UserFacingMessage;

    public ExecutionOrchestrator(WorkflowState state, RunTrace trace)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _trace = trace ?? throw new ArgumentNullException(nameof(trace));
    }

    public void ResetPlan(ToolPlan plan, string planJson, string jobSpecJson, string userText)
    {
        _state.PendingToolPlanJson = planJson;
        _state.PendingToolPlan = plan;
        _state.PendingStepIndex = 0;
        _state.ToolOutputs = new List<JsonElement>();
        _state.LastJobSpecJson = jobSpecJson;
        _state.OriginalUserText = userText;
        _state.PlanPaused = false;
    }

    public void Pause() => _state.PlanPaused = true;
    public void Resume() => _state.PlanPaused = false;

    public async Task<ToolRunResult> RunNextAsync(
        AppConfig cfg,
        IReadOnlyList<AttachmentInbox.AttachmentEntry> attachments,
        ToolManifest manifest,
        ToolPromptRegistry prompts,
        CancellationToken ct)
    {
        if (_state.PendingToolPlan == null)
            return new ToolRunResult(false, "[tool:error] no plan active");

        if (_state.PlanPaused)
            return new ToolRunResult(false, "[tool:paused] plan is paused");

        if (_state.PendingStepIndex < 0 || _state.PendingStepIndex >= _state.PendingToolPlan.Steps.Count)
            return new ToolRunResult(false, "[tool:error] invalid step index");

        var step = _state.PendingToolPlan.Steps[_state.PendingStepIndex];
        if (!manifest.Has(step.ToolId))
        {
            var known = string.Join(", ", manifest.ToolsById.Keys.OrderBy(id => id));
            _trace.Emit($"[tool:error] unknown toolId '{step.ToolId}' at step {_state.PendingStepIndex + 1}. Known tools: {known}");
            return new ToolRunResult(false, "Unknown toolId in tool plan. Please regenerate the plan.");
        }

        var result = await ToolStepRunner.RunAsync(step, cfg, _trace, attachments, manifest, prompts, ct).ConfigureAwait(false);
        if (!result.Ok)
            return result;

        if (result.Output.HasValue)
        {
            _state.ToolOutputs.Add(result.Output.Value);
            try
            {
                _state.MemoryStore.AddToolOutput(result.Output.Value.GetRawText());
            }
            catch { }
            var card = BuildOutputCard(result.Output.Value);
            if (card != null)
            {
                _state.OutputCards.Add(card);
                OutputCardProduced?.Invoke(card);
                UserFacingMessage?.Invoke(card.ToDisplayText());
            }
        }

        _state.PendingStepIndex++;
        return result;
    }

    private OutputCard? BuildOutputCard(JsonElement root)
    {
        if (!root.TryGetProperty("toolId", out var toolEl) || toolEl.ValueKind != JsonValueKind.String)
            return null;

        var toolId = toolEl.GetString() ?? "";
        if (toolId.Equals("file.read.text", StringComparison.OrdinalIgnoreCase))
        {
            var name = root.TryGetProperty("storedName", out var n) ? n.GetString() ?? "" : "";
            var content = root.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
            var preview = content.Length <= 400 ? content : content.Substring(0, 400) + "…";
            var full = content.Length > 4000 ? content.Substring(0, 4000) + "…" : content;
            return new OutputCard
            {
                Kind = OutputCardKind.File,
                Title = "File read: " + name,
                Summary = $"{content.Length} characters read",
                Preview = preview,
                FullContent = full,
                RelatedAttachment = name,
                Tags = new[] { "file", "text" },
                ToolId = toolId,
                CreatedUtc = DateTimeOffset.UtcNow,
                Metadata = "sha256=" + (root.TryGetProperty("sha256", out var sh) ? sh.GetString() ?? "" : "")
            };
        }

        if (toolId.Equals("vision.describe.image", StringComparison.OrdinalIgnoreCase))
        {
            var name = root.TryGetProperty("storedName", out var n) ? n.GetString() ?? "" : "";
            var caption = root.TryGetProperty("caption", out var cap) ? cap.GetString() ?? "" : "";
            var tags = new List<string>();
            if (root.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
            {
                tags = tagsEl.EnumerateArray().Where(t => t.ValueKind == JsonValueKind.String).Select(t => t.GetString() ?? "").Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
            }

            var summary = string.IsNullOrWhiteSpace(caption) ? "Vision result" : caption;
            return new OutputCard
            {
                Kind = OutputCardKind.Vision,
                Title = "Image description: " + name,
                Summary = summary,
                Preview = caption,
                FullContent = caption,
                RelatedAttachment = name,
                Tags = tags,
                ToolId = toolId,
                CreatedUtc = DateTimeOffset.UtcNow
            };
        }

        return null;
    }
}
