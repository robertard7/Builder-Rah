#nullable enable
using System.Threading;
using System.Threading.Tasks;
using RahBuilder.Settings;
using RahBuilder.Workflow;
using RahOllamaOnly.Tracing;
using Xunit;

namespace RahBuilder.Workflow.Provider;

public sealed class WorkflowFacadeProviderTests
{
    [Fact]
    public async Task RouteUserInput_WhenProviderDisabled_StopsWorkflow()
    {
        var cfg = new AppConfig
        {
            General = { ProviderEnabled = false, EnableProviderApi = false }
        };
        var trace = new RunTrace(new NullTraceSink());
        var facade = new WorkflowFacade(cfg, trace);
        string? message = null;
        facade.UserFacingMessage += msg => message = msg;

        await facade.RouteUserInput("hello", CancellationToken.None);

        Assert.Equal("Provider disabled — enable in Settings to continue.", message);
    }

    [Fact]
    public async Task RouteUserInput_WhenProviderOffline_StopsWorkflow()
    {
        var cfg = new AppConfig
        {
            General = { ProviderEnabled = true, EnableProviderApi = false }
        };
        var trace = new RunTrace(new NullTraceSink());
        var facade = new WorkflowFacade(cfg, trace);
        string? message = null;
        facade.UserFacingMessage += msg => message = msg;
        facade.MarkProviderReachable(false, "test");

        await facade.RouteUserInput("hello", CancellationToken.None);

        Assert.Equal("Provider currently unavailable — try ‘Retry Provider’ or check settings.", message);
    }

    private sealed class NullTraceSink : ITraceSink
    {
        public void Trace(string line)
        {
        }
    }
}
