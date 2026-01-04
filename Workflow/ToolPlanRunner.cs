#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RahBuilder.Workflow;

public static class ToolPlanRunner
{
    public static string AssemblePrompt(string rolePrompt, IReadOnlyList<string> blueprintPrompts, string basePrompt)
    {
        if (blueprintPrompts == null || blueprintPrompts.Count == 0)
            throw new InvalidOperationException("No blueprints selected for prompt assembly.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(rolePrompt))
        {
            var rp = rolePrompt.Trim();
            if (seen.Add(rp))
                sb.AppendLine(rp);
        }

        foreach (var bp in blueprintPrompts.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            var trimmed = bp.Trim();
            if (!seen.Add(trimmed))
                continue;
            sb.AppendLine();
            sb.AppendLine("BLUEPRINT:");
            sb.AppendLine(trimmed);
        }

        if (!string.IsNullOrWhiteSpace(basePrompt))
        {
            var bp = basePrompt.Trim();
            if (!seen.Add(bp))
                return sb.ToString().Trim();
            sb.AppendLine();
            sb.AppendLine(bp);
        }

        return sb.ToString().Trim();
    }
}
