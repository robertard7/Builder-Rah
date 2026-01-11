#nullable enable
using Xunit;

namespace RahBuilder.Workflow.Provider;

public sealed class ProviderManagerTests
{
    [Fact]
    public void UpdateEnabled_TogglesState()
    {
        var manager = new ProviderManager(new ProviderState(true, true));

        manager.UpdateEnabled(false);

        Assert.False(manager.State.Enabled);
    }

    [Fact]
    public void MarkReachable_UpdatesReachability()
    {
        var manager = new ProviderManager(new ProviderState(true, true));

        manager.MarkReachable(false, "test");

        Assert.False(manager.State.Reachable);
    }
}
