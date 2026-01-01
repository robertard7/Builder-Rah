#nullable enable
using System;
using System.Collections.Generic;

namespace RahOllamaOnly.Tools;

// Minimal tool row used across validation/diagnostics.
public sealed class ToolDefinition
{
    public string Id { get; init; } = "";
    public string Description { get; init; } = "";
}

public sealed class ToolManifest
{
    public Dictionary<string, ToolDefinition> ToolsById { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public ToolManifest() { }

    // Existing code expects to be able to new ToolManifest(IEnumerable<ToolDefinition>)
    public ToolManifest(IEnumerable<ToolDefinition> tools)
    {
        if (tools == null) return;
        foreach (var t in tools)
        {
            if (t == null) continue;
            if (string.IsNullOrWhiteSpace(t.Id)) continue;
            ToolsById[t.Id] = t;
        }
    }

    // Your ToolingValidator is passing a Dictionary into a ctor. Support it.
    public ToolManifest(Dictionary<string, ToolDefinition> toolsById)
    {
        if (toolsById == null) return;
        foreach (var kv in toolsById)
        {
            if (kv.Value == null) continue;
            if (string.IsNullOrWhiteSpace(kv.Value.Id))
            {
                // tolerate cases where dictionary key is the id
                ToolsById[kv.Key] = new ToolDefinition { Id = kv.Key, Description = kv.Value.Description ?? "" };
            }
            else
            {
                ToolsById[kv.Value.Id] = kv.Value;
            }
        }
    }

    public bool Has(string toolId)
        => !string.IsNullOrWhiteSpace(toolId) && ToolsById.ContainsKey(toolId);
}
