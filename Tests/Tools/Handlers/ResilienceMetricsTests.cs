#nullable enable
using System;
using RahOllamaOnly.Metrics;
using RahOllamaOnly.Tools.Handlers;
using Xunit;

namespace RahOllamaOnly.Tests.Tools.Handlers;

public sealed class ResilienceMetricsTests
{
    [Fact]
    public void Snapshot_Increments_OnStateChanges()
    {
        var before = ResilienceDiagnosticsHub.Snapshot();

        var breaker = new CircuitBreaker(1, TimeSpan.Zero);
        ResilienceDiagnosticsHub.Attach(breaker);
        breaker.RecordFailure();
        breaker.CanExecute();
        breaker.RecordSuccess();

        var after = ResilienceDiagnosticsHub.Snapshot();

        Assert.True(after.OpenCount >= before.OpenCount + 1);
        Assert.True(after.HalfOpenCount >= before.HalfOpenCount + 1);
        Assert.True(after.ClosedCount >= before.ClosedCount + 1);
    }

    [Fact]
    public void Snapshot_Increments_RetryAttempts()
    {
        var before = ResilienceDiagnosticsHub.Snapshot();

        ResilienceDiagnosticsHub.RecordRetryAttempt();
        ResilienceDiagnosticsHub.RecordRetryAttempt();

        var after = ResilienceDiagnosticsHub.Snapshot();

        Assert.True(after.RetryAttempts >= before.RetryAttempts + 2);
    }

    [Fact]
    public void Attach_MultipleBreakers_AccumulatesCounts()
    {
        var before = ResilienceDiagnosticsHub.Snapshot();

        var breakerA = new CircuitBreaker(1, TimeSpan.Zero);
        var breakerB = new CircuitBreaker(1, TimeSpan.Zero);
        ResilienceDiagnosticsHub.Attach(breakerA);
        ResilienceDiagnosticsHub.Attach(breakerB);

        breakerA.RecordFailure();
        breakerB.RecordFailure();

        var after = ResilienceDiagnosticsHub.Snapshot();

        Assert.True(after.OpenCount >= before.OpenCount + 2);
    }
}
