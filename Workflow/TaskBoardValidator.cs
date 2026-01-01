#nullable enable
using System;
using System.Collections.Generic;
using RahOllamaOnly.Tools;
using RahOllamaOnly.Tools.Prompt;

namespace RahBuilder.Workflow;

public static class TaskBoardValidator
{
    public static (bool Ok, string Message) Validate(TaskBoard board, ToolManifest manifest, ToolPromptRegistry prompts)
    {
        if (board == null) return (false, "TaskBoard is null.");
        if (board.Tasks == null || board.Tasks.Count == 0) return (false, "TaskBoard has 0 tasks.");

        for (int i = 0; i < board.Tasks.Count; i++)
        {
            var t = board.Tasks[i];
            if (t == null) return (false, $"Task[{i}] is null.");

            if (string.IsNullOrWhiteSpace(t.ToolId))
                return (false, $"Task[{i}] missing toolId.");

            if (!manifest.Has(t.ToolId))
                return (false, $"Task[{i}] tool not in manifest: {t.ToolId}");

            if (!prompts.Has(t.ToolId))
                return (false, $"Task[{i}] tool missing prompt (gated): {t.ToolId}");
        }

        return (true, "ok");
    }
}
