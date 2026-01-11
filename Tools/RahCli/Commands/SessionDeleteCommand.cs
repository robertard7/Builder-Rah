#nullable enable
using System;
using RahBuilder.Headless.Api;
using RahBuilder.Workflow;

namespace RahCli.Commands;

public static class SessionDeleteCommand
{
    public static int Execute(CommandContext context, string sessionId, bool jsonOutput)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            WriteError(jsonOutput, ApiError.BadRequest("session_id_required"));
            return ExitCodes.UserError;
        }

        var store = new SessionStore();
        store.Delete(sessionId);
        var payload = new { ok = true, sessionId };
        if (jsonOutput)
            Output.JsonOutput.Write(payload);
        else
            Output.TextOutput.Write(payload);
        return ExitCodes.Success;
    }

    private static void WriteError(bool jsonOutput, ApiError error)
    {
        if (jsonOutput)
            Output.JsonOutput.WriteError(error);
        else
            Output.TextOutput.WriteError(error);
    }
}
