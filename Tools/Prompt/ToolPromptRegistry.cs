#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RahOllamaOnly.Tools.Prompt;

public sealed class ToolPromptRegistry
{
    private readonly Dictionary<string, string> _byToolId =
        new(StringComparer.OrdinalIgnoreCase);

    // Keep both names because your older diagnostics expects AllToolIds.
    public IReadOnlyCollection<string> All => _byToolId.Keys.ToArray();
    public IReadOnlyCollection<string> AllToolIds => _byToolId.Keys.ToArray();

    public bool Has(string toolId)
        => !string.IsNullOrWhiteSpace(toolId) && _byToolId.ContainsKey(toolId);

    public string? Get(string toolId)
        => _byToolId.TryGetValue(toolId, out var s) ? s : null;

    public void Clear() => _byToolId.Clear();

    public void LoadFromDirectory(string promptsDir)
    {
        _byToolId.Clear();

        if (string.IsNullOrWhiteSpace(promptsDir)) return;
        if (!Directory.Exists(promptsDir)) return;

        foreach (var path in Directory.GetFiles(promptsDir))
        {
            var toolId = NormalizeToolIdFromPromptFile(path);
            if (string.IsNullOrWhiteSpace(toolId)) continue;

            var txt = "";
            try { txt = File.ReadAllText(path); } catch { /* ignore */ }

            _byToolId[toolId] = txt ?? "";
        }
    }

    public static string NormalizeToolIdFromPromptFile(string filePath)
    {
        var name = Path.GetFileName(filePath) ?? "";
        if (name.Length == 0) return "";

        var id = Path.GetFileNameWithoutExtension(name) ?? "";

        if (id.EndsWith(".prompt", StringComparison.OrdinalIgnoreCase))
            id = id.Substring(0, id.Length - ".prompt".Length);

        return id.Trim();
    }
}
