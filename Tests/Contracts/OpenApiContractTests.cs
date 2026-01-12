#nullable enable
using System.IO;
using Xunit;

namespace RahBuilder.Tests.Contracts;

public sealed class OpenApiContractTests
{
    [Fact]
    public void OpenApiContainsSessions()
    {
        var text = File.ReadAllText("openapi.yaml");
        Assert.Contains("/sessions", text);
    }

    [Fact]
    public void OpenApiContainsResilienceHistoryAndAlerts()
    {
        var text = File.ReadAllText("openapi.yaml");
        Assert.Contains("/metrics/resilience/history", text);
        Assert.Contains("/alerts/{id}", text);
        Assert.Contains("/alerts/events/{id}", text);
        Assert.Contains("ResilienceHistoryResponse", text);
        Assert.Contains("/metrics/resilience/prometheus", text);
        Assert.Contains("ResilienceMetricsResponse", text);
        Assert.Contains("ResilienceAlertRuleResponse", text);
        Assert.Contains("ResilienceAlertEventResponse", text);
        Assert.Contains("ResilienceErrorResponse", text);
    }

    [Fact]
    public void OpenApiDocumentsCircuitOpenDetails()
    {
        var text = File.ReadAllText("openapi.yaml");
        Assert.Contains("retryAfterSeconds", text);
        Assert.Contains("circuitState", text);
        Assert.Contains("endpoint", text);
    }
}
