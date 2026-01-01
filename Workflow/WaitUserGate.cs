#nullable enable
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace RahBuilder.Workflow;

public enum WaitUserAction
{
    None,
    Accept,
    Reject,
    Edit,
    AcceptStep
}

public sealed record WaitUserResponse(WaitUserAction Action, string? EditText = null, int? StepIndex = null);

public static class WaitUserGate
{
    private static readonly Regex AcceptStepRx = new(@"^accept\s+step\s+(?<n>\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static WaitUserResponse ParseResponse(string input)
    {
        var s = (input ?? "").Trim();
        if (s.Length == 0) return new WaitUserResponse(WaitUserAction.None);

        var lower = s.ToLowerInvariant();
        if (lower == "accept")
            return new WaitUserResponse(WaitUserAction.Accept);
        if (lower == "reject")
            return new WaitUserResponse(WaitUserAction.Reject);
        if (lower.StartsWith("edit", StringComparison.OrdinalIgnoreCase))
        {
            var rest = s.Length > 4 ? s.Substring(4).Trim() : "";
            return new WaitUserResponse(WaitUserAction.Edit, rest);
        }

        var m = AcceptStepRx.Match(lower);
        if (m.Success && int.TryParse(m.Groups["n"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
        {
            return new WaitUserResponse(WaitUserAction.AcceptStep, StepIndex: idx - 1);
        }

        return new WaitUserResponse(WaitUserAction.None);
    }

    public static string GateMessage(string pendingQuestion, string? toolPlanPreview = null, ConversationMode mode = ConversationMode.Conversational)
    {
        var preview = string.IsNullOrWhiteSpace(toolPlanPreview) ? "" : $"{toolPlanPreview}\n";
        if (mode == ConversationMode.Strict)
        {
            return
                "WAIT_USER\n" +
                preview +
                $"Pending: {pendingQuestion}\n" +
                "Allowed replies: accept | reject | edit <rewrite> | accept step <n>";
        }

        return
            "WAIT_USER\n" +
            preview +
            $"{pendingQuestion}\n" +
            "(You can say things like 'go ahead', 'run all', 'stop', or 'change it to <...>')";
    }
}
