#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using RahOllamaOnly.Tools.Prompt;

namespace RahOllamaOnly.Tools.Diagnostics;

public static class ToolingValidator
{
    public static ToolingDiagnostics Validate(
        ToolManifest? manifest,
        ToolPromptRegistry? prompts,
        IReadOnlyList<string>? validationErrors = null)
    {
        manifest ??= new ToolManifest(new Dictionary<string, ToolDefinition>(StringComparer.OrdinalIgnoreCase));
        prompts ??= new ToolPromptRegistry();

        var toolIds = manifest.ToolsById.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        var missing = new List<string>();
        var errors = validationErrors?.Where(e => !string.IsNullOrWhiteSpace(e)).ToList() ?? new List<string>();

        foreach (var id in toolIds)
        {
            if (!prompts.Has(id))
                missing.Add(id);
        }

        var active = toolIds.Count - missing.Count;
        var state = (toolIds.Count > 0)
            ? (missing.Count == 0 ? "tools active" : "tools gated (missing prompts)")
            : "tools inactive";

        if (errors.Count > 0)
        {
            active = 0;
            state = $"tools invalid ({errors.Count} manifest errors)";
        }

        return new ToolingDiagnostics(
            ToolCount: toolIds.Count,
            PromptCount: prompts.AllToolIds.Count,
            ActiveToolCount: active < 0 ? 0 : active,
            MissingPrompts: missing,
            ValidationErrors: errors,
            State: state,
            BlueprintTotal: 0,
            BlueprintSelectable: 0,
            SelectedBlueprints: Array.Empty<string>(),
            BlueprintTagBreakdown: new Dictionary<string, int>()
        );
    }
}
