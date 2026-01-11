#nullable enable
using RahBuilder.Workflow;
using Xunit;

namespace RahBuilder.Tests.Workflow;

public sealed class IntentBuilderTests
{
    [Fact]
    public void BuildDocument_ReadyIntentHasGoal()
    {
        var builder = new IntentBuilder();
        var state = new IntentState
        {
            SessionId = "s",
            CurrentGoal = "summarize",
            Status = IntentStatus.Ready
        };

        var doc = builder.BuildDocument(state);

        Assert.Equal("intent.v1", doc.Version);
        Assert.True(doc.Ready);
        Assert.Equal("summarize", doc.Goal);
    }
}
