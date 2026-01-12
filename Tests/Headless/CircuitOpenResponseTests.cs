#nullable enable
using RahBuilder.Workflow;
using Xunit;

namespace RahBuilder.Tests.Headless;

public sealed class CircuitOpenResponseTests
{
    [Fact]
    public void CircuitOpenIncludesDetails()
    {
        var error = HeadlessApiServer.BuildCircuitOpenError("/sessions/abc/message");
        Assert.NotNull(error.Details);
        Assert.True(error.Details!.ContainsKey("retryAfterSeconds"));
        Assert.True(error.Details.ContainsKey("circuitState"));
        Assert.True(error.Details.ContainsKey("endpoint"));
    }
}
