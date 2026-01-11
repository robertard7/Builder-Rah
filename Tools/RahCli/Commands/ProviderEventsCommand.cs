#nullable enable
using RahBuilder.Workflow.Provider;

namespace RahCli.Commands;

public static class ProviderEventsCommand
{
    public static int Execute(CommandContext context, bool jsonOutput)
    {
        var eventsList = ProviderDiagnosticsHub.Events;
        if (jsonOutput)
            Output.JsonOutput.Write(eventsList);
        else
            Output.TextOutput.Write(eventsList);
        return ExitCodes.Success;
    }
}
