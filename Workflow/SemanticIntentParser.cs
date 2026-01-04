#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RahBuilder.Settings;
using RahOllamaOnly.Tools;

namespace RahBuilder.Workflow;

public sealed record SemanticIntent(
    string Goal,
    IReadOnlyList<string> OrderedActions,
    IReadOnlyList<string> Constraints,
    string Context,
    string Clarification,
    bool Ready);

public static class SemanticIntentParser
{
    public static async Task<SemanticIntent> ParseAsync(
        AppConfig cfg,
        string userText,
        IReadOnlyList<AttachmentInbox.AttachmentEntry> attachments,
        ChatMemory memory,
        CancellationToken ct)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        memory ??= new ChatMemory();
        var prompt = (cfg.General.IntentExtractorPrompt ?? "").Trim();
        if (prompt.Length == 0)
            return new SemanticIntent("", Array.Empty<string>(), Array.Empty<string>(), "", "intent_prompt_missing", false);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("ATTACHMENTS:");
        if (attachments == null || attachments.Count == 0)
            sb.AppendLine("- none");
        else
            foreach (var a in attachments.Where(a => a != null))
                sb.AppendLine($"- storedName: {a.StoredName}, kind: {a.Kind}, original: {a.OriginalName}");

        var history = memory.Summarize(6);
        if (!string.IsNullOrWhiteSpace(history))
        {
            sb.AppendLine();
            sb.AppendLine("RECENT_MEMORY:");
            sb.AppendLine(history);
        }

        sb.AppendLine();
        sb.AppendLine("USER_TEXT:");
        sb.AppendLine(userText ?? "");

        var raw = await LlmInvoker.InvokeChatAsync(cfg, "Orchestrator", prompt, sb.ToString(), ct, Array.Empty<string>(), "semantic_intent").ConfigureAwait(false);
        var parsed = TryParse(raw);
        if (parsed == null)
        {
            var retryPrompt = prompt + "\nReturn ONLY JSON. Fix invalid formatting.";
            raw = await LlmInvoker.InvokeChatAsync(cfg, "Orchestrator", retryPrompt, sb.ToString(), ct, Array.Empty<string>(), "semantic_intent_retry").ConfigureAwait(false);
            parsed = TryParse(raw) ?? new IntentExtraction("intent.v1", userText ?? "", "", "", new List<string>(), new List<string>(), "invalid_json", new List<string> { "invalid_json" }, false);
        }

        var ordered = OrderActions(parsed?.Actions ?? Array.Empty<string>(), userText);
        return new SemanticIntent(
            parsed?.Goal ?? "",
            ordered,
            parsed?.Constraints ?? Array.Empty<string>(),
            parsed?.Context ?? "",
            parsed?.Clarification ?? "",
            parsed?.Ready == true && ordered.Count > 0);
    }

    private static IntentExtraction? TryParse(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            var goal = Read(root, "goal");
            var context = Read(root, "context");
            var clarification = Read(root, "clarification");
            var constraints = ReadArray(root, "constraints");
            var actions = ReadArray(root, "actions");
            var ready = root.TryGetProperty("ready", out var r) && r.ValueKind == JsonValueKind.True;
            var missing = ReadArray(root, "missing");
            return new IntentExtraction("intent.v1", goal, goal, context, actions, constraints, clarification, missing, ready);
        }
        catch
        {
            return null;
        }
    }

    private static List<string> ReadArray(JsonElement obj, string name)
    {
        var list = new List<string>();
        if (!obj.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Array)
            return list;
        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var s = (item.GetString() ?? "").Trim();
                if (s.Length > 0) list.Add(s);
            }
        }
        return list;
    }

    private static string Read(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
            return "";
        return el.GetString() ?? "";
    }

    private static List<string> OrderActions(IReadOnlyList<string> actions, string userText)
    {
        var ordered = new List<string>();
        foreach (var a in actions ?? Array.Empty<string>())
        {
            var mapped = IntentExtractionParser.MapAction(a);
            if (!ordered.Contains(mapped, StringComparer.OrdinalIgnoreCase))
                ordered.Add(mapped);
        }

        foreach (var token in IntentExtractionParser.SplitByConnectors(userText))
        {
            var mapped = IntentExtractionParser.MapAction(token);
            if (!ordered.Contains(mapped, StringComparer.OrdinalIgnoreCase))
                ordered.Add(mapped);
        }

        return ordered;
    }
}
