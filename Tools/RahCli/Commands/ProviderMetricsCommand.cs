#nullable enable
using RahBuilder.Workflow.Provider;

namespace RahCli.Commands;

public static class ProviderMetricsCommand
{
    public static int Execute(CommandContext context, bool jsonOutput)
    {
        var metrics = ProviderDiagnosticsHub.LatestMetrics;
        if (jsonOutput)
            Output.JsonOutput.Write(metrics);
        else
            Output.TextOutput.Write(metrics);
        return ExitCodes.Success;
    }
}
