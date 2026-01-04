#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RahBuilder.Settings;
using RahBuilder.Tools.BlueprintTemplates;

namespace RahBuilder.Workflow;

public sealed record BlueprintSelectionItem(string Id, string Why);

public sealed record BlueprintSelectionResult(
    string Mode,
    int AvailableCount,
    int SelectableCount,
    IReadOnlyList<BlueprintSelectionItem> Selected,
    IReadOnlyList<BlueprintSelectionItem> Rejected)
{
    public static BlueprintSelectionResult Empty(string why) =>
        new("blueprint.select.v1", 0, 0, Array.Empty<BlueprintSelectionItem>(), new[] { new BlueprintSelectionItem("", why) });
}

public static class BlueprintSelector
{
    private static readonly Regex WordRx = new(@"[A-Za-z0-9_\-\.]+", RegexOptions.Compiled);

    public static BlueprintSelectionResult Select(
        AppConfig cfg,
        string transcript,
        ConversationMode mode,
        IReadOnlyList<AttachmentInbox.AttachmentEntry> attachments,
        string executionTarget)
    {
        var folder = cfg?.General?.BlueprintTemplatesPath ?? "";
        var catalog = BlueprintCatalog.Load(folder);
        if (catalog.Count == 0)
            return BlueprintSelectionResult.Empty("blueprint_catalog_empty");

        var selected = new List<BlueprintSelectionItem>();
        var rejected = new List<BlueprintSelectionItem>();

        var tokens = BuildTokens(transcript, attachments, mode, executionTarget);
        var windowsHost = string.Equals(executionTarget, "WindowsHost", StringComparison.OrdinalIgnoreCase);

        var scored = new List<(BlueprintCatalogEntry Entry, int Score, List<string> Hits)>();
        foreach (var entry in catalog)
        {
            if (!windowsHost && entry.Tags.Any(t => t.Contains("winforms", StringComparison.OrdinalIgnoreCase)))
            {
                rejected.Add(new BlueprintSelectionItem(entry.Id, "winforms_requires_windows_host"));
                continue;
            }

            var hits = entry.Tags.Where(t => tokens.Contains(t, StringComparer.OrdinalIgnoreCase)).ToList();
            var score = hits.Count + entry.Priority;

            if (mode == ConversationMode.Strict && entry.Tags.Any(t => t.Contains("strict", StringComparison.OrdinalIgnoreCase)))
                score += 1;

            scored.Add((entry, score, hits));
        }

        var ordered = scored
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Entry.Priority)
            .ThenBy(x => x.Entry.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var topScore = ordered.FirstOrDefault().Score;
        foreach (var item in ordered)
        {
            if (item.Score <= 0 && item.Score < topScore)
            {
                rejected.Add(new BlueprintSelectionItem(item.Entry.Id, "no_relevant_tags"));
                continue;
            }

            var reason = item.Hits.Count == 0
                ? $"priority={item.Entry.Priority}"
                : $"tags={string.Join(",", item.Hits)} priority={item.Entry.Priority}";
            selected.Add(new BlueprintSelectionItem(item.Entry.Id, reason));
        }

        var selectableCount = selected.Count + rejected.Count;
        return new BlueprintSelectionResult("blueprint.select.v1", catalog.Count, selectableCount, selected, rejected);
    }

    private static HashSet<string> BuildTokens(string transcript, IReadOnlyList<AttachmentInbox.AttachmentEntry> attachments, ConversationMode mode, string executionTarget)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match m in WordRx.Matches(transcript ?? ""))
        {
            var t = m.Value.Trim();
            if (t.Length > 0) tokens.Add(t);
        }

        foreach (var att in attachments ?? Array.Empty<AttachmentInbox.AttachmentEntry>())
        {
            if (!string.IsNullOrWhiteSpace(att.Kind))
                tokens.Add(att.Kind);
            if (!string.IsNullOrWhiteSpace(att.OriginalName))
                tokens.Add(att.OriginalName);
            if (!string.IsNullOrWhiteSpace(att.StoredName))
                tokens.Add(att.StoredName);
        }

        tokens.Add(mode.ToString());
        if (!string.IsNullOrWhiteSpace(executionTarget))
            tokens.Add(executionTarget);

        return tokens;
    }
}
