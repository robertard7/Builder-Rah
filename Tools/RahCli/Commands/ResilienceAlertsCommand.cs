#nullable enable
using System;
using System.Linq;
using RahOllamaOnly.Metrics;

namespace RahCli.Commands;

public static class ResilienceAlertsCommand
{
    public static int Execute(CommandContext context, string[] args, bool jsonOutput)
    {
        if (args.Length > 0 && string.Equals(args[0], "list", StringComparison.OrdinalIgnoreCase))
            args = args.Skip(1).ToArray();
        var severity = GetArgument(args, "--severity");
        var includeAcknowledged = !args.Any(a => string.Equals(a, "--active-only", StringComparison.OrdinalIgnoreCase));
        var rules = ResilienceDiagnosticsHub.ListAlertRules();
        var eventsList = ResilienceDiagnosticsHub.ListAlertEvents(50, severity, includeAcknowledged);
        var payload = new { rules, events = eventsList };
        if (jsonOutput)
        {
            Output.JsonOutput.Write(payload);
        }
        else
        {
            Console.WriteLine("Alert rules:");
            foreach (var rule in rules)
                Console.WriteLine($"- {rule.Name} (open>{rule.OpenThreshold}, retry>{rule.RetryThreshold}, window={rule.WindowMinutes}m, enabled={rule.Enabled}, severity={rule.Severity})");

            Console.WriteLine();
            Console.WriteLine("Recent alerts:");
            foreach (var alert in eventsList)
            {
                var status = alert.Acknowledged ? "acknowledged" : "active";
                Console.WriteLine($"- {alert.TriggeredAt:O} [{status}] {alert.Message}");
            }

            var criticalActive = eventsList.Count(alert => !alert.Acknowledged && string.Equals(alert.Severity, "critical", StringComparison.OrdinalIgnoreCase));
            if (criticalActive > 0)
                Console.Error.WriteLine($"[resilience] {criticalActive} critical alert(s) active.");
        }
        return ExitCodes.Success;
    }

    private static string? GetArgument(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }
}
