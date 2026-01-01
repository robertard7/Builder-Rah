#nullable enable
namespace RahBuilder.Settings;

public sealed class ToolchainSettings
{
    // future: container-only toolchain settings live here
    public bool ContainerOnly { get; set; } = true;
}
