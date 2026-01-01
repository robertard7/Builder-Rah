#nullable enable
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace RahBuilder.Workflow;

/// <summary>
/// Graph-driven routing.
/// Phase 1: validate Mermaid and deterministically select a route node.
/// No legacy fallback. No heuristics. No per-app behavior.
/// </summary>
public sealed class WorkflowRouter
{
    // Match route tags like:
    //   X[route: PLAN]
    //   X["route: PLAN"]
    //   X[route:PLAN]
    private static readonly Regex RouteTagRx =
        new(@"\[[^\]]*route\s*:\s*(?<name>[A-Za-z0-9_.\-\/]+)[^\]]*\]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public RouterValidation ValidateGraph(string? mermaid)
    {
        var g = mermaid ?? "";

        // Accept [router], ["router"], [entry], ["entry"] anywhere in node brackets.
        var hasEntry = Regex.IsMatch(
            g,
            @"\[[^\]]*(router|entry)[^\]]*\]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        var hasWaitGate = g.IndexOf("wait_user", StringComparison.OrdinalIgnoreCase) >= 0;

        // IMPORTANT FIX:
        // Don't require literal "[route:" because graphs often use ["route: PLAN"].
        var hasRoute = RouteTagRx.Matches(g).Count > 0;

        if (!hasEntry)
            return RouterValidation.Invalid("Router graph missing entry node tag [router] or [entry].");
        if (!hasRoute)
            return RouterValidation.Invalid("Router graph missing at least one route node tag [route: Name].");
        if (!hasWaitGate)
            return RouterValidation.Invalid("Router graph missing wait_user gate.");

        return RouterValidation.Valid();
    }

    /// <summary>
    /// Phase 1 routing: deterministic selection.
    /// Returns the first route node in the graph (stable ordering by appearance).
    /// </summary>
    public RouteDecision Decide(string? mermaid, string? userText)
    {
        var g = mermaid ?? "";
        var v = ValidateGraph(g);
        if (!v.Ok)
            return RouteDecision.Error(v.Message);

        var routes = ExtractRouteNames(g);
        if (routes.Count == 0)
            return RouteDecision.Error("No route nodes found after validation.");

        var chosen = routes[0];

        return new RouteDecision(
            Ok: true,
            SelectedRoute: chosen,
            RouteCount: routes.Count,
            Why: $"Selected first route in graph (phase1). userTextLen={(userText ?? "").Length}"
        );
    }

    private static List<string> ExtractRouteNames(string g)
    {
        var results = new List<string>();
        foreach (Match m in RouteTagRx.Matches(g))
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

public sealed record RouterValidation(bool Ok, string Message)
{
    public static RouterValidation Valid() => new(true, "ok");
    public static RouterValidation Invalid(string msg) => new(false, msg);
}

public sealed record RouteDecision(bool Ok, string SelectedRoute, int RouteCount, string Why)
{
    public static RouteDecision Error(string msg) => new(false, "", 0, msg);
}
