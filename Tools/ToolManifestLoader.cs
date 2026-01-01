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

        JsonElement arr;
        if (doc.RootElement.ValueKind == JsonValueKind.Object &&
            doc.RootElement.TryGetProperty("tools", out var toolsEl) &&
            toolsEl.ValueKind == JsonValueKind.Array)
        {
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

            list.Add(new ToolDefinition { Id = id, Description = desc });
        }

        return new ToolManifest(list);
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
}
