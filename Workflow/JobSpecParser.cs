#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace RahBuilder.Workflow;

public static class JobSpecParser
{
    public static JobSpec? TryParse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var json = text.Trim();
        return TryParseToJobSpec(json, out var spec) ? spec : null;
    }

    private static bool TryParseToJobSpec(string json, out JobSpec? spec)
    {
        spec = null;

        JsonDocument? doc = null;

        try
        {
            doc = JsonDocument.Parse(json);
            var ok = TryBuildJobSpec(doc, out spec);
            if (!ok)
            {
                doc.Dispose();
            }

            return ok;
        }
        catch
        {
            doc?.Dispose();
            return false;
        }
    }

    private static bool TryBuildJobSpec(JsonDocument doc, out JobSpec? spec)
    {
        spec = null;

        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return false;

        if (!doc.RootElement.TryGetProperty("state", out var state) || state.ValueKind != JsonValueKind.Object)
            return false;

        if (!state.TryGetProperty("ready", out var readyEl) || readyEl.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            return false;

        if (!state.TryGetProperty("missing", out var missingEl) || missingEl.ValueKind != JsonValueKind.Array)
            return false;

        var mode = ReadString(doc.RootElement, "mode", "jobspec.v2");
        var request = ReadString(doc.RootElement, "request", "");
        var goal = ReadString(doc.RootElement, "goal", "").Trim();
        var context = ReadString(doc.RootElement, "context", "");
        var actions = ReadStringArray(doc.RootElement, "actions").Select(a => a.Trim()).Where(a => a.Length > 0).ToList();
        var constraints = ReadStringArray(doc.RootElement, "constraints");
        var clarification = ReadString(doc.RootElement, "clarification", "");

        var missing = NormalizeMissing(ReadStringArray(state, "missing"));
        var attachments = ParseAttachments(doc.RootElement, missing);

        if (string.IsNullOrWhiteSpace(clarification))
            clarification = ReadString(state, "clarify", "");

        if (!HasProperty(doc.RootElement, "context"))
            AddMissing("context", missing);
        if (!HasProperty(doc.RootElement, "constraints"))
            AddMissing("constraints", missing);
        if (!HasProperty(doc.RootElement, "clarification"))
            AddMissing("clarification", missing);
        if (!HasProperty(doc.RootElement, "actions"))
            AddMissing("actions", missing);
        if (!HasProperty(doc.RootElement, "attachments"))
            AddMissing("attachments", missing);

        AddMissingIfEmpty(request, "request", missing);
        AddMissingIfEmpty(goal, "goal", missing);
        AddMissingIfEmpty(actions, "actions", missing);
        AddMissingIfEmpty(attachments, "attachments", missing);

        var inferred = InferActions(request, attachments);
        if (actions.Count == 0 && inferred.Count > 0)
            actions = inferred;
        else if (inferred.Count > 0)
            actions = MergeActions(actions, inferred);

        if (actions.Count == 0 && attachments.Count > 0)
        {
            actions = SuggestActionsFromAttachments(attachments);
            if (actions.Count > 0)
                missing.RemoveAll(m => string.Equals(m, "actions", StringComparison.OrdinalIgnoreCase));
        }
        else if (actions.Count > 0)
        {
            missing.RemoveAll(m => string.Equals(m, "actions", StringComparison.OrdinalIgnoreCase));
        }

        var ready = readyEl.GetBoolean() && missing.Count == 0;

        spec = JobSpec.FromJson(
            doc,
            mode,
            request,
            goal,
            context,
            actions,
            constraints,
            attachments,
            string.IsNullOrWhiteSpace(clarification) ? null : clarification,
            ready,
            missing);
        return true;
    }

    private static List<JobSpecAttachment> ParseAttachments(JsonElement root, List<string> missing)
    {
        var list = new List<JobSpecAttachment>();
        if (!root.TryGetProperty("attachments", out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            AddMissing("attachments", missing);
            return list;
        }

        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                AddMissing("attachments", missing);
                continue;
            }

            var stored = ReadString(item, "storedName", "");
            if (string.IsNullOrWhiteSpace(stored))
            {
                AddMissing("attachments", missing);
                continue;
            }

            var kind = NormalizeKind(ReadString(item, "kind", "other"));
            var status = NormalizeStatus(ReadString(item, "status", "present"));
            var summary = ReadString(item, "summary", "").Trim();
            var tags = ReadStringArray(item, "tags");

            var tools = ReadStringArray(item, "tools");
            if (tools.Count == 0)
                tools = ReadStringArray(item, "requiredTools");
            if (tools.Count == 0)
            {
                var suggested = SuggestTools(kind);
                if (suggested.Count > 0)
                    tools = suggested;
            }

            list.Add(new JobSpecAttachment(stored, kind, status, summary, tags, tools));
        }

        return list;
    }

    private static string ReadString(JsonElement obj, string property, string fallback)
    {
        if (obj.ValueKind != JsonValueKind.Object) return fallback;
        if (!obj.TryGetProperty(property, out var el)) return fallback;
        return el.ValueKind == JsonValueKind.String ? el.GetString() ?? fallback : fallback;
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
                var s = (item.GetString() ?? "").Trim();
                if (s.Length > 0)
                    list.Add(s);
            }
        }

        return list;
    }

    private static List<string> NormalizeMissing(IEnumerable<string> source)
    {
        var missing = new List<string>();
        foreach (var m in source ?? Enumerable.Empty<string>())
        {
            var s = (m ?? "").Trim();
            if (s.Length == 0) continue;
            AddMissing(s, missing);
        }
        return missing;
    }

    private static void AddMissing(string field, List<string> missing)
    {
        if (missing == null) return;
        if (missing.Contains(field, StringComparer.OrdinalIgnoreCase)) return;
        missing.Add(field);
    }

    private static void AddMissingIfEmpty(List<JobSpecAttachment> attachments, string field, List<string> missing)
    {
        if (attachments == null || attachments.Count == 0)
            AddMissing(field, missing);
    }

    private static void AddMissingIfEmpty(IEnumerable<string> list, string field, List<string> missing)
    {
        if (list == null || !list.Any())
            AddMissing(field, missing);
    }

    private static void AddMissingIfEmpty(string value, string field, List<string> missing)
    {
        if (string.IsNullOrWhiteSpace(value))
            AddMissing(field, missing);
    }

    private static List<string> SuggestActionsFromAttachments(List<JobSpecAttachment> attachments)
    {
        var actions = new List<string>();
        if (attachments == null || attachments.Count == 0)
            return actions;

        var kinds = attachments.Select(a => NormalizeKind(a.Kind)).ToList();
        if (kinds.Any(k => k == "image"))
            actions.Add("describe image");
        if (kinds.Any(k => k == "document" || k == "code"))
            actions.Add("summarize document");
        if (actions.Count > 1)
            actions.Add("combine findings");

        return actions;
    }

    private static List<string> InferActions(string request, List<JobSpecAttachment> attachments)
    {
        var actions = new List<string>();
        var text = (request ?? "").ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(text))
            return actions;

        bool hasImage = attachments.Any(a => NormalizeKind(a.Kind) == "image");
        bool hasDoc = attachments.Any(a => NormalizeKind(a.Kind) is "document" or "code");

        if (text.Contains("describe") && hasImage)
            actions.Add("describe image");
        if ((text.Contains("summarize") || text.Contains("read") || text.Contains("analyze")) && hasDoc)
            actions.Add("summarize document");
        if (text.Contains("compare"))
            actions.Add("compare attachments");
        if (text.Contains("combine") || text.Contains("then") || text.Contains("after"))
            actions.Add("combine findings");

        return NormalizeActions(actions);
    }

    private static List<string> MergeActions(List<string> existing, List<string> inferred)
    {
        var merged = new List<string>(existing ?? new List<string>());
        foreach (var a in inferred ?? Enumerable.Empty<string>())
        {
            if (!merged.Contains(a, StringComparer.OrdinalIgnoreCase))
                merged.Add(a);
        }
        return NormalizeActions(merged);
    }

    private static List<string> NormalizeActions(IEnumerable<string> actions)
    {
        return actions
            ?.Select(a => (a ?? "").Trim())
            .Where(a => a.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();
    }

    private static string NormalizeKind(string kind)
    {
        var k = (kind ?? "").ToLowerInvariant();
        if (k.Contains("image")) return "image";
        if (k.Contains("doc")) return "document";
        if (k.Contains("code")) return "code";
        return "other";
    }

    private static string NormalizeStatus(string status)
    {
        var s = (status ?? "").ToLowerInvariant();
        return s switch
        {
            "missing" => "missing",
            "pending" => "pending",
            _ => "present"
        };
    }

    private static List<string> SuggestTools(string kind)
    {
        var k = NormalizeKind(kind);
        if (k == "image") return new List<string> { "vision.describe.image" };
        if (k == "document" || k == "code") return new List<string> { "file.read.text" };
        return new List<string>();
    }

    private static bool HasProperty(JsonElement obj, string property)
    {
        return obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(property, out _);
    }
}
