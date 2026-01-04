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

        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(rolePrompt))
            sb.AppendLine(rolePrompt.Trim());

        foreach (var bp in blueprintPrompts.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            sb.AppendLine();
            sb.AppendLine("BLUEPRINT:");
            sb.AppendLine(bp.Trim());
        }

        if (!string.IsNullOrWhiteSpace(basePrompt))
        {
            sb.AppendLine();
            sb.AppendLine(basePrompt.Trim());
        }

        return sb.ToString().Trim();
    }
}
