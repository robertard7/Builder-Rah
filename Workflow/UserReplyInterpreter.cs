#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using RahBuilder.Settings;

namespace RahBuilder.Workflow;

public enum ReplyActionType
{
    NewRequest,
    AnswerClarification,
    ApproveNextStep,
    ApproveAllSteps,
    RejectPlan,
    EditPlan
}

public sealed record UserReplyAction(ReplyActionType Action, string Payload = "");

public static class UserReplyInterpreter
{
    private static readonly string[] ApproveWords = { "go ahead", "do it", "yes", "run it", "continue", "sure", "ok", "okay", "sounds good", "go for it" };
    private static readonly string[] ApproveAllWords = { "run all", "all steps", "do everything", "run everything" };
    private static readonly string[] RejectWords = { "no", "stop", "don't", "cancel", "never mind", "nevermind" };
    private static readonly string[] EditMarkers = { "change it to", "instead", "edit:", "rewrite:", "update to" };

    public static UserReplyAction Interpret(AppConfig cfg, WorkflowState state, string text)
    {
        var mode = cfg.General.ConversationMode;
        var s = (text ?? "").Trim();
        var lower = s.ToLowerInvariant();

        if (mode == ConversationMode.Strict)
            return InterpretStrict(state, lower, s);

        // conversational
        if (MatchesAny(lower, ApproveAllWords))
            return new UserReplyAction(ReplyActionType.ApproveAllSteps);
        if (MatchesAny(lower, ApproveWords))
            return new UserReplyAction(ReplyActionType.ApproveNextStep);
        if (MatchesAny(lower, RejectWords))
            return new UserReplyAction(ReplyActionType.RejectPlan);
        if (MatchesAny(lower, EditMarkers))
            return new UserReplyAction(ReplyActionType.EditPlan, s);

        if (state.PendingQuestion != null || (state.PendingQuestions?.Count ?? 0) > 0)
            return new UserReplyAction(ReplyActionType.AnswerClarification, s);

        return new UserReplyAction(ReplyActionType.NewRequest, s);
    }

    private static UserReplyAction InterpretStrict(WorkflowState state, string lower, string original)
    {
        if (lower.StartsWith("accept step "))
            return new UserReplyAction(ReplyActionType.ApproveNextStep);
        if (lower == "accept")
            return new UserReplyAction(ReplyActionType.ApproveNextStep);
        if (lower == "reject" || lower == "no")
            return new UserReplyAction(ReplyActionType.RejectPlan);
        if (lower.StartsWith("edit"))
            return new UserReplyAction(ReplyActionType.EditPlan, original);
        if (state.PendingQuestion != null || (state.PendingQuestions?.Count ?? 0) > 0)
            return new UserReplyAction(ReplyActionType.AnswerClarification, original);
        return new UserReplyAction(ReplyActionType.NewRequest, original);
    }

    private static bool MatchesAny(string text, IEnumerable<string> phrases) =>
        phrases.Any(p => text.Contains(p, StringComparison.OrdinalIgnoreCase));
}

