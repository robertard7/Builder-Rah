#nullable enable
using System;
using RahOllamaOnly.Metrics;

namespace RahCli.Commands;

public static class ResilienceAlertsCommand
{
    public static int Execute(CommandContext context, bool jsonOutput)
    {
        var rules = ResilienceDiagnosticsHub.ListAlertRules();
        var eventsList = ResilienceDiagnosticsHub.ListAlertEvents();
        var payload = new { rules, events = eventsList };
        if (jsonOutput)
        {
            Output.JsonOutput.Write(payload);
        }
        else
        {
            Console.WriteLine("Alert rules:");
            foreach (var rule in rules)
                Console.WriteLine($"- {rule.Name} (open>{rule.OpenThreshold}, retry>{rule.RetryThreshold}, window={rule.WindowMinutes}m, enabled={rule.Enabled})");

            Console.WriteLine();
            Console.WriteLine("Recent alerts:");
            foreach (var alert in eventsList)
                Console.WriteLine($"- {alert.TriggeredAt:O} {alert.Message}");
        }
        return ExitCodes.Success;
    }
}
