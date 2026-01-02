#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RahBuilder.Settings;
using RahOllamaOnly.Tools;

namespace RahBuilder.Workflow;

public sealed record IntentExtraction(
    string Mode,
    string Request,
    string Goal,
    string Context,
    IReadOnlyList<string> Actions,
    IReadOnlyList<string> Constraints,
    string Clarification,
    IReadOnlyList<string> Missing,
    bool Ready);

public sealed record IntentExtractionResult(bool Ok, IntentExtraction? Extraction, string Raw, string Error)
{
    public static IntentExtractionResult Failure(string error, string raw = "") => new(false, null, raw, error);
    public static IntentExtractionResult Success(IntentExtraction extraction, string raw) => new(true, extraction, raw, "");
}

public static class IntentExtractor
{
    public static async Task<IntentExtractionResult> ExtractAsync(
        AppConfig cfg,
        string userText,
        IReadOnlyList<AttachmentInbox.AttachmentEntry> attachments,
        ChatMemory memory,
        CancellationToken ct)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        userText ??= "";
        attachments ??= Array.Empty<AttachmentInbox.AttachmentEntry>();
        memory ??= new ChatMemory();

        var prompt = (cfg.General.IntentExtractorPrompt ?? "").Trim();
        if (prompt.Length == 0)
            return IntentExtractionResult.Failure("intent_prompt_missing");

        var sb = new StringBuilder();
        sb.AppendLine("ATTACHMENTS:");
        if (attachments.Count == 0)
        {
            sb.AppendLine("- none");
        }
        else
        {
            foreach (var a in attachments.Where(a => a != null))
                sb.AppendLine($"- storedName: {a.StoredName}, kind: {a.Kind}, summary: {a.OriginalName}");
        }
        sb.AppendLine();
        var history = memory.Summarize(6);
        if (!string.IsNullOrWhiteSpace(history))
        {
            sb.AppendLine("RECENT_MEMORY:");
            sb.AppendLine(history);
            sb.AppendLine();
        }
        sb.AppendLine("USER_TEXT:");
        sb.AppendLine(userText);

        var combined = sb.ToString();

        string raw;
        try
        {
            raw = await LlmInvoker.InvokeChatAsync(cfg, "Orchestrator", prompt, combined, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return IntentExtractionResult.Failure("intent_invoke_failed: " + ex.Message);
        }

        var parsed = IntentExtractionParser.TryParse(raw);
        if (parsed == null)
        {
            var repairPrompt = prompt + "\nReturn ONLY JSON for the schema; fix formatting.";
            try
            {
                raw = await LlmInvoker.InvokeChatAsync(cfg, "Orchestrator", repairPrompt, combined, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return IntentExtractionResult.Failure("intent_retry_failed: " + ex.Message, raw);
            }
            parsed = IntentExtractionParser.TryParse(raw);
            if (parsed == null)
                return IntentExtractionResult.Failure("intent_invalid_json", raw);
        }

        var enriched = IntentExtractionParser.EnrichActions(parsed, userText);
        return IntentExtractionResult.Success(enriched, raw);
    }
}

internal static class IntentExtractionParser
{
    public static IntentExtraction? TryParse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            var mode = ReadString(root, "mode", "intent.v1");
            var request = ReadString(root, "request", "");
            var goal = ReadString(root, "goal", "");
            var context = ReadString(root, "context", "");
            var actions = ReadArray(root, "actions");
            var constraints = ReadArray(root, "constraints");
            var clarification = ReadString(root, "clarification", "");
            var missing = ReadArray(root, "missing");
            var ready = root.TryGetProperty("ready", out var readyEl) && readyEl.ValueKind == JsonValueKind.True;
            if (!ready && root.TryGetProperty("state", out var state) && state.ValueKind == JsonValueKind.Object)
            {
                ready = state.TryGetProperty("ready", out var s) && s.ValueKind == JsonValueKind.True;
                var altMissing = ReadArray(state, "missing");
                if (altMissing.Count > 0)
                    missing = altMissing;
            }

            return new IntentExtraction(
                mode,
                request,
                goal,
                context,
                actions,
                constraints,
                clarification,
                missing,
                ready);
        }
        catch
        {
            return null;
        }
    }

    public static IntentExtraction EnrichActions(IntentExtraction extraction, string userText)
    {
        var actions = new List<string>(extraction.Actions ?? Array.Empty<string>());
        foreach (var token in SplitByConnectors(userText))
        {
            var cleaned = token.Trim();
            if (cleaned.Length == 0) continue;
            if (!actions.Contains(cleaned, StringComparer.OrdinalIgnoreCase))
                actions.Add(cleaned);
        }

        var ready = extraction.Ready && (extraction.Missing?.Count ?? 0) == 0 && actions.Count > 0 && extraction.Goal.Trim().Length > 0;
        return new IntentExtraction(
            extraction.Mode,
            extraction.Request,
            extraction.Goal,
            extraction.Context,
            actions,
            extraction.Constraints ?? Array.Empty<string>(),
            extraction.Clarification ?? "",
            extraction.Missing ?? Array.Empty<string>(),
            ready);
    }

    private static List<string> SplitByConnectors(string text)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
            return list;

        var connectors = new[] { " then ", " after ", " also ", " next ", " and then " };
        var lower = " " + text.ToLowerInvariant() + " ";
        foreach (var c in connectors)
        {
            var idx = lower.IndexOf(c, StringComparison.Ordinal);
            if (idx >= 0)
            {
                var parts = text.Split(new[] { "then", "after", "also", "next" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var p in parts)
                {
                    var s = p.Trim();
                    if (s.Length > 0)
                        list.Add(s);
                }
                return list;
            }
        }

        return list;
    }

    private static string ReadString(JsonElement obj, string name, string fallback)
    {
        if (obj.ValueKind != JsonValueKind.Object) return fallback;
        if (!obj.TryGetProperty(name, out var el)) return fallback;
        return el.ValueKind == JsonValueKind.String ? (el.GetString() ?? fallback) : fallback;
    }

    private static List<string> ReadArray(JsonElement obj, string name)
    {
        var list = new List<string>();
        if (obj.ValueKind != JsonValueKind.Object) return list;
        if (!obj.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Array) return list;
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
}
