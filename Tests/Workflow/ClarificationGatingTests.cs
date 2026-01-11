#nullable enable
using RahBuilder.Workflow;
using Xunit;

namespace RahBuilder.Tests.Workflow;

public sealed class ClarificationGatingTests
{
    [Fact]
    public void IntentValidation_FlagsMissingGoal()
    {
        var doc = new IntentDocumentV1(
            "intent.v1",
            "",
            "",
            new string[0],
            new string[0],
            new string[0],
            false,
            "");

        var errors = IntentValidation.Validate(doc);

        Assert.Contains("goal_required", errors);
    }
}
