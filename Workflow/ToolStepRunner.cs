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
using RahOllamaOnly.Tools;
using RahOllamaOnly.Tools.Prompt;
using RahOllamaOnly.Tracing;

namespace RahBuilder.Workflow;

public sealed record ToolRunResult(bool Ok, string Message, JsonElement? Output = null);

public static class ToolStepRunner
{
    private static readonly string[] AllowedTools = { "file.read.text", "vision.describe.image" };

    public static async Task<ToolRunResult> RunAsync(
        ToolPlanStep step,
        AppConfig cfg,
        RunTrace trace,
        IReadOnlyList<AttachmentInbox.AttachmentEntry> attachments,
        ToolManifest manifest,
        ToolPromptRegistry prompts,
        CancellationToken ct)
    {
        if (step == null) return new ToolRunResult(false, "[tool:error] step is null.");
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        if (trace == null) throw new ArgumentNullException(nameof(trace));
        manifest ??= new ToolManifest();
        prompts ??= new ToolPromptRegistry();

        if (!AllowedTools.Contains(step.ToolId, StringComparer.OrdinalIgnoreCase))
            return new ToolRunResult(false, $"[tool:error] tool not allowed: {step.ToolId}");

        if (!manifest.Has(step.ToolId))
            return new ToolRunResult(false, $"[tool:error] tool not in manifest: {step.ToolId}");
        if (!prompts.Has(step.ToolId))
            return new ToolRunResult(false, $"[tool:error] tool prompt missing: {step.ToolId}");

        if (!step.Inputs.TryGetValue("storedName", out var storedName) || string.IsNullOrWhiteSpace(storedName))
            return new ToolRunResult(false, "[tool:error] inputs.storedName is required.");

        var attachment = attachments.FirstOrDefault(a => string.Equals(a.StoredName, storedName, StringComparison.OrdinalIgnoreCase));
        if (attachment == null)
            return new ToolRunResult(false, $"[tool:error] attachment not found: {storedName}");

        var info = new FileInfo(attachment.HostPath);
        if (!info.Exists)
            return new ToolRunResult(false, $"[tool:error] attachment file missing on disk: {attachment.HostPath}");
        if (info.Length > cfg.General.MaxAttachmentBytes)
            return new ToolRunResult(false, $"[tool:error] attachment exceeds size limit: {info.Length} bytes");

        if (step.ToolId.Equals("file.read.text", StringComparison.OrdinalIgnoreCase))
            return await RunFileReadAsync(attachment, trace, ct).ConfigureAwait(false);

        if (step.ToolId.Equals("vision.describe.image", StringComparison.OrdinalIgnoreCase))
            return await RunVisionAsync(attachment, cfg, trace, ct).ConfigureAwait(false);

        return new ToolRunResult(false, $"[tool:error] unsupported tool: {step.ToolId}");
    }

    private static async Task<ToolRunResult> RunFileReadAsync(AttachmentInbox.AttachmentEntry att, RunTrace trace, CancellationToken ct)
    {
        try
        {
            using var fs = new FileStream(att.HostPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new StreamReader(fs, Encoding.UTF8);
            var content = await reader.ReadToEndAsync().ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            var payload = JsonSerializer.SerializeToElement(new
            {
                toolId = "file.read.text",
                storedName = att.StoredName,
                sha256 = att.Sha256,
                content
            });

            trace.Emit($"[tool:ok] file.read.text {att.StoredName} bytes={content.Length}");
            return new ToolRunResult(true, "[tool:ok] file.read.text", payload);
        }
        catch (Exception ex)
        {
            return new ToolRunResult(false, "[tool:error] file.read.text failed: " + ex.Message);
        }
    }

    private static async Task<ToolRunResult> RunVisionAsync(AttachmentInbox.AttachmentEntry att, AppConfig cfg, RunTrace trace, CancellationToken ct)
    {
        if (cfg.Orchestrator?.Roles == null || cfg.Orchestrator.Roles.Count == 0)
            return new ToolRunResult(false, "[tool:error] orchestrator roles not configured.");

        var visionRole = cfg.Orchestrator.Roles.Find(r => string.Equals(r.Role, "Vision", StringComparison.OrdinalIgnoreCase));
        if (visionRole == null || string.IsNullOrWhiteSpace(visionRole.Provider) || string.IsNullOrWhiteSpace(visionRole.Model))
            return new ToolRunResult(false, "[tool:error] Vision role not configured (provider/model missing).");

        try
        {
            var bytes = await File.ReadAllBytesAsync(att.HostPath, ct).ConfigureAwait(false);
            var base64 = Convert.ToBase64String(bytes);

            var systemPrompt = "You are the Vision tool. Respond ONLY with JSON: {\"caption\":\"...\",\"tags\":[\"...\"]}. Keep tags short.";

            var userText = $"storedName: {att.StoredName}\nsha256: {att.Sha256}\nbase64: {base64}";

            var resp = await LlmInvoker.InvokeChatAsync(cfg, "Vision", systemPrompt, userText, ct, Array.Empty<string>(), "vision.describe.image").ConfigureAwait(false);
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(resp);
            }
            catch
            {
                return new ToolRunResult(false, "[tool:error] vision.describe.image returned non-JSON.");
            }

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return new ToolRunResult(false, "[tool:error] vision.describe.image JSON not object.");

            if (!doc.RootElement.TryGetProperty("caption", out var captionEl) || captionEl.ValueKind != JsonValueKind.String)
                return new ToolRunResult(false, "[tool:error] vision.describe.image missing caption.");

            var tags = new List<string>();
            if (doc.RootElement.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in tagsEl.EnumerateArray())
                {
                    if (t.ValueKind == JsonValueKind.String)
                    {
                        var s = t.GetString();
                        if (!string.IsNullOrWhiteSpace(s))
                            tags.Add(s.Trim());
                    }
                }
            }

            var payload = JsonSerializer.SerializeToElement(new
            {
                toolId = "vision.describe.image",
                storedName = att.StoredName,
                sha256 = att.Sha256,
                caption = captionEl.GetString() ?? "",
                tags
            });

            trace.Emit($"[tool:ok] vision.describe.image {att.StoredName}");
            return new ToolRunResult(true, "[tool:ok] vision.describe.image", payload);
        }
        catch (Exception ex)
        {
            return new ToolRunResult(false, "[tool:error] vision.describe.image failed: " + ex.Message);
        }
    }
}
