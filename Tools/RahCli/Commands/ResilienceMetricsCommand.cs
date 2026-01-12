#nullable enable
using RahOllamaOnly.Metrics;

namespace RahCli.Commands;

public static class ResilienceMetricsCommand
{
    public static int Execute(CommandContext context, bool jsonOutput)
    {
        var metrics = ResilienceDiagnosticsHub.Snapshot();
        if (jsonOutput)
            Output.JsonOutput.Write(metrics);
        else
            Output.TextOutput.Write(metrics);
        return ExitCodes.Success;
    }
}
