#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace RahBuilder.Workflow;

public static class ToolPlanParser
{
    public static ToolPlan? TryParse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            if (!doc.RootElement.TryGetProperty("mode", out var modeEl) || modeEl.ValueKind != JsonValueKind.String)
                return null;
            var mode = modeEl.GetString() ?? "";
            if (!string.Equals(mode, "tool_plan.v1", StringComparison.OrdinalIgnoreCase))
                return null;

            if (!doc.RootElement.TryGetProperty("tweakFirst", out var tweakEl) || tweakEl.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                return null;
            var tweakFirst = tweakEl.GetBoolean();

            if (!doc.RootElement.TryGetProperty("steps", out var stepsEl) || stepsEl.ValueKind != JsonValueKind.Array)
                return null;

            var steps = new List<ToolPlanStep>();
            foreach (var stepEl in stepsEl.EnumerateArray())
            {
                if (stepEl.ValueKind != JsonValueKind.Object)
                    return null;

                if (!stepEl.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.String)
                    return null;
                var id = (idEl.GetString() ?? "").Trim();
                if (id.Length == 0)
                    return null;

                if (!stepEl.TryGetProperty("toolId", out var toolEl) || toolEl.ValueKind != JsonValueKind.String)
                    return null;
                var toolId = (toolEl.GetString() ?? "").Trim();
                if (toolId.Length == 0)
                    return null;

                if (!stepEl.TryGetProperty("inputs", out var inputsEl) || inputsEl.ValueKind != JsonValueKind.Object)
                    return null;
                var inputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in inputsEl.EnumerateObject())
                {
                    if (prop.Value.ValueKind != JsonValueKind.String)
                        return null;
                    inputs[prop.Name] = prop.Value.GetString() ?? "";
                }

                if (!stepEl.TryGetProperty("why", out var whyEl) || whyEl.ValueKind != JsonValueKind.String)
                    return null;
                var why = (whyEl.GetString() ?? "").Trim();
                if (why.Length == 0)
                    return null;

                steps.Add(new ToolPlanStep(id, toolId, inputs, why));
            }

            if (steps.Count == 0)
                return null;

            return new ToolPlan(mode, tweakFirst, steps);
        }
        catch
        {
            return null;
        }
    }
}

