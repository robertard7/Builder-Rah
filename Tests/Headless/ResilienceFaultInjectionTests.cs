#nullable enable
using System;
using System.Threading.Tasks;
using RahBuilder.Settings;
using RahBuilder.Tests.Helpers;
using RahOllamaOnly.Metrics;
using RahOllamaOnly.Tools;
using RahOllamaOnly.Tools.Handlers;
using Xunit;

namespace RahBuilder.Tests.Headless;

public sealed class ResilienceFaultInjectionTests
{
    [Fact]
    public async Task FaultInjection_TriggersAlertOnOpen()
    {
        ResilienceDiagnosticsHub.Reset();
        var config = new AppConfig { General = { ProviderEnabled = true } };
        var breaker = new CircuitBreaker(failureThreshold: 1, breakDuration: TimeSpan.FromMinutes(1));
        var executor = new LLMPromptExecutor(config, "Orchestrator", circuitBreaker: breaker);
        var sequence = new FaultInjectionProvider(new[] { false, false });
        var originalReachable = LlmInvoker.IsProviderReachable;
        LlmInvoker.IsProviderReachable = () => sequence.Next();
        try
        {
            ResilienceDiagnosticsHub.AddAlertRule("open-spike", openThreshold: 0, retryThreshold: 0, windowMinutes: 60, severity: "critical");
            ResilienceDiagnosticsHub.Snapshot();

            await executor.ExecuteAsync("test", new ExecutionOptions { ToolId = "fault" });
            ResilienceDiagnosticsHub.Snapshot();

            var eventsList = ResilienceDiagnosticsHub.ListAlertEvents(10);
            Assert.Contains(eventsList, evt => evt.Severity == "critical");
        }
        finally
        {
            LlmInvoker.IsProviderReachable = originalReachable;
        }
    }
}
