#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace RahBuilder.Tools.BlueprintTemplates;

public sealed record BlueprintTemplateEntry(
    string Id,
    string Kind,
    string File,
    string Sha256,
    IReadOnlyList<string> Tags
);

public sealed class BlueprintTemplateManifest
{
    public IReadOnlyList<BlueprintTemplateEntry> Entries { get; }

    private BlueprintTemplateManifest(IReadOnlyList<BlueprintTemplateEntry> entries)
    {
        Entries = entries;
    }

    public static BlueprintTemplateManifest Load(string blueprintTemplatesFolder)
    {
        if (string.IsNullOrWhiteSpace(blueprintTemplatesFolder))
            throw new ArgumentException("BlueprintTemplates folder path is empty.");

        var manifestPath = Path.Combine(blueprintTemplatesFolder, "manifest.json");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("BlueprintTemplates manifest.json not found.", manifestPath);

        // manifest.json may contain a UTF-8 BOM; handle it explicitly
        var json = ReadAllTextBomSafe(manifestPath);

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("entries", out var entriesEl) || entriesEl.ValueKind != JsonValueKind.Array)
            return new BlueprintTemplateManifest(Array.Empty<BlueprintTemplateEntry>());

        var list = new List<BlueprintTemplateEntry>();

        foreach (var e in entriesEl.EnumerateArray())
        {
            var id = e.TryGetProperty("id", out var idEl) ? (idEl.GetString() ?? "") : "";
            var kind = e.TryGetProperty("kind", out var kindEl) ? (kindEl.GetString() ?? "") : "";
            var file = e.TryGetProperty("file", out var fileEl) ? (fileEl.GetString() ?? "") : "";
            var sha = e.TryGetProperty("sha256", out var shaEl) ? (shaEl.GetString() ?? "") : "";

            var tags = new List<string>();
            if (e.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in tagsEl.EnumerateArray())
                {
                    var s = t.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        tags.Add(s);
                }
            }

            if (!string.IsNullOrWhiteSpace(id))
                list.Add(new BlueprintTemplateEntry(id.Trim(), kind.Trim(), file.Trim(), sha.Trim(), tags));
        }

        // Stable ordering for UI (searchable list still better than a 670-item combo)
        var ordered = list
            .OrderBy(x => x.Kind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new BlueprintTemplateManifest(ordered);
    }

    public IReadOnlyList<BlueprintTemplateEntry> FilterForRole(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName)) return Entries;

        var r = roleName.Trim().ToLowerInvariant();

        // Practical default: show graph + presets for orchestration roles first
        // but do NOT hide anything permanently: user can switch filter to "All".
        if (r.Contains("orchestrator"))
            return Entries.Where(e => e.Id.Contains("orchestrator", StringComparison.OrdinalIgnoreCase)).ToList();

        if (r.Contains("planner"))
            return Entries.Where(e => e.Id.Contains("planner", StringComparison.OrdinalIgnoreCase)).ToList();

        if (r.Contains("executor"))
            return Entries.Where(e => e.Id.Contains("executor", StringComparison.OrdinalIgnoreCase)).ToList();

        if (r.Contains("repair"))
            return Entries.Where(e => e.Id.Contains("repair", StringComparison.OrdinalIgnoreCase)
                                   || e.Id.Contains("diagnose", StringComparison.OrdinalIgnoreCase)
                                   || e.Id.Contains("minimal_fix", StringComparison.OrdinalIgnoreCase)).ToList();

        if (r.Contains("embed") || r.Contains("embedding"))
            return Entries.Where(e => e.Id.Contains("embed", StringComparison.OrdinalIgnoreCase)
                                   || e.Id.Contains("embedding", StringComparison.OrdinalIgnoreCase)).ToList();

        return Entries;
    }

    private static string ReadAllTextBomSafe(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        return Encoding.UTF8.GetString(bytes);
    }
}
