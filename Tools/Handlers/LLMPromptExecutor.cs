#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RahBuilder.Settings;
using RahOllamaOnly.Metrics;
using RahOllamaOnly.Tools;

namespace RahOllamaOnly.Tools.Handlers;

public sealed class LLMPromptExecutor : IPromptExecutor
{
    private readonly AppConfig _cfg;
    private readonly string _defaultRole;
    private readonly RetryPolicy _retryPolicy;
    private readonly CircuitBreaker _circuitBreaker;

    public LLMPromptExecutor(AppConfig cfg, string defaultRole, RetryPolicy? retryPolicy = null, CircuitBreaker? circuitBreaker = null)
    {
        _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
        _defaultRole = string.IsNullOrWhiteSpace(defaultRole) ? "Orchestrator" : defaultRole;
        _retryPolicy = retryPolicy ?? new RetryPolicy();
        _circuitBreaker = circuitBreaker ?? new CircuitBreaker();
        ResilienceDiagnosticsHub.Attach(_circuitBreaker);
    }

    public async Task<ExecutionResult> ExecuteAsync(string prompt, ExecutionOptions options)
    {
        if (prompt == null)
            throw new ArgumentNullException(nameof(prompt));
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        var role = string.IsNullOrWhiteSpace(options.Role) ? _defaultRole : options.Role;
        var effectiveMaxRetries = ResolveMaxRetries(options.MaxRetries, _retryPolicy.MaxRetries);
        var attempts = effectiveMaxRetries + 1;
        var resilienceEvents = options.ResilienceDebug ? new List<string>() : null;
        Exception? lastError = null;

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (!_circuitBreaker.CanExecute())
            {
                ResilienceDiagnosticsHub.RecordCircuitOpen(options.ToolId);
                return BuildCircuitOpenResult(options, resilienceEvents);
            }

            using var cts = CreateCancellation(options);
            try
            {
                var response = await LlmInvoker.InvokeChatAsync(
                    _cfg,
                    role,
                    prompt,
                    string.Empty,
                    cts.Token).ConfigureAwait(false);

                _circuitBreaker.RecordSuccess();
                return BuildSuccessResult(response, attempt + 1, options, resilienceEvents);
            }
            catch (Exception ex)
            {
                lastError = ex;
                _circuitBreaker.RecordFailure();
                if (attempt >= attempts - 1)
                {
                    resilienceEvents?.Add("retry_limit_reached");
                    break;
                }

                if (!_retryPolicy.ShouldRetry(ex))
                {
                    resilienceEvents?.Add("non_retryable_error");
                    break;
                }

                ResilienceDiagnosticsHub.RecordRetryAttempt(options.ToolId);
                var delay = _retryPolicy.GetDelay(attempt);
                resilienceEvents?.Add($"retry_attempt={attempt + 1} delayMs={delay.TotalMilliseconds:0}");
                try
                {
                    await Task.Delay(delay, options.CancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    resilienceEvents?.Add("retry_canceled");
                    break;
                }
            }
        }

        return BuildFailureResult(lastError?.Message ?? "Prompt execution failed.", attemptCount: attempts, options, resilienceEvents);
    }

    private CancellationTokenSource CreateCancellation(ExecutionOptions options)
    {
        if (options.Timeout.HasValue && options.Timeout.Value > TimeSpan.Zero)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken);
            cts.CancelAfter(options.Timeout.Value);
            return cts;
        }

        return CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken);
    }

    private static int ResolveMaxRetries(int requestedMaxRetries, int policyMaxRetries)
    {
        if (requestedMaxRetries > 0 && policyMaxRetries > 0)
            return Math.Min(requestedMaxRetries, policyMaxRetries);
        if (requestedMaxRetries > 0)
            return requestedMaxRetries;
        return policyMaxRetries;
    }

    private static ExecutionResult BuildCircuitOpenResult(ExecutionOptions options, List<string>? resilienceEvents)
    {
        const string message = "The prompt execution circuit is open. Please try again shortly.";
        resilienceEvents?.Add("circuit_open");
        return new ExecutionResult(
            false,
            message,
            message,
            BuildMetadata(options, resilienceEvents, attemptCount: 0));
    }

    private static ExecutionResult BuildSuccessResult(string output, int attemptCount, ExecutionOptions options, List<string>? resilienceEvents)
    {
        return new ExecutionResult(
            true,
            output,
            null,
            BuildMetadata(options, resilienceEvents, attemptCount));
    }

    private static ExecutionResult BuildFailureResult(string error, int attemptCount, ExecutionOptions options, List<string>? resilienceEvents)
    {
        return new ExecutionResult(
            false,
            string.Empty,
            error,
            BuildMetadata(options, resilienceEvents, attemptCount));
    }

    private static IReadOnlyDictionary<string, string?>? BuildMetadata(
        ExecutionOptions options,
        List<string>? resilienceEvents,
        int attemptCount)
    {
        if (!options.ResilienceDebug)
            return null;

        var metadata = new Dictionary<string, string?>
        {
            ["resilience.attempts"] = attemptCount.ToString(),
            ["resilience.events"] = resilienceEvents == null || resilienceEvents.Count == 0
                ? null
                : string.Join(" | ", resilienceEvents)
        };

        if (!string.IsNullOrWhiteSpace(options.ToolId))
            metadata["toolId"] = options.ToolId;

        return metadata;
    }
}
