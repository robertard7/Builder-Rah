#nullable enable

namespace RahBuilder.Workflow;

public static class StatusMapper
{
    public static SessionStatus GetStatus(WorkflowState state, bool recentlyCompleted)
    {
        if (state.Canceled)
            return SessionStatus.Canceled;

        if (recentlyCompleted)
            return SessionStatus.Completed;

        if (state.PendingToolPlan != null)
        {
            var idx = state.PendingStepIndex;
            var count = state.PendingToolPlan.Steps.Count;
            if (idx >= count)
                return SessionStatus.Completed;
            if (state.ToolOutputs.Count > 0 || idx > 0 || state.AutoApproveAll)
                return SessionStatus.Running;
            return SessionStatus.Planning;
        }

        if (!string.IsNullOrWhiteSpace(state.PendingQuestion))
            return SessionStatus.WaitingUser;

        if (!string.IsNullOrWhiteSpace(state.PendingUserRequest))
            return SessionStatus.Planning;

        return SessionStatus.Idle;
    }

    public static string ToDisplay(SessionStatus status)
    {
        return status switch
        {
            SessionStatus.Idle => "Idle",
            SessionStatus.Planning => "Planning",
            SessionStatus.Running => "Running",
            SessionStatus.WaitingUser => "Waiting for input",
            SessionStatus.Completed => "Completed",
            SessionStatus.Failed => "Failed",
            SessionStatus.Canceled => "Canceled",
            _ => "Idle"
        };
    }
}
