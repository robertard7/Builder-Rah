#nullable enable
using System.Collections.Generic;
using Xunit;

namespace RahBuilder.Workflow;

public sealed class PlanTests
{
    [Fact]
    public void OrderedActions_FromConnectors()
    {
        var actions = IntentExtractionParser.SplitByConnectors("describe image then build api and then add tests");
        Assert.Contains("describe image", actions);
        Assert.Contains("build api", actions);
    }

    [Fact]
    public void SemanticIntent_MapsCompound()
    {
        var ordered = IntentExtractionParser.MapAction("add authentication");
        Assert.Equal("code.generate.auth", ordered);
    }

    [Fact]
    public void PlanDefinition_ToToolPlan_RoundTrip()
    {
        var plan = new PlanDefinition
        {
            Session = "s",
            Steps = new List<PlanStep>
            {
                new PlanStep { Order = 1, ToolId = "vision.describe.image", Description = "describe", Inputs = new Dictionary<string, string>{{"storedName","img"}} }
            }
        };

        var tp = plan.ToToolPlan();
        Assert.Single(tp.Steps);
        Assert.Equal("vision.describe.image", tp.Steps[0].ToolId);
    }
}
