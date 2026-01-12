#nullable enable
using System;
using RahOllamaOnly.Tests.Mocks;
using RahOllamaOnly.Tools.Handlers;
using Xunit;

namespace RahOllamaOnly.Tests.Handlers.Resilience;

public sealed class CircuitBreakerExecutionTests
{
    private sealed class FakeClock
    {
        public DateTimeOffset Now { get; private set; } = DateTimeOffset.UtcNow;

        public void Advance(TimeSpan delta)
        {
            Now = Now.Add(delta);
        }
    }

    private sealed class TestCircuitBreaker
    {
        private readonly CircuitBreaker _breaker;
        private readonly FakeClock _clock;

        public TestCircuitBreaker(TimeSpan breakDuration)
        {
            _clock = new FakeClock();
            _breaker = new CircuitBreaker(2, breakDuration, () => _clock.Now);
        }

        public CircuitBreaker Breaker => _breaker;

        public void Advance(TimeSpan delta) => _clock.Advance(delta);
    }

    [Fact]
    public void RepeatedFailures_OpenCircuit()
    {
        var failing = new FailingProvider();
        var breaker = new CircuitBreaker(2, TimeSpan.FromSeconds(1));

        Assert.True(breaker.CanExecute());
        Assert.Throws<TimeoutException>(() => failing.Invoke());
        breaker.RecordFailure();

        Assert.True(breaker.CanExecute());
        Assert.Throws<TimeoutException>(() => failing.Invoke());
        breaker.RecordFailure();

        Assert.False(breaker.CanExecute());
    }

    [Fact]
    public void HalfOpen_Success_ClosesCircuit()
    {
        var recovery = new RecoveryProvider(0);
        var harness = new TestCircuitBreaker(TimeSpan.FromSeconds(5));

        harness.Breaker.RecordFailure();
        harness.Breaker.RecordFailure();

        harness.Advance(TimeSpan.FromSeconds(5));
        Assert.True(harness.Breaker.CanExecute());
        recovery.Invoke();
        harness.Breaker.RecordSuccess();

        Assert.True(harness.Breaker.CanExecute());
    }

    [Fact]
    public void HalfOpen_Failure_ReopensCircuit()
    {
        var recovery = new RecoveryProvider(1);
        var harness = new TestCircuitBreaker(TimeSpan.FromSeconds(5));

        harness.Breaker.RecordFailure();
        harness.Breaker.RecordFailure();

        harness.Advance(TimeSpan.FromSeconds(5));
        Assert.True(harness.Breaker.CanExecute());
        Assert.Throws<TimeoutException>(() => recovery.Invoke());
        harness.Breaker.RecordFailure();

        Assert.False(harness.Breaker.CanExecute());
    }
}
