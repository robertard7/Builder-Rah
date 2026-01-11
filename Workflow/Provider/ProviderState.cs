#nullable enable

namespace RahBuilder.Workflow.Provider;

public sealed class ProviderState : IProvider
{
    public bool Enabled { get; }
    public bool Reachable { get; set; }

    public ProviderState(bool enabled, bool reachable)
    {
        Enabled = enabled;
        Reachable = reachable;
    }
}
