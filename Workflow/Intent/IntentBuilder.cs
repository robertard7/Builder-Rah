#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RahBuilder.Workflow;

public sealed class IntentBuilder
{
    private static readonly Regex QuotedRx = new(@"[\""'\`](?<value>[^\""'\`]+)[\""'\`]", RegexOptions.Compiled);
    private static readonly Regex TargetRx = new(@"(?:folder|project|repo|repository|solution|app|directory)\s+(?<value>[\w\-.\\/]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public IntentUpdate Apply(UserMessage msg, IntentState? priorState)
    {
        var state = CloneOrCreate(priorState);
        var messageText = (msg.Text ?? "").Trim();

        state.ContextWindow.Add(msg);
        TrimContext(state);

        var slots = new Dictionary<string, string>(state.Slots, StringComparer.OrdinalIgnoreCase);
        var goal = DetectGoal(messageText, state.CurrentGoal);
        state.CurrentGoal = goal;

        if (TryExtractTarget(messageText, out var target))
            slots["target"] = target;

        state.Slots = slots;

        if (string.IsNullOrWhiteSpace(messageText))
        {
            state.Status = IntentStatus.Collecting;
            return BuildUpdate(state, true, "What would you like to do?");
        }

        if (string.IsNullOrWhiteSpace(goal))
        {
            state.Status = IntentStatus.Collecting;
            return BuildUpdate(state, true, "What would you like to do?");
        }

        if (IsAttachmentRequired(goal) && msg.Attachments.Count == 0)
        {
            state.Status = IntentStatus.Clarifying;
            return BuildUpdate(state, true, "Please attach a file or photo so I can help.");
        }

        if (NeedsTarget(goal) && !slots.ContainsKey("target"))
        {
            state.Status = IntentStatus.Clarifying;
            return BuildUpdate(state, true, "What should I name the folder or target?");
        }

        state.Status = IntentStatus.Ready;
        return BuildUpdate(state, false, null);
    }

    public IntentDocumentV1 BuildDocument(IntentState state)
    {
        var goal = state.CurrentGoal ?? "";
        var missing = new List<string>();
        var actions = new List<string>();
        var constraints = new List<string>();
        var clarification = "";

        if (string.IsNullOrWhiteSpace(goal))
            missing.Add("goal");

        if (state.Status == IntentStatus.Clarifying)
            clarification = "Clarification required.";

        if (!string.IsNullOrWhiteSpace(goal))
            actions.Add(goal);

        return new IntentDocumentV1(
            "intent.v1",
            goal,
            "",
            actions,
            constraints,
            missing,
            state.Status == IntentStatus.Ready,
            clarification);
    }

    private static IntentState CloneOrCreate(IntentState? priorState)
    {
        if (priorState == null)
        {
            return new IntentState
            {
                SessionId = Guid.NewGuid().ToString("N"),
                Status = IntentStatus.Collecting
            };
        }

        return new IntentState
        {
            SessionId = priorState.SessionId,
            CurrentGoal = priorState.CurrentGoal,
            Slots = new Dictionary<string, string>(priorState.Slots, StringComparer.OrdinalIgnoreCase),
            ContextWindow = new List<UserMessage>(priorState.ContextWindow),
            Status = priorState.Status
        };
    }

    private static IntentUpdate BuildUpdate(IntentState state, bool requiresUserInput, string? nextQuestion)
    {
        return new IntentUpdate(
            state,
            nextQuestion,
            requiresUserInput,
            nextQuestion);
    }

    private static void TrimContext(IntentState state)
    {
        const int MaxMessages = 12;
        if (state.ContextWindow.Count <= MaxMessages)
            return;

        var skip = state.ContextWindow.Count - MaxMessages;
        state.ContextWindow = state.ContextWindow.Skip(skip).ToList();
    }

    private static string? DetectGoal(string text, string? currentGoal)
    {
        if (string.IsNullOrWhiteSpace(text))
            return currentGoal;

        var lower = text.ToLowerInvariant();
        if (lower.Contains("summarize") || lower.Contains("summary"))
            return "summarize";
        if (lower.Contains("describe"))
            return "describe";
        if (lower.Contains("build") || lower.Contains("compile"))
            return "build";
        if (lower.Contains("run"))
            return "run";
        if (lower.Contains("test"))
            return "test";
        if (lower.Contains("format"))
            return "format";
        if (lower.StartsWith("create ") || lower.StartsWith("make "))
            return "create";

        return currentGoal;
    }

    private static bool TryExtractTarget(string text, out string target)
    {
        target = "";
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var quoted = QuotedRx.Match(text);
        if (quoted.Success)
        {
            target = quoted.Groups["value"].Value.Trim();
            return !string.IsNullOrWhiteSpace(target);
        }

        var match = TargetRx.Match(text);
        if (match.Success)
        {
            target = match.Groups["value"].Value.Trim();
            return !string.IsNullOrWhiteSpace(target);
        }

        return false;
    }

    private static bool IsAttachmentRequired(string goal)
    {
        return goal is "summarize" or "describe";
    }

    private static bool NeedsTarget(string goal)
    {
        return goal is "create";
    }
}
