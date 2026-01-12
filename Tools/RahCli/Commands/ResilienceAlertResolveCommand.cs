#nullable enable
using System;
using RahBuilder.Headless.Api;
using RahOllamaOnly.Metrics;

namespace RahCli.Commands;

public static class ResilienceAlertResolveCommand
{
    public static int Execute(CommandContext context, string? eventId, bool jsonOutput)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            if (jsonOutput)
                Output.JsonOutput.WriteError(ApiError.BadRequest("missing_alert_event_id"));
            return ExitCodes.UserError;
        }

        var updated = ResilienceDiagnosticsHub.AcknowledgeAlertEvent(eventId);
        if (updated == null)
        {
            if (jsonOutput)
                Output.JsonOutput.WriteError(ApiError.NotFound());
            return ExitCodes.UserError;
        }

        if (jsonOutput)
        {
            Output.JsonOutput.Write(updated);
        }
        else
        {
            Console.WriteLine($"Alert {updated.Id} acknowledged at {updated.AcknowledgedAt:O}");
        }

        return ExitCodes.Success;
    }
}
