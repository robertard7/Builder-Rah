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

    private static readonly string[] RequiredRoutes = new[]
    {
        "route:chat_intake",
        "route:digest_intent",
        "route:select_blueprints",
        "route:assemble_prompt",
        "route:plan_tools",
        "route:execute",
        "route:wait_user",
        "route:loop_detect"
    };

    public static RouterValidation Validate(string? mermaid)
    {
        var g = mermaid ?? "";

        var hasEntry = Regex.IsMatch(
            g,
            @"\[[^\]]*(router|entry)[^\]]*\]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        var hasWaitGate = g.IndexOf("wait_user", StringComparison.OrdinalIgnoreCase) >= 0;
        var routes = ExtractRouteNames(g);

        if (!hasEntry)
            return RouterValidation.Invalid("Router graph missing entry node tag [router] or [entry].");
        if (routes.Count == 0)
            return RouterValidation.Invalid("Router graph missing at least one route node tag [route: Name].");
        if (!hasWaitGate)
            return RouterValidation.Invalid("Router graph missing wait_user gate.");

        foreach (var required in RequiredRoutes)
        {
            var plain = required.Replace("route:", "", StringComparison.OrdinalIgnoreCase);
            var match = routes.Any(r =>
                string.Equals(r, required, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r, plain, StringComparison.OrdinalIgnoreCase));
            if (!match)
                return RouterValidation.Invalid($"Workflow graph missing required route node: {required}");
        }

        return RouterValidation.Valid();
    }

    public static List<string> ExtractRouteNames(string g)
    {
        var results = new List<string>();
        foreach (Match m in RouteTagRx.Matches(g ?? ""))
        {
            var name = (m.Groups["name"].Value ?? "").Trim();
            if (name.Length == 0) continue;

            var exists = false;
            foreach (var r in results)
            {
                if (string.Equals(r, name, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }

            if (!exists) results.Add(name);
        }
        return results;
    }
}
