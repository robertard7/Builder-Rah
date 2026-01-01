#nullable enable
using System.Collections.Generic;
using System.Text.Json;

namespace RahBuilder.Workflow;

public sealed class WorkflowState
{
    public string? PendingQuestion { get; set; }
    public string? PendingToolPlanJson { get; set; }
    public ToolPlan? PendingToolPlan { get; set; }
    public int PendingStepIndex { get; set; }
    public List<JsonElement> ToolOutputs { get; set; } = new();
    public string? LastJobSpecJson { get; set; }
    public string? OriginalUserText { get; set; }

    public void ClearPlan()
    {
        PendingToolPlanJson = null;
        PendingToolPlan = null;
        PendingStepIndex = 0;
        ToolOutputs = new List<JsonElement>();
        LastJobSpecJson = null;
        OriginalUserText = null;
    }
}
