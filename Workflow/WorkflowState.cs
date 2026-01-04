#nullable enable
using System;
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
    public List<OutputCard> OutputCards { get; set; } = new();
    public string? LastJobSpecJson { get; set; }
    public string? OriginalUserText { get; set; }
    public string PendingUserRequest { get; set; } = "";
    public List<string> ClarificationAnswers { get; set; } = new();
    public List<string> PendingQuestions { get; set; } = new();
    public bool AutoApproveAll { get; set; }
    public bool PlanPaused { get; set; }
    public IntentExtraction? LastIntent { get; set; }
    public ChatMemory Memory { get; } = new();
    public ConversationMemory MemoryStore { get; } = new();
    public HashSet<string> ConfirmedFields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> ClarificationAsked { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> AskedMissingFields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool GenerateArtifacts { get; set; }
    public string SessionToken { get; set; } = Guid.NewGuid().ToString("N");
    public List<string> ArtifactPackages { get; set; } = new();
    public List<ProgramArtifactResult> ProgramArtifacts { get; set; } = new();
    public bool PlanConfirmed { get; set; }
    public string LastRoute { get; set; } = "";
    public string LastRouteBeforeWait { get; set; } = "";
    public string LastFlowSignature { get; set; } = "";
    public bool ChatIntakeCompleted { get; set; }
    public List<string> SelectedBlueprints { get; set; } = new();

    public void ClearPlan()
    {
        PendingToolPlanJson = null;
        PendingToolPlan = null;
        PendingStepIndex = 0;
        ToolOutputs = new List<JsonElement>();
        OutputCards = new List<OutputCard>();
        LastJobSpecJson = null;
        OriginalUserText = null;
        AutoApproveAll = false;
        PlanPaused = false;
        GenerateArtifacts = false;
        ArtifactPackages = new List<string>();
        ProgramArtifacts = new List<ProgramArtifactResult>();
        PlanConfirmed = false;
        SelectedBlueprints = new List<string>();
    }

    public void ClearClarifications()
    {
        ClarificationAnswers = new List<string>();
        PendingQuestions = new List<string>();
        AskedMissingFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ClarificationAsked = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        ConfirmedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        PlanConfirmed = false;
    }
}
