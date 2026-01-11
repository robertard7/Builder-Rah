#nullable enable
using RahBuilder.Settings;

namespace RahCli;

public sealed class CommandContext
{
    public CommandContext(bool jsonOutput, AppConfig config)
    {
        JsonOutput = jsonOutput;
        Config = config;
    }

    public bool JsonOutput { get; }
    public AppConfig Config { get; }
}
