#nullable enable
using System.Collections.Generic;

namespace RahBuilder.Workflow;

public sealed class TaskBoard
{
    public List<TaskItem> Tasks { get; set; } = new();
}

public sealed class TaskItem
{
    public string Title { get; set; } = "";
    public string ToolId { get; set; } = "";
    public string Args { get; set; } = "";
}
