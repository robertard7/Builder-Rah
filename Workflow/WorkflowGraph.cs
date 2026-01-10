// Workflow/WorkflowGraph.cs
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RahBuilder.Workflow;

public static class WorkflowGraph
{
    private static readonly Regex RouteTagRx =
        new(@"\[[^\]]*route\s*:\s*(?<name>[A-Za-z0-9_.\-\/]+)[^\]]*\]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static RouterValidation Validate(string? mermaid)
    {
        var g = mermaid ?? "";
        if (g.Trim().Length == 0)
            return RouterValidation.Invalid("Workflow graph is empty.");

        // Accept either explicit [entry] tag or a route named UserInput as the “entry concept”.
        var hasEntryTag = Regex.IsMatch(g, @"\[[^\]]*(entry|router)[^\]]*\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        var routes = ExtractRouteNames(g);
        if (routes.Count == 0)
            return RouterValidation.Invalid("Router graph missing at least one route node tag [route: Name].");

        var hasUserInput = routes.Any(r => string.Equals(r, "UserInput", StringComparison.OrdinalIgnoreCase));
        if (!hasEntryTag && !hasUserInput)
            return RouterValidation.Invalid("Graph needs either an [entry] node tag or a [route: UserInput] node.");

        // The WAIT_USER gate is core to your UI flow.
        var hasWaitUser = routes.Any(r => string.Equals(r, "WaitUser", StringComparison.OrdinalIgnoreCase));
        if (!hasWaitUser)
            return RouterValidation.Invalid("Graph missing required [route: WaitUser] node (WAIT_USER gate).");

        return RouterValidation.Valid();
    }

    public static List<string> ExtractRouteNames(string g)
    {
        var results = new List<string>();
        foreach (Match m in RouteTagRx.Matches(g ?? ""))
        {
            var name = (m.Groups["name"].Value ?? "").Trim();
            if (name.Length == 0) continue;

            if (!results.Any(r => string.Equals(r, name, StringComparison.OrdinalIgnoreCase)))
                results.Add(name);
        }
        return results;
    }
}
