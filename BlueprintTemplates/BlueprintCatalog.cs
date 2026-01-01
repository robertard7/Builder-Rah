// C:\dev\rah\Rah-Builder\Tools\BlueprintTemplates\BlueprintCatalog.cs
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace RahBuilder.Tools.BlueprintTemplates;

public sealed record BlueprintCatalogEntry(
    string Id,
    string Kind,
    string Visibility,
    int Priority,
    string Title,
    string Description,
    IReadOnlyList<string> Tags,
    string File
)
{
    public string Display => string.IsNullOrWhiteSpace(Title) ? Id : $"{Id} | {Title}";
}

public static class BlueprintCatalog
{
    public static IReadOnlyList<BlueprintCatalogEntry> Load(string blueprintTemplatesFolder)
    {
        if (string.IsNullOrWhiteSpace(blueprintTemplatesFolder))
            return Array.Empty<BlueprintCatalogEntry>();

        var manifestPath = Path.Combine(blueprintTemplatesFolder, "manifest.json");
        if (!File.Exists(manifestPath))
            return Array.Empty<BlueprintCatalogEntry>();

        var json = ReadAllTextBomSafe(manifestPath);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("entries", out var entriesEl) || entriesEl.ValueKind != JsonValueKind.Array)
            return Array.Empty<BlueprintCatalogEntry>();

        var list = new List<BlueprintCatalogEntry>();

        foreach (var e in entriesEl.EnumerateArray())
        {
            var id = GetString(e, "id");
            if (string.IsNullOrWhiteSpace(id)) continue;

            var kind = GetString(e, "kind");
            var file = GetString(e, "file");
            var visibility = "internal";
            var priority = 0;

            if (e.TryGetProperty("meta", out var meta) && meta.ValueKind == JsonValueKind.Object)
            {
                visibility = GetString(meta, "visibility");
                if (string.IsNullOrWhiteSpace(visibility)) visibility = "internal";
                priority = GetInt(meta, "priority");
            }

            var tags = GetStringArray(e, "tags");

            // Resolve file path (manifest stores "BlueprintTemplates/..." but folder already points to BlueprintTemplates)
            var resolvedTemplatePath = ResolveTemplatePath(blueprintTemplatesFolder, file);

            // Pull title/description/tags from the template file when present
            var title = "";
            var desc = "";
            var fileTags = Array.Empty<string>();

            if (!string.IsNullOrWhiteSpace(resolvedTemplatePath) && File.Exists(resolvedTemplatePath))
            {
                try
                {
                    var tjson = ReadAllTextBomSafe(resolvedTemplatePath);
                    using var tdoc = JsonDocument.Parse(tjson);
                    var root = tdoc.RootElement;

                    // Support either top-level or meta.*
                    title = GetString(root, "title");
                    desc = GetString(root, "description");
                    fileTags = GetStringArray(root, "tags").ToArray();

                    if (root.TryGetProperty("meta", out var tmeta) && tmeta.ValueKind == JsonValueKind.Object)
                    {
                        if (string.IsNullOrWhiteSpace(title)) title = GetString(tmeta, "title");
                        if (string.IsNullOrWhiteSpace(desc)) desc = GetString(tmeta, "description");
                        var mtags = GetStringArray(tmeta, "tags");
                        if (mtags.Count > 0) fileTags = mtags.ToArray();
                    }
                }
                catch
                {
                    // Don’t crash the UI because one template is malformed.
                }
            }

            var mergedTags = tags
                .Concat(fileTags)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            list.Add(new BlueprintCatalogEntry(
                id.Trim(),
                (kind ?? "").Trim(),
                visibility.Trim(),
                priority,
                (title ?? "").Trim(),
                (desc ?? "").Trim(),
                mergedTags,
                (file ?? "").Trim()
            ));
        }

        // Stable sort for UI
        return list
            .OrderByDescending(x => string.Equals(x.Visibility, "public", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => x.Priority)
            .ThenBy(x => x.Kind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<BlueprintCatalogEntry> PublicOnly(IReadOnlyList<BlueprintCatalogEntry> src)
        => src.Where(e => string.Equals(e.Visibility, "public", StringComparison.OrdinalIgnoreCase)).ToList();

    private static string ResolveTemplatePath(string blueprintTemplatesFolder, string fileFromManifest)
    {
        if (string.IsNullOrWhiteSpace(fileFromManifest)) return "";

        var f = fileFromManifest.Replace('\\', '/').Trim();

        // Common case: "BlueprintTemplates/atoms/....json"
        if (f.StartsWith("BlueprintTemplates/", StringComparison.OrdinalIgnoreCase))
            f = f.Substring("BlueprintTemplates/".Length);

        // If still looks rooted (rare), just return it
        if (Path.IsPathRooted(f))
            return f;

        return Path.Combine(blueprintTemplatesFolder, f.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string GetString(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object) return "";
        if (!obj.TryGetProperty(name, out var el)) return "";
        return el.ValueKind == JsonValueKind.String ? (el.GetString() ?? "") : el.ToString();
    }

    private static int GetInt(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object) return 0;
        if (!obj.TryGetProperty(name, out var el)) return 0;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var i)) return i;
        if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var j)) return j;
        return 0;
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object) return Array.Empty<string>();
        if (!obj.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        var list = new List<string>();
        foreach (var x in el.EnumerateArray())
        {
            if (x.ValueKind == JsonValueKind.String)
            {
                var s = x.GetString();
                if (!string.IsNullOrWhiteSpace(s)) list.Add(s);
            }
            else
            {
                var s = x.ToString();
                if (!string.IsNullOrWhiteSpace(s)) list.Add(s);
            }
        }
        return list;
    }

    private static string ReadAllTextBomSafe(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        return Encoding.UTF8.GetString(bytes);
    }
}
