#nullable enable
using System;
using System.Collections.Generic;
using RahBuilder.Settings;

namespace RahBuilder.Workflow;

/// <summary>
/// Graph-driven routing.
/// Phase 1: validate Mermaid and deterministically select a route node.
/// No legacy fallback. No heuristics. No per-app behavior.
/// </summary>
public sealed class WorkflowRouter
{
    private string _lastRoute = "";
    private string _lastStateHash = "";
    private int _repeatCount;

    public RouterValidation ValidateGraph(string? mermaid)
    {
        return WorkflowGraph.Validate(mermaid);
    }

    /// <summary>
    /// Phase 1 routing: deterministic selection.
    /// Returns the first route node in the graph (stable ordering by appearance).
    /// </summary>
    public RouteDecision Decide(
        string? mermaid,
        string? userText,
        ConversationMode mode,
        bool chatIntakeCompleted,
        string stateHash)
    {
        var g = mermaid ?? "";
        var v = ValidateGraph(g);
        if (!v.Ok)
            return RouteDecision.Error(v.Message);

        var routes = WorkflowGraph.ExtractRouteNames(g);
        if (routes.Count == 0)
            return RouteDecision.Error("No route nodes found after validation.");

        var chosen = routes[0];
        if (mode == ConversationMode.Conversational)
        {
            if (!IsChatIntake(chosen))
                return RouteDecision.Error("Conversational mode requires route:chat_intake as the first node.");
            if (IsJobSpec(chosen))
                return RouteDecision.Error("JobSpecDigest cannot be the first hop in conversational mode.");
        }

        if (IsJobSpec(chosen) && !chatIntakeCompleted)
            return RouteDecision.Error("jobspec.v2 cannot be entered before chat intake.");

        if (IsJobSpec(chosen) && !IsChatIntake(routes[0]))
            return RouteDecision.Error("JobSpecDigest must follow chat_intake.");

        var loopKey = string.IsNullOrWhiteSpace(stateHash) ? $"{g}|{userText}|{chosen}" : stateHash;
        if (string.Equals(_lastRoute, chosen, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(_lastStateHash, loopKey, StringComparison.Ordinal))
        {
            _repeatCount++;
            if (_repeatCount > 2)
                return RouteDecision.Error($"Route '{chosen}' repeated {_repeatCount} times with unchanged state.");
        }
        else
        {
            _repeatCount = 1;
            _lastRoute = chosen;
            _lastStateHash = loopKey;
        }

        return new RouteDecision(
            Ok: true,
            SelectedRoute: chosen,
            RouteCount: routes.Count,
            Why: $"Selected first route in graph (phase1). userTextLen={(userText ?? "").Length}"
        );
    }

    private static bool IsChatIntake(string route) =>
        route.Equals("route:chat_intake", StringComparison.OrdinalIgnoreCase) ||
        route.Equals("chat_intake", StringComparison.OrdinalIgnoreCase);

    private static bool IsJobSpec(string route) =>
        route.Equals("route:jobspec.v2", StringComparison.OrdinalIgnoreCase) ||
        route.Equals("jobspec.v2", StringComparison.OrdinalIgnoreCase);
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
