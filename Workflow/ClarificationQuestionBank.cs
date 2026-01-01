#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace RahBuilder.Workflow;

public static class ClarificationQuestionBank
{
    private static readonly Dictionary<string, string> Map = new()
    {
        { "targets", "What project or folder should this apply to?" },
        { "acceptance", "What counts as done? (e.g., build succeeds, tests pass)" },
        { "constraints", "Any constraints? (no rewrites, no new deps, etc.)" },
        { "scope", "Can you clarify the scope or components involved?" },
        { "details", "Any extra details I should know?" }
    };

    public static List<string> PickQuestions(IEnumerable<string> missing, int max = 2)
    {
        var list = new List<string>();
        foreach (var m in missing ?? Enumerable.Empty<string>())
        {
            if (Map.TryGetValue(m.ToLowerInvariant(), out var q) && !list.Contains(q))
                list.Add(q);
            if (list.Count >= max) break;
        }

        if (list.Count == 0)
            list.Add("Could you share a bit more detail so I can proceed?");

        return list;
    }
}

