#nullable enable

namespace RahBuilder.Workflow;

public enum SessionStatus
{
    Idle,
    Planning,
    Running,
    WaitingUser,
    Completed,
    Failed,
    Canceled
}
