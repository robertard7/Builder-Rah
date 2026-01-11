#nullable enable
using System;
using RahBuilder.Headless.Api;
using RahBuilder.Workflow;
using RahOllamaOnly.Tracing;

namespace RahCli.Commands;

public static class SessionCancelCommand
{
    public static int Execute(CommandContext context, string sessionId, bool jsonOutput)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            WriteError(jsonOutput, ApiError.BadRequest("session_id_required"));
            return ExitCodes.UserError;
        }

        var store = new SessionStore();
        var state = store.Load(sessionId);
        if (state == null)
        {
            WriteError(jsonOutput, ApiError.NotFound("session_not_found"));
            return ExitCodes.RemoteError;
        }

        var trace = new RunTrace(new TracePanelTraceSink(new RahOllamaOnly.Ui.TracePanelWriter()));
        var workflow = new WorkflowFacade(context.Config, trace);
        workflow.ApplySessionState(state);
        workflow.StopPlan();
        var updated = workflow.BuildSessionState();
        store.Save(updated);

        var payload = new { sessionId, status = workflow.GetPublicSnapshot() };
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
