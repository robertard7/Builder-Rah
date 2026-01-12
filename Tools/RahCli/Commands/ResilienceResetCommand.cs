#nullable enable
using System;
using RahOllamaOnly.Metrics;

namespace RahCli.Commands;

public static class ResilienceResetCommand
{
    public static int Execute(CommandContext context, bool jsonOutput)
    {
        ResilienceDiagnosticsHub.Reset();
        var payload = new { ok = true, resetAt = DateTimeOffset.UtcNow };
        if (jsonOutput)
            Output.JsonOutput.Write(payload);
        else
            Output.TextOutput.Write(payload);
        return ExitCodes.Success;
    }
}
