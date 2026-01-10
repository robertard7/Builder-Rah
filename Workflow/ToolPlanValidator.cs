#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using RahOllamaOnly.Tools;

namespace RahBuilder.Workflow;

internal static class ToolPlanValidator
{
    public static IReadOnlyList<string> GetUnknownToolIds(ToolPlan? plan, ToolManifest manifest)
    {
        if (plan?.Steps == null || plan.Steps.Count == 0)
            return Array.Empty<string>();

        manifest ??= new ToolManifest();
        var unknown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var step in plan.Steps)
        {
            if (step == null)
            {
                unknown.Add("<missing>");
                continue;
            }

            if (string.IsNullOrWhiteSpace(step.ToolId))
            {
                unknown.Add("<missing>");
                continue;
            }

            if (!manifest.Has(step.ToolId))
                unknown.Add(step.ToolId);
        }

        return unknown.ToList();
    }
}
