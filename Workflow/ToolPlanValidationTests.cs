#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RahBuilder.Settings;
using RahOllamaOnly.Tools;
using RahOllamaOnly.Tools.Prompt;
using RahOllamaOnly.Tracing;
using Xunit;

namespace RahBuilder.Workflow;

public sealed class ToolPlanValidationTests
{
    [Fact]
    public void GetUnknownToolIds_KnownToolId_ReturnsEmpty()
    {
        var manifest = new ToolManifest(new[]
        {
            new ToolDefinition { Id = "file.read.text", Description = "read file" }
        });
        var plan = new ToolPlan("tool_plan.v1", false, new List<ToolPlanStep>
        {
            new ToolPlanStep("1", "file.read.text", new Dictionary<string, string> { { "storedName", "doc" } }, "read")
        });

        var unknown = ToolPlanValidator.GetUnknownToolIds(plan, manifest);

        Assert.Empty(unknown);
    }

    [Fact]
    public async Task RunNextAsync_UnknownToolId_FailsWithoutAdvancing()
    {
        var state = new WorkflowState
        {
            PendingToolPlan = new ToolPlan("tool_plan.v1", false, new List<ToolPlanStep>
            {
                new ToolPlanStep("1", "unknown.tool", new Dictionary<string, string> { { "storedName", "doc" } }, "oops")
            }),
            PendingStepIndex = 0
        };

        var trace = new RunTrace(new TestTraceSink());
        var orchestrator = new ExecutionOrchestrator(state, trace);

        var result = await orchestrator.RunNextAsync(
            new AppConfig(),
            new List<AttachmentInbox.AttachmentEntry>(),
            new ToolManifest(),
            new ToolPromptRegistry(),
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Contains("Unknown toolId", result.Message);
        Assert.Equal(0, state.PendingStepIndex);
    }

    private sealed class TestTraceSink : ITraceSink
    {
        public void Trace(string line)
        {
        }
    }
}
