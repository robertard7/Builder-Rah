#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace RahBuilder.Workflow;

public sealed class PlanStep
{
    public int Order { get; init; }
    public string ToolId { get; init; } = "";
    public string Description { get; init; } = "";
    public string Why { get; init; } = "";
    public Dictionary<string, string> Inputs { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class PlanDefinition
{
    public string Session { get; init; } = "";
    public List<PlanStep> Steps { get; init; } = new();

    public string ToJson()
    {
        return JsonSerializer.Serialize(new
        {
            session = Session,
            ready = Steps.Count > 0,
            steps = Steps.Select(s => new
            {
                id = s.Order,
                toolId = s.ToolId,
                why = s.Why,
                description = s.Description,
                inputs = s.Inputs
            })
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    public static PlanDefinition FromToolPlan(string session, ToolPlan plan)
    {
        var steps = new List<PlanStep>();
        for (int i = 0; i < plan.Steps.Count; i++)
        {
            var s = plan.Steps[i];
            steps.Add(new PlanStep
            {
                Order = i + 1,
                ToolId = s.ToolId,
                Description = s.Why,
                Why = s.Why,
                Inputs = s.Inputs ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            });
        }

        return new PlanDefinition
        {
            Session = session,
            Steps = steps
        };
    }

    public ToolPlan ToToolPlan()
    {
        var steps = new List<ToolPlanStep>();
        foreach (var s in Steps)
        {
            steps.Add(new ToolPlanStep(
                id: "step" + s.Order,
                toolId: s.ToolId,
                inputs: s.Inputs ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                why: string.IsNullOrWhiteSpace(s.Why) ? s.Description : s.Why));
        }

        return new ToolPlan("tool_plan.v1", true, steps);
    }
}
