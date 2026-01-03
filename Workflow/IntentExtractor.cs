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
        ConversationMemory conversation,
        CancellationToken ct)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        userText ??= "";
        attachments ??= Array.Empty<AttachmentInbox.AttachmentEntry>();
        memory ??= new ChatMemory();
        conversation ??= new ConversationMemory();

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
        var history = memory.Summarize(8);
        if (!string.IsNullOrWhiteSpace(history))
        {
            sb.AppendLine("RECENT_MEMORY:");
            sb.AppendLine(history);
            sb.AppendLine();
        }
        if (conversation.ClarificationAnswers.Count > 0)
        {
            sb.AppendLine("CLARIFICATIONS_ANSWERED:");
            foreach (var kv in conversation.ClarificationAnswers)
                sb.AppendLine($"- {kv.Key}: {kv.Value}");
            sb.AppendLine();
        }
        if (conversation.PlanNotes.Count > 0)
        {
            sb.AppendLine("PLAN_NOTES:");
            foreach (var note in conversation.PlanNotes)
                sb.AppendLine("- " + note);
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
            var mapped = MapAction(cleaned);
            if (!actions.Contains(mapped, StringComparer.OrdinalIgnoreCase))
                actions.Add(mapped);
        }

        foreach (var inferred in InferSemanticActions(userText))
        {
            if (!actions.Contains(inferred, StringComparer.OrdinalIgnoreCase))
                actions.Add(inferred);
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

    internal static List<string> SplitByConnectors(string text)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
            return list;

        var connectors = new[] { " then ", " after ", " also ", " next ", " and then ", " followed by ", ";", "." };
        var lower = " " + text.ToLowerInvariant() + " ";
        foreach (var c in connectors)
        {
            if (lower.Contains(c, StringComparison.Ordinal))
            {
                var parts = text.Split(new[] { "then", "after", "also", "next", "and then", "followed by", ";", "." }, StringSplitOptions.RemoveEmptyEntries);
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

    internal static string MapAction(string raw)
    {
        var text = (raw ?? "").Trim();
        var lower = text.ToLowerInvariant();
        if (lower.Contains("describe") && lower.Contains("image"))
            return "vision.describe.image";
        if (lower.Contains("caption"))
            return "vision.describe.image";
        if (lower.Contains("web") && lower.Contains("server"))
            return "code.generate.webserver";
        if (lower.Contains("rest") && lower.Contains("api"))
            return "code.generate.api";
        if (lower.Contains("api") && lower.Contains("tests"))
            return "code.test.generate";
        if (lower.Contains("test"))
            return "code.generate.tests";
        if (lower.Contains("auth"))
            return "code.generate.auth";
        if (lower.Contains("login"))
            return "code.generate.auth";
        if (lower.Contains("read") && (lower.Contains("file") || lower.Contains("document")))
            return "file.read.text";
        if (lower.Contains("code") && lower.Contains("generate"))
            return "code.generate";
        if (lower.Contains("build") && lower.Contains("app"))
            return "code.generate.app";
        if (lower.Contains("doc") || lower.Contains("readme"))
            return "code.generate.docs";
        return text;
    }

    private static IEnumerable<string> InferSemanticActions(string text)
    {
        var lower = (text ?? "").ToLowerInvariant();
        if (lower.Contains("write tests") || lower.Contains("add tests") || lower.Contains("unit test"))
            yield return "code.generate.tests";
        if (lower.Contains("include auth") || lower.Contains("authentication") || lower.Contains("login"))
            yield return "code.generate.auth";
        if (lower.Contains("rest api") || lower.Contains("api endpoints"))
            yield return "code.generate.api";
        if (lower.Contains("database") || lower.Contains("sqlite") || lower.Contains("db"))
            yield return "code.generate.database";
        if (lower.Contains("docs") || lower.Contains("documentation") || lower.Contains("readme"))
            yield return "code.generate.docs";
        if (lower.Contains("package") || lower.Contains("zip"))
            yield return "code.package";
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
