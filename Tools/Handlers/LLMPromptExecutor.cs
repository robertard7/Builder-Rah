#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using RahBuilder.Settings;
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
    }

    public async Task<ExecutionResult> ExecuteAsync(string prompt, ExecutionOptions options)
    {
        if (prompt == null)
            throw new ArgumentNullException(nameof(prompt));
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        var role = string.IsNullOrWhiteSpace(options.Role) ? _defaultRole : options.Role;
        var attempts = Math.Max(_retryPolicy.MaxRetries, options.MaxRetries) + 1;
        Exception? lastError = null;

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (!_circuitBreaker.CanExecute())
                return new ExecutionResult(false, string.Empty, "Prompt execution circuit is open.");

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
                return new ExecutionResult(true, response);
            }
            catch (Exception ex)
            {
                lastError = ex;
                _circuitBreaker.RecordFailure();
                if (attempt >= attempts - 1)
                    break;
                if (!_retryPolicy.ShouldRetry(ex))
                    break;

                var delay = _retryPolicy.GetDelay(attempt);
                try
                {
                    await Task.Delay(delay, options.CancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        return new ExecutionResult(false, string.Empty, lastError?.Message ?? "Prompt execution failed.");
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
}
