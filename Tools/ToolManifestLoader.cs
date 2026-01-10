#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace RahOllamaOnly.Tools;

public static class ToolManifestLoader
{
    public static ToolManifest LoadFromFile(string toolsJsonPath)
    {
        if (string.IsNullOrWhiteSpace(toolsJsonPath) || !File.Exists(toolsJsonPath))
            return new ToolManifest();

        var json = File.ReadAllText(toolsJsonPath);
        using var doc = JsonDocument.Parse(json);

        var version = 0;
        JsonElement arr;
        if (doc.RootElement.ValueKind == JsonValueKind.Object &&
            doc.RootElement.TryGetProperty("tools", out var toolsEl) &&
            toolsEl.ValueKind == JsonValueKind.Array)
        {
            version = GetInt(doc.RootElement, "version");
            arr = toolsEl;
        }
        else if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            arr = doc.RootElement;
        }
        else
        {
            return new ToolManifest();
        }

        var list = new List<ToolDefinition>();
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;

            var id = GetString(item, "id") ?? GetString(item, "toolId") ?? "";
            if (string.IsNullOrWhiteSpace(id)) continue;

            var desc = GetString(item, "description") ?? GetString(item, "desc") ?? "";
            var cmd = GetString(item, "cmd") ?? GetString(item, "command") ?? "";
            var targets = GetStringArray(item, "targets");
            if (targets.Count == 0)
            {
                var target = GetString(item, "target");
                if (!string.IsNullOrWhiteSpace(target))
                    targets.Add(target);
            }

            list.Add(new ToolDefinition
            {
                Id = id,
                Description = desc,
                Command = cmd,
                Targets = targets
            });
        }

        var manifest = new ToolManifest(list) { Version = version };
        return manifest;
    }

    private static string? GetString(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var p)) return null;
        return p.ValueKind switch
        {
            JsonValueKind.String => p.GetString(),
            JsonValueKind.Number => p.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => p.GetRawText()
        };
    }

    private static int GetInt(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var p)) return 0;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var number))
            return number;
        if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var parsed))
            return parsed;
        return 0;
    }

    private static List<string> GetStringArray(JsonElement obj, string name)
    {
        var list = new List<string>();
        if (!obj.TryGetProperty(name, out var p)) return list;
        if (p.ValueKind != JsonValueKind.Array) return list;
        foreach (var item in p.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) continue;
            var value = item.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                list.Add(value.Trim());
        }
        return list;
    }
}
