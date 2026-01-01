#nullable enable
using System.Collections.Generic;

namespace RahBuilder.Workflow;

public sealed class ToolPlan
{
    public string Mode { get; }
    public bool TweakFirst { get; }
    public List<ToolPlanStep> Steps { get; }

    public ToolPlan(string mode, bool tweakFirst, List<ToolPlanStep> steps)
    {
        Mode = mode;
        TweakFirst = tweakFirst;
        Steps = steps;
    }
}

public sealed class ToolPlanStep
{
    public string Id { get; }
    public string ToolId { get; }
    public Dictionary<string, string> Inputs { get; }
    public string Why { get; }

    public ToolPlanStep(string id, string toolId, Dictionary<string, string> inputs, string why)
    {
        Id = id;
        ToolId = toolId;
        Inputs = inputs;
        Why = why;
    }
}

