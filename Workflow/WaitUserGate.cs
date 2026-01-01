#nullable enable
using System;

namespace RahBuilder.Workflow;

public static class WaitUserGate
{
    public static bool IsAllowedResponse(string input)
    {
        var s = (input ?? "").Trim().ToLowerInvariant();
        if (s.Length == 0) return false;

        return s is "yes" or "no" or "accept" or "reject" or "edit";
    }

    public static string GateMessage(string pendingQuestion)
    {
        return
            "WAIT_USER\n" +
            $"Pending: {pendingQuestion}\n" +
            "Allowed replies: yes | no | accept | reject | edit";
    }
}
