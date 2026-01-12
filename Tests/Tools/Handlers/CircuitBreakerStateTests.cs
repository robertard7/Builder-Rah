#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RahOllamaOnly.Tools.Handlers;
using Xunit;

namespace RahOllamaOnly.Tests.Tools.Handlers;

public sealed class CircuitBreakerStateTests
{
    [Fact]
    public void RecordFailure_OpensCircuit_OnThreshold()
    {
        var breaker = new CircuitBreaker(1, TimeSpan.FromSeconds(1));
        var transitions = new List<(CircuitState From, CircuitState To)>();
        breaker.StateChanged += (_, args) => transitions.Add((args.Previous, args.Current));

        breaker.RecordFailure();

        Assert.Single(transitions);
        Assert.Equal((CircuitState.Closed, CircuitState.Open), transitions[0]);
    }

    [Fact]
    public async Task OpenCircuit_TransitionsToHalfOpen_ThenClosedOnSuccess()
    {
        var breaker = new CircuitBreaker(1, TimeSpan.FromMilliseconds(10));
        var transitions = new List<(CircuitState From, CircuitState To)>();
        breaker.StateChanged += (_, args) => transitions.Add((args.Previous, args.Current));

        breaker.RecordFailure();

        await Task.Delay(20);

        var canExecute = breaker.CanExecute();
        Assert.True(canExecute);
        breaker.RecordSuccess();

        Assert.Equal(
            new[]
            {
                (CircuitState.Closed, CircuitState.Open),
                (CircuitState.Open, CircuitState.HalfOpen),
                (CircuitState.HalfOpen, CircuitState.Closed)
            },
            transitions);
    }

    [Fact]
    public async Task HalfOpenFailure_ReopensCircuit()
    {
        var breaker = new CircuitBreaker(1, TimeSpan.FromMilliseconds(10));
        var transitions = new List<(CircuitState From, CircuitState To)>();
        breaker.StateChanged += (_, args) => transitions.Add((args.Previous, args.Current));

        breaker.RecordFailure();
        await Task.Delay(20);
        Assert.True(breaker.CanExecute());
        breaker.RecordFailure();

        Assert.Equal(
            new[]
            {
                (CircuitState.Closed, CircuitState.Open),
                (CircuitState.Open, CircuitState.HalfOpen),
                (CircuitState.HalfOpen, CircuitState.Open)
            },
            transitions);
    }
}
