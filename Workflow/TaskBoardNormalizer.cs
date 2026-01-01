#nullable enable
using System;
using System.Text.Json;

namespace RahBuilder.Workflow;

public static class TaskBoardNormalizer
{
    public static TaskBoard? TryNormalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var t = text.Trim();

        // Only accepts strict JSON object with { tasks: [...] } shape.
        // No fallback guessing.
        try
        {
            var board = JsonSerializer.Deserialize<TaskBoard>(t, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (board == null) return null;
            if (board.Tasks == null) board.Tasks = new();
            return board;
        }
        catch
        {
            return null;
        }
    }
}
