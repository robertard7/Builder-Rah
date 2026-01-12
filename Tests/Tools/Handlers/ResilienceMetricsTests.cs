#nullable enable
using System;
using RahOllamaOnly.Metrics;
using RahOllamaOnly.Tools.Handlers;
using Xunit;

namespace RahOllamaOnly.Tests.Tools.Handlers;

public sealed class ResilienceMetricsTests
{
    private static readonly object MetricsLock = new();

    [Fact]
    public void Snapshot_Increments_OnStateChanges()
    {
        lock (MetricsLock)
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
    }

    [Fact]
    public void Snapshot_Increments_RetryAttempts()
    {
        lock (MetricsLock)
        {
            var before = ResilienceDiagnosticsHub.Snapshot();

            ResilienceDiagnosticsHub.RecordRetryAttempt();
            ResilienceDiagnosticsHub.RecordRetryAttempt();

            var after = ResilienceDiagnosticsHub.Snapshot();

            Assert.True(after.RetryAttempts >= before.RetryAttempts + 2);
        }
    }

    [Fact]
    public void Attach_MultipleBreakers_AccumulatesCounts()
    {
        lock (MetricsLock)
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

    [Fact]
    public void Reset_ClearsMetrics()
    {
        lock (MetricsLock)
        {
            ResilienceDiagnosticsHub.Reset();

            var breaker = new CircuitBreaker(1, TimeSpan.Zero);
            ResilienceDiagnosticsHub.Attach(breaker);
            breaker.RecordFailure();
            ResilienceDiagnosticsHub.RecordRetryAttempt();

            ResilienceDiagnosticsHub.Reset();
            var after = ResilienceDiagnosticsHub.Snapshot();

            Assert.Equal(0, after.OpenCount);
            Assert.Equal(0, after.HalfOpenCount);
            Assert.Equal(0, after.ClosedCount);
            Assert.Equal(0, after.RetryAttempts);
        }
    }
}
